using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Moves the air terminals in this model onto the air terminals of a
    // linked architectural model - the architect's reflected ceiling plan
    // is the authority for where a diffuser sits, and for how high.
    //
    // Matching is done on the PLAN distance only. The elevation in the
    // MEP model is exactly what is usually wrong, so including it in the
    // search would let a badly levelled terminal match the wrong ceiling.
    public class ALIGNTERMINALS
    {
        public class Options
        {
            // How far a terminal may be from its architectural
            // counterpart and still be considered the same one
            public double MaxDistanceMm = 1500;

            public bool MatchElevation = true;

            // Restrict a match to terminals that are the same family, or
            // the same family and type
            public bool SameFamily;
            public bool SameType;

            public List<long> LinkIds = new List<long>();

            // Restricts the work to these terminals. Empty means every
            // air terminal in the model.
            public HashSet<long> OnlyTerminals = new HashSet<long>();
        }

        public class Match
        {
            public long TerminalId { get; set; }
            public string TerminalName { get; set; }
            public string LinkName { get; set; }
            public string TargetName { get; set; }

            public XYZ From { get; set; }
            public XYZ To { get; set; }

            public double PlanDistanceMm { get; set; }
            public double RiseMm { get; set; }

            // Why it cannot be aligned, or what happened when it was
            public string Problem { get; set; }
            public bool Moved { get; set; }

            public bool CanMove
            {
                get { return To != null && string.IsNullOrEmpty(Problem); }
            }
        }

        private class Candidate
        {
            public XYZ Point;
            public string Family;
            public string Type;
            public string LinkName;
            public bool Taken;
        }

        // ==================================================
        // Finding the matches
        // ==================================================

        public static List<Match> Plan(Document doc, Options options)
        {
            var matches = new List<Match>();

            if (doc == null || options == null)
                return matches;

            double maxFeet = ToFeet(options.MaxDistanceMm);

            List<Candidate> targets = CollectFromLinks(doc, options);

            // Bucketed by the search radius, so each terminal only looks
            // at the nine cells around it rather than every candidate
            Dictionary<long, List<Candidate>> grid = BuildGrid(targets, maxFeet);

            var pairs = new List<Tuple<double, FamilyInstance, Candidate>>();

            List<FamilyInstance> scope = InScope(doc, options);

            foreach (FamilyInstance terminal in scope)
            {
                XYZ point = PointOf(terminal);

                if (point == null)
                {
                    matches.Add(new Match
                    {
                        TerminalId = terminal.Id.ToValue(),
                        TerminalName = NameOf(terminal),
                        Problem = "no insertion point to move"
                    });

                    continue;
                }

                foreach (Candidate candidate in Near(grid, point, maxFeet))
                {
                    if (!Allowed(terminal, candidate, options))
                        continue;

                    double distance = PlanDistance(point, candidate.Point);

                    if (distance > maxFeet)
                        continue;

                    pairs.Add(Tuple.Create(distance, terminal, candidate));
                }
            }

            // Nearest first, and each architectural terminal claimed
            // once - otherwise two MEP terminals can be dragged onto the
            // same diffuser and stack on top of each other
            pairs.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            var used = new HashSet<long>();

            foreach (Tuple<double, FamilyInstance, Candidate> pair in pairs)
            {
                long id = pair.Item2.Id.ToValue();

                if (used.Contains(id) || pair.Item3.Taken)
                    continue;

                used.Add(id);
                pair.Item3.Taken = true;

                XYZ from = PointOf(pair.Item2);
                XYZ to = pair.Item3.Point;

                if (!options.MatchElevation)
                    to = new XYZ(to.X, to.Y, from.Z);

                matches.Add(new Match
                {
                    TerminalId = id,
                    TerminalName = NameOf(pair.Item2),
                    LinkName = pair.Item3.LinkName,
                    TargetName = pair.Item3.Family + " : " + pair.Item3.Type,
                    From = from,
                    To = to,
                    PlanDistanceMm = FromFeet(pair.Item1),
                    RiseMm = FromFeet(to.Z - from.Z)
                });
            }

            // Everything left over had nothing near enough
            foreach (FamilyInstance terminal in scope)
            {
                long id = terminal.Id.ToValue();

                if (used.Contains(id))
                    continue;

                if (matches.Any(m => m.TerminalId == id))
                    continue;

                matches.Add(new Match
                {
                    TerminalId = id,
                    TerminalName = NameOf(terminal),
                    Problem = "nothing within " +
                              options.MaxDistanceMm.ToString("N0") + " mm"
                });
            }

            matches.Sort((a, b) =>
            {
                // Anything actionable first, farthest move at the top of
                // it - those are the ones worth looking at
                if (a.CanMove != b.CanMove)
                    return a.CanMove ? -1 : 1;

                return b.PlanDistanceMm.CompareTo(a.PlanDistanceMm);
            });

            return matches;
        }

        // ==================================================
        // Moving them
        // ==================================================

        // Must be called inside a transaction
        public static int Apply(Document doc, IEnumerable<Match> matches)
        {
            int moved = 0;

            foreach (Match match in matches)
            {
                if (!match.CanMove)
                    continue;

                Element element = doc.GetElement(
                    REVITCOMPAT.NewId(match.TerminalId));

                if (element == null)
                {
                    match.Problem = "no longer in the model";
                    continue;
                }

                XYZ before = PointOf(element as FamilyInstance);

                if (before == null)
                {
                    match.Problem = "no insertion point to move";
                    continue;
                }

                try
                {
                    ElementTransformUtils.MoveElement(
                        doc, element.Id, match.To - before);
                }
                catch (Exception ex)
                {
                    match.Problem = Short(ex.Message);
                    continue;
                }

                // A face hosted terminal follows its ceiling, so the move
                // can quietly come out somewhere else. Say so rather than
                // reporting a success that did not happen.
                XYZ after = PointOf(element as FamilyInstance);

                if (after == null)
                {
                    match.Moved = true;
                    moved++;

                    continue;
                }

                double offMm = FromFeet(after.DistanceTo(match.To));

                if (offMm > 1.0)
                {
                    double planOffMm =
                        FromFeet(PlanDistance(after, match.To));

                    match.Problem = planOffMm <= 1.0
                        ? "aligned in plan; elevation held by its host"
                        : "host would not let it move (" +
                          offMm.ToString("N0") + " mm out)";

                    continue;
                }

                match.Moved = true;
                moved++;
            }

            return moved;
        }

        // ==================================================
        // Collecting
        // ==================================================

        // The terminals this run is allowed to touch
        private static List<FamilyInstance> InScope(
            Document doc, Options options)
        {
            var scope = new List<FamilyInstance>();

            bool everything = options.OnlyTerminals == null ||
                              options.OnlyTerminals.Count == 0;

            foreach (FamilyInstance terminal in Terminals(doc))
            {
                if (everything ||
                    options.OnlyTerminals.Contains(terminal.Id.ToValue()))
                    scope.Add(terminal);
            }

            return scope;
        }

        private static IEnumerable<FamilyInstance> Terminals(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DuctTerminal)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();
        }

        private static List<Candidate> CollectFromLinks(
            Document doc, Options options)
        {
            var found = new List<Candidate>();

            foreach (long linkId in options.LinkIds)
            {
                var link = doc.GetElement(REVITCOMPAT.NewId(linkId))
                    as RevitLinkInstance;

                if (link == null)
                    continue;

                Document linkDoc = link.GetLinkDocument();

                if (linkDoc == null)
                    continue;

                Transform transform = link.GetTransform();

                foreach (FamilyInstance terminal in Terminals(linkDoc))
                {
                    XYZ point = PointOf(terminal);

                    if (point == null)
                        continue;

                    found.Add(new Candidate
                    {
                        // The link has its own origin and rotation
                        Point = transform.OfPoint(point),
                        Family = FamilyOf(terminal),
                        Type = TypeOf(terminal),
                        LinkName = linkDoc.Title
                    });
                }
            }

            return found;
        }

        public static int CountInLinks(Document doc, Options options)
        {
            return CollectFromLinks(doc, options).Count;
        }

        public static int CountInModel(Document doc)
        {
            return Terminals(doc).Count();
        }

        // ==================================================
        // The grid
        // ==================================================

        private static Dictionary<long, List<Candidate>> BuildGrid(
            List<Candidate> candidates, double cell)
        {
            var grid = new Dictionary<long, List<Candidate>>();

            if (cell <= 0)
                cell = 1;

            foreach (Candidate candidate in candidates)
            {
                long key = CellKey(candidate.Point, cell);

                List<Candidate> bucket;

                if (!grid.TryGetValue(key, out bucket))
                {
                    bucket = new List<Candidate>();
                    grid[key] = bucket;
                }

                bucket.Add(candidate);
            }

            return grid;
        }

        private static long CellKey(XYZ point, double cell)
        {
            long x = (long)Math.Floor(point.X / cell);
            long y = (long)Math.Floor(point.Y / cell);

            // Two 32 bit cell indices packed into one key
            return (x << 32) ^ (y & 0xFFFFFFFFL);
        }

        private static IEnumerable<Candidate> Near(
            Dictionary<long, List<Candidate>> grid, XYZ point, double cell)
        {
            if (cell <= 0)
                cell = 1;

            long cx = (long)Math.Floor(point.X / cell);
            long cy = (long)Math.Floor(point.Y / cell);

            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    long key = ((cx + dx) << 32) ^ ((cy + dy) & 0xFFFFFFFFL);

                    List<Candidate> bucket;

                    if (!grid.TryGetValue(key, out bucket))
                        continue;

                    foreach (Candidate candidate in bucket)
                    {
                        if (!candidate.Taken)
                            yield return candidate;
                    }
                }
            }
        }

        // ==================================================
        // Small helpers
        // ==================================================

        private static bool Allowed(
            FamilyInstance terminal, Candidate candidate, Options options)
        {
            if (options.SameType)
            {
                return Same(FamilyOf(terminal), candidate.Family) &&
                       Same(TypeOf(terminal), candidate.Type);
            }

            if (options.SameFamily)
                return Same(FamilyOf(terminal), candidate.Family);

            return true;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static double PlanDistance(XYZ a, XYZ b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static XYZ PointOf(FamilyInstance instance)
        {
            if (instance == null)
                return null;

            var location = instance.Location as LocationPoint;

            return location == null ? null : location.Point;
        }

        private static string FamilyOf(FamilyInstance instance)
        {
            if (instance.Symbol == null || instance.Symbol.Family == null)
                return string.Empty;

            return instance.Symbol.Family.Name;
        }

        private static string TypeOf(FamilyInstance instance)
        {
            return instance.Symbol == null
                ? string.Empty
                : instance.Symbol.Name;
        }

        private static string NameOf(FamilyInstance instance)
        {
            return FamilyOf(instance) + " : " + TypeOf(instance);
        }

        private static string Short(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "could not be moved";

            message = message.Replace("\r", " ").Replace("\n", " ").Trim();

            return message.Length <= 90
                ? message
                : message.Substring(0, 87) + "...";
        }

        private static double ToFeet(double millimetres)
        {
            return UnitUtils.ConvertToInternalUnits(
                millimetres, UnitTypeId.Millimeters);
        }

        private static double FromFeet(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(
                feet, UnitTypeId.Millimeters);
        }
    }
}
