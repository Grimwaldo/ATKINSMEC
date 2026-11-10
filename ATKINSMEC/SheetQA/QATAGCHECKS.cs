using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The Tags checker page's three checks. One walk measures everything
    // (QATAGS.Of, shared through QACONTEXT); each check reports the pairs
    // of its own kinds. A pair shown in a view AND in its dependents -
    // the same two elements - is one finding, on the first sheet, saying
    // how many other views show it: moving one tag puts all of them right.
    public abstract class QATAGCHECK : IQACHECK
    {
        public string Group
        {
            get { return QAGROUPS.Tags; }
        }

        public abstract string Name { get; }

        public abstract string Explanation { get; }

        // Whether this check reports the pair
        protected abstract bool Wants(QATAGCLASH.Clash clash);

        public List<QAFinding> Run(Document doc, QACONTEXT context, QASETTINGS settings)
        {
            QATAGS.Survey survey = QATAGS.Of(doc, context, settings);

            var findings = new List<QAFinding>();

            var first = new Dictionary<string, QAFinding>();
            var again = new Dictionary<string, int>();

            foreach (QATAGS.Seen seen in survey.Views)
            {
                foreach (QATAGCLASH.Clash clash in seen.Clashes)
                {
                    if (!Wants(clash))
                        continue;

                    string pair = QATAGCLASH.Pair(clash.A.Id, clash.B.Id);

                    if (first.ContainsKey(pair))
                    {
                        again[pair]++;

                        continue;
                    }

                    // A tag before a text note: Show goes to the tag
                    QATAGCLASH.Box one = clash.A;
                    QATAGCLASH.Box other = clash.B;

                    if (one.What == QATAGCLASH.Kind.Text && other.What == QATAGCLASH.Kind.Tag)
                    {
                        one = clash.B;
                        other = clash.A;
                    }

                    string problem = clash.SameElement
                        ? "on top of another tag of the same element: " + LabelOf(seen, other.Id)
                        : "overlaps " + LabelOf(seen, other.Id);

                    if (one.Rough || other.Rough)
                        problem += " (the view is turned: measured roughly)";

                    var finding = new QAFinding
                    {
                        Check = Name,
                        Severity = QASeverity.Issue,
                        SheetNumber = seen.SheetNumber,
                        SheetName = seen.SheetName,
                        ViewName = seen.View.Name,
                        ViewId = seen.View.Id.ToValue(),
                        ElementId = one.Id,
                        OtherElementId = other.Id,
                        Element = LabelOf(seen, one.Id),
                        Problem = problem,
                        Value = clash.Depth.ToString("0.0") + " mm"
                    };

                    first[pair] = finding;
                    again[pair] = 0;

                    findings.Add(finding);
                }
            }

            foreach (KeyValuePair<string, QAFinding> pair in first)
            {
                int more = again[pair.Key];

                if (more > 0)
                    pair.Value.Value += ", also in " + more + " other view" + (more == 1 ? string.Empty : "s");
            }

            if (!survey.Told && (survey.LeadersKept > 0 || survey.Trouble.Length > 0))
            {
                survey.Told = true;

                findings.Add(new QAFinding
                {
                    Check = Name,
                    Severity = QASeverity.Warning,
                    Element = string.Empty,
                    Problem = survey.Trouble.Length > 0
                        ? "tags with leaders were measured leader and all, so they may read as overlapping " +
                          "when only a leader does: " + survey.Trouble
                        : survey.LeadersKept + " tag(s) were measured leader and all - Revit would not set " +
                          "their leader aside - so they may read as overlapping when only a leader does"
                });
            }

            return findings;
        }

        private static string LabelOf(QATAGS.Seen seen, long id)
        {
            string label;

            return seen.Labels.TryGetValue(id, out label) ? label : "id " + id;
        }
    }

    public class QATAGOVERLAPCHECK : QATAGCHECK
    {
        public override string Name
        {
            get { return "Tags overlapping tags"; }
        }

        public override string Explanation
        {
            get
            {
                return "Two tags whose heads overlap in a view on a sheet - room, space and area " +
                       "tags too. Leaders are not counted. One on top of another tag of the " +
                       "same element is said so.";
            }
        }

        protected override bool Wants(QATAGCLASH.Clash clash)
        {
            return clash.A.What == QATAGCLASH.Kind.Tag && clash.B.What == QATAGCLASH.Kind.Tag;
        }
    }

    public class QATAGTEXTCHECK : QATAGCHECK
    {
        public override string Name
        {
            get { return "Tags overlapping text"; }
        }

        public override string Explanation
        {
            get { return "A tag's head overlapping a text note."; }
        }

        protected override bool Wants(QATAGCLASH.Clash clash)
        {
            return clash.A.What != clash.B.What;
        }
    }

    public class QATEXTOVERLAPCHECK : QATAGCHECK
    {
        public override string Name
        {
            get { return "Text overlapping text"; }
        }

        public override string Explanation
        {
            get { return "Two text notes overlapping each other."; }
        }

        protected override bool Wants(QATAGCLASH.Clash clash)
        {
            return clash.A.What == QATAGCLASH.Kind.Text && clash.B.What == QATAGCLASH.Kind.Text;
        }
    }
}
