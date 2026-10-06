using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // The QA/QC framework. One check is one class implementing IQACHECK
    // and one line in QACHECKS.All() - nothing else has to change, which
    // is the point: this add-in is expected to grow a lot of checks.
    public enum QASeverity
    {
        Issue = 0,
        Warning = 1,
        Info = 2
    }

    public class QAFinding
    {
        public string Check { get; set; }
        public QASeverity Severity { get; set; }

        public string SheetNumber { get; set; }
        public string SheetName { get; set; }
        public string ViewName { get; set; }

        // The view the finding was seen in - marking colours the element
        // in that view, not everywhere
        public long ViewId { get; set; }

        // What the Show button jumps to - usually the tag, since that is
        // what is wrong on the drawing
        public long ElementId { get; set; }

        public string Element { get; set; }
        public string Problem { get; set; }
        public string Value { get; set; }

        // What can put this right, if anything - see QAGRIDFIX and
        // QADUCTFIX. Left empty by a check that only reports.
        public string FixKind { get; set; }

        // Whatever the fixer named by FixKind needs and cannot work out
        // again from the element alone - a connector id, say. Opaque to
        // everything else: only the check that set FixKind and the fix
        // that reads it ever look inside.
        public string FixData { get; set; }

        public string Sheet
        {
            get
            {
                if (string.IsNullOrEmpty(SheetNumber))
                    return SheetName ?? string.Empty;

                return SheetNumber + " - " + SheetName;
            }
        }
    }

    public class QASCOPE
    {
        public enum Kind
        {
            AllSheets,
            PickedSheets,
            ActiveView,

            // A duct disconnection belongs to the model, not to a sheet -
            // a duct nobody drew on a sheet is still broken
            WholeModel,
            PickedLevels
        }

        public Kind What { get; set; }

        public HashSet<long> SheetIds { get; set; } = new HashSet<long>();

        public HashSet<long> LevelIds { get; set; } = new HashSet<long>();

        // The view open when the checks ran, filled in by BuildContext
        // because the window has no API context to ask. The model-wide
        // pages collect FROM this view rather than from the document -
        // see QACHECKS.Look. Zero anywhere else.
        public long ViewId { get; set; }
    }

    // Worked out once and handed to every check, so ten checks do not
    // each walk the sheet list again
    public class QACONTEXT
    {
        // What the window asked for. A check that works on the model
        // rather than on sheets reads its own filter out of here.
        public QASCOPE Scope { get; set; }

        // Somewhere a family of checks can leave one expensive walk of
        // the model, so four of them do not each do it again. Keyed by
        // whatever name that family agrees on.
        public Dictionary<string, object> Shared { get; set; } =
            new Dictionary<string, object>();

        public List<ViewSheet> Sheets { get; set; } = new List<ViewSheet>();

        // Every view the checks should look in
        public List<View> Views { get; set; } = new List<View>();

        // Which sheet a view is placed on, by view id
        public Dictionary<long, ViewSheet> SheetOfView { get; set; } =
            new Dictionary<long, ViewSheet>();

        public ViewSheet SheetFor(View view)
        {
            ViewSheet sheet;

            return SheetOfView.TryGetValue(view.Id.ToValue(), out sheet)
                ? sheet
                : null;
        }
    }

    // The tabs the checks are grouped under. A check names its group and
    // the window puts it on the right tab - adding a group is adding a
    // string here and a tab page in the designer.
    public class QAGROUPS
    {
        public const string Flowrate = "Flowrate check";
        public const string Grids = "Grids";
        public const string Ducts = "Duct disconnection check";
        public const string Fcu = "FCU flowrate check";
        public const string Chw = "CHW check";
        public const string ChwJoints = "CHW pipe disconnection check";

        public static string[] InOrder()
        {
            return new[]
            {
                Flowrate, Grids, Ducts, Fcu, Chw, ChwJoints
            };
        }
    }

    // Everything the window can tell a check. One object rather than a
    // signature per check, so adding a setting does not disturb the
    // checks that do not care about it.
    public class QASETTINGS
    {
        // Grids: the model the grids were copied from
        public List<long> GridLinkIds { get; set; } = new List<long>();

        // How far a grid may sit from its counterpart, or fall short of
        // its neighbours, before it is worth reporting
        public double GridToleranceMm { get; set; } = 50;
        public double ExtentToleranceMm { get; set; } = 200;

        // Where the bubbles are supposed to be
        public bool BubbleTop { get; set; } = true;
        public bool BubbleLeft { get; set; } = true;

        // Ducts: how far from an open end to look for a duct to join
        // it into, and how close two open ends must be to count as
        // meeting
        public double DuctReachMm { get; set; } = 1500;
        public double DuctJoinMm { get; set; } = 25;

        // An end that belongs to a system is joined into that system.
        // With this on, one whose own system has nothing within reach
        // falls back to the nearest duct of any system.
        public bool DuctFallBackToNearest { get; set; }

        // FCUs: the schedule, read in the window and handed over here
        // so a check never has to touch a file
        public List<FCUSHEET.Unit> FcuUnits { get; set; } =
            new List<FCUSHEET.Unit>();

        // Which parameter on the equipment holds the unit's number
        public string FcuParameter { get; set; } = QAFCU.ByMark;

        // Out by more than either of these counts as wrong - the
        // percentage keeps small units from being judged too harshly,
        // the litres keep large ones from being judged too finely
        public double FcuTolerancePercent { get; set; } = 2;
        public double FcuToleranceLitres { get; set; } = 5;

        // Chilled water: the sizing table, read in the window, and
        // which piping system types carry it.
        //
        // Supply and return are kept apart because that is how the job
        // is drawn and how the findings read - the same way CHW VOLUME
        // asks for them. Hydraulically the two sides carry the same
        // flow, so the sizing is identical; the split is for saying
        // WHICH side a finding is on, and for letting somebody check
        // one side without the other.
        public CHWSIZESHEET.Table ChwTable { get; set; }

        public HashSet<long> ChwSupplyTypeIds { get; set; } =
            new HashSet<long>();

        public HashSet<long> ChwReturnTypeIds { get; set; } =
            new HashSet<long>();

        public int ChwSystemTypeCount
        {
            get
            {
                return ChwSupplyTypeIds.Count + ChwReturnTypeIds.Count;
            }
        }

        // A nominal size comes back from Revit as 49.999999 often
        // enough that matching it to the table needs slack
        public double ChwSizeToleranceMm { get; set; } = 2;

        // Oversizing is noisy by nature, so it is only worth reporting
        // when it is well clear and on a pipe big enough to care about
        public int ChwOversizeSteps { get; set; } = 2;
        // May a fitting be slid along to save a pipe that would
        // otherwise be deleted? It moves pipework, in millimetres, so
        // it is the user's call - off means the run is refused instead
        // and nothing at all changes there.
        public bool ChwKeepShortPipes { get; set; }

        public double ChwOversizeFromMm { get; set; } = 50;

        // CHW disconnections: how far apart two ends, or an end and a
        // connector, can be and still be "meant to be joined". Generous
        // for pipework, and still far too small to reach across a
        // plant room and invent a connection nobody drew.
        public double ChwJointReachMm { get; set; } = 300;

        // CHW disconnections: may a pipe be joined to a connector that
        // carries NO system classification?
        //
        // ON by default, because off it makes this page do nothing at
        // all on the families it most needs to work with. It cannot
        // cause the mistake the system rule exists to prevent - two
        // KNOWN systems must still agree - so what it opens up is only
        // the connectors Revit has no opinion about.
        public bool ChwJoinUnknownSystem { get; set; } = true;

        // CHW disconnections: which piping system types to check.
        //
        // EMPTY MEANS EVERY HYDRONIC ONE - the page still works on a
        // model nobody has set up, and that fallback asks Revit's own
        // classification rather than reading names. Ticking narrows it:
        // a job with two CHW circuits, or with heating pipework drawn
        // on hydronic system types, is checked one circuit at a time.
        public HashSet<long> ChwJointSystemTypeIds { get; set; } =
            new HashSet<long>();

        // CHW disconnections: which mechanical equipment families a
        // pipe may be joined to. EMPTY MEANS EVERY family - the page
        // still works on a model nobody has set up - but a model with
        // pumps, chillers and air handling units in it will list ends
        // that were never meant to be on a fan coil, so narrowing it is
        // usually the first thing somebody does.
        public HashSet<long> ChwEquipFamilyIds { get; set; } =
            new HashSet<long>();

        public string BubbleDescription
        {
            get
            {
                return (BubbleTop ? "top" : "bottom") + " and " +
                       (BubbleLeft ? "left" : "right");
            }
        }
    }

    public interface IQACHECK
    {
        // Which tab it appears on
        string Group { get; }

        // Shown in the list of checks
        string Name { get; }

        // One line under it, saying what it looks for
        string Explanation { get; }

        List<QAFinding> Run(
            Document doc, QACONTEXT context, QASETTINGS settings);
    }

    public class QACHECKS
    {
        // The registry. Add a check here and it appears in the window.
        public static List<IQACHECK> All()
        {
            return new List<IQACHECK>
            {
                new QAFLOWCHECK(),
                new QAGRIDLINKCHECK(),
                new QAGRIDEXTENTCHECK(),
                new QAGRIDBUBBLECHECK(),
                new QADUCTTAPCHECK(),
                new QADUCTBRANCHCHECK(),
                new QADUCTJOINCHECK(),
                new QADUCTSTRANDEDCHECK(),
                new QAFCUFLOWCHECK(),
                new QAFCUMATCHCHECK(),
                new QACHWSIZECHECK(),
                new QACHWOVERSIZECHECK(),
                new QACHWFCUJOINTCHECK(),
                new QACHWMEETCHECK(),
                new QACHWSTRANDEDCHECK()
            };
        }

        // ==================================================
        // Scope
        // ==================================================

        public static List<ViewSheet> AllSheets(Document doc)
        {
            var sheets =
                new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .ToList();

            sheets.Sort((a, b) =>
                CompareSheetNumbers(a.SheetNumber, b.SheetNumber));

            return sheets;
        }

        // Where a model-wide page should look for its elements.
        //
        // "The view open now" has to mean what REVIT means by it, and
        // that is not a property anything carries: whether a pipe shows
        // in a view is decided by the crop region, the view range, the
        // discipline, hidden elements and every view filter on it. No
        // per-element test can answer that. A view-scoped collector
        // can, because it IS Revit's answer - so the scope has to reach
        // the collector rather than being applied afterwards.
        public static FilteredElementCollector Look(
            Document doc, QASCOPE scope)
        {
            if (scope != null
                && scope.What == QASCOPE.Kind.ActiveView
                && scope.ViewId > 0)
            {
                return new FilteredElementCollector(
                    doc, REVITCOMPAT.NewId(scope.ViewId));
            }

            return new FilteredElementCollector(doc);
        }

        public static QACONTEXT BuildContext(
            Document doc, QASCOPE scope, View activeView)
        {
            var context = new QACONTEXT { Scope = scope };

            // Nothing to gather for a model-wide run: the checks that
            // ask for it walk the model themselves
            if (scope.What == QASCOPE.Kind.WholeModel
                || scope.What == QASCOPE.Kind.PickedLevels)
                return context;

            if (scope.What == QASCOPE.Kind.ActiveView)
            {
                if (activeView != null && !activeView.IsTemplate)
                {
                    context.Views.Add(activeView);

                    // The model-wide pages need the view ITSELF, not a
                    // list of views: they collect pipe and duct straight
                    // out of the document and have to be told to collect
                    // it out of the view instead. Nothing set this
                    // before, so "the view open now" quietly meant the
                    // whole project on the CHW and duct pages.
                    //
                    // Not for a SHEET, though. Collecting from a sheet
                    // gives its viewports and its title block - no pipe
                    // is "in" a sheet - so it would find nothing at all
                    // and read as a model with no pipework in it. A
                    // sheet keeps the old behaviour of the whole model,
                    // which is at least a truthful answer.
                    if (!(activeView is ViewSheet))
                        scope.ViewId = activeView.Id.ToValue();
                }

                return context;
            }

            foreach (ViewSheet sheet in AllSheets(doc))
            {
                if (scope.What == QASCOPE.Kind.PickedSheets &&
                    !scope.SheetIds.Contains(sheet.Id.ToValue()))
                    continue;

                context.Sheets.Add(sheet);

                foreach (ElementId viewId in sheet.GetAllPlacedViews())
                {
                    var view = doc.GetElement(viewId) as View;

                    if (view == null || view.IsTemplate)
                        continue;

                    context.Views.Add(view);

                    context.SheetOfView[viewId.ToValue()] = sheet;
                }
            }

            return context;
        }

        // ==================================================
        // Running
        // ==================================================

        public static List<IQACHECK> InGroup(string group)
        {
            var wanted = new List<IQACHECK>();

            foreach (IQACHECK check in All())
            {
                if (string.Equals(check.Group, group,
                        StringComparison.OrdinalIgnoreCase))
                    wanted.Add(check);
            }

            return wanted;
        }

        public static List<QAFinding> Run(
            Document doc,
            QACONTEXT context,
            IEnumerable<IQACHECK> checks,
            QASETTINGS settings)
        {
            var findings = new List<QAFinding>();

            foreach (IQACHECK check in checks)
            {
                try
                {
                    List<QAFinding> found = check.Run(
                        doc, context, settings ?? new QASETTINGS());

                    if (found != null)
                        findings.AddRange(found);
                }
                catch (Exception ex)
                {
                    // One broken check must not lose the others' results
                    findings.Add(new QAFinding
                    {
                        Check = check.Name,
                        Severity = QASeverity.Warning,
                        Problem = "this check failed to run: " + ex.Message
                    });
                }
            }

            // Sheet first: a QA list is worked through one drawing at a
            // time, so everything for a sheet belongs together. Severity
            // then orders the rows within each sheet.
            findings.Sort((a, b) =>
            {
                int bySheet = CompareSheetNumbers(a.SheetNumber, b.SheetNumber);

                if (bySheet != 0)
                    return bySheet;

                int bySeverity = a.Severity.CompareTo(b.Severity);

                if (bySeverity != 0)
                    return bySeverity;

                int byView = string.Compare(
                    a.ViewName, b.ViewName,
                    StringComparison.OrdinalIgnoreCase);

                if (byView != 0)
                    return byView;

                int byCheck = string.Compare(
                    a.Check, b.Check, StringComparison.OrdinalIgnoreCase);

                return byCheck != 0
                    ? byCheck
                    : string.Compare(a.Element, b.Element,
                        StringComparison.OrdinalIgnoreCase);
            });

            return findings;
        }

        // ==================================================
        // Shared helpers for checks
        // ==================================================

        // Sheet numbers sorted the way a person reads them: M-9 before
        // M-10, which plain text ordering gets backwards. Runs of digits
        // are compared as numbers, everything else letter by letter.
        // A finding with no sheet sorts last - it belongs to no drawing.
        public static int CompareSheetNumbers(string a, string b)
        {
            bool emptyA = string.IsNullOrEmpty(a);
            bool emptyB = string.IsNullOrEmpty(b);

            if (emptyA || emptyB)
            {
                if (emptyA && emptyB)
                    return 0;

                return emptyA ? 1 : -1;
            }

            int i = 0;
            int j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int startA = i;
                    int startB = j;

                    while (i < a.Length && char.IsDigit(a[i]))
                        i++;

                    while (j < b.Length && char.IsDigit(b[j]))
                        j++;

                    // Leading zeros do not make a number bigger
                    string numberA =
                        a.Substring(startA, i - startA).TrimStart('0');
                    string numberB =
                        b.Substring(startB, j - startB).TrimStart('0');

                    if (numberA.Length != numberB.Length)
                        return numberA.Length - numberB.Length;

                    int byDigits = string.CompareOrdinal(numberA, numberB);

                    if (byDigits != 0)
                        return byDigits;
                }
                else
                {
                    int byChar = char.ToUpperInvariant(a[i])
                        .CompareTo(char.ToUpperInvariant(b[j]));

                    if (byChar != 0)
                        return byChar;

                    i++;
                    j++;
                }
            }

            return (a.Length - i) - (b.Length - j);
        }

        // Every tag drawn in the given view
        public static List<IndependentTag> TagsIn(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();
        }

        public static string DescribeElement(Element element)
        {
            if (element == null)
                return "(missing element)";

            string category = element.Category == null
                ? string.Empty
                : element.Category.Name;

            var instance = element as FamilyInstance;

            if (instance != null && instance.Symbol != null)
            {
                string family = instance.Symbol.Family == null
                    ? string.Empty
                    : instance.Symbol.Family.Name;

                return category + ": " + family + " : " + instance.Symbol.Name;
            }

            ElementId typeId = element.GetTypeId();

            if (typeId != ElementId.InvalidElementId)
            {
                Element type = element.Document.GetElement(typeId);

                if (type != null && !string.IsNullOrEmpty(type.Name))
                    return category + ": " + type.Name;
            }

            return string.IsNullOrEmpty(category)
                ? "Id " + element.Id.ToValue()
                : category + " id " + element.Id.ToValue();
        }
    }
}
