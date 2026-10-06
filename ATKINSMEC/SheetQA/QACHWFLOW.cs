using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Clearing the Additional Flow parameter off chilled water pipe.
    //
    // "Additional Flow" is Revit's way of adding flow to a pipe segment
    // on top of what the system calculates. It is useful while sizing
    // by hand and a menace afterwards: it is invisible on the drawing,
    // it survives every later change, and it inflates what the pipe
    // reports carrying - so the size check reads a flow the system does
    // not actually produce and calls a perfectly good pipe undersized.
    //
    // EVERY PIPE IN THE PROJECT, at the user's request.
    //
    // It used to follow the page - the ticked chilled water system
    // types, inside the page's scope - which reads sensibly and is
    // wrong in practice. Additional Flow is a nuisance wherever it is:
    // a pipe with no system, or on a system nobody ticked, still
    // reports a flow the system never produced, and still makes a
    // later check call a good pipe undersized. Leaving those behind
    // meant running the button, fixing the ticks, and running it
    // again.
    //
    // So this ignores both the scope and the ticked types, and the
    // window says so plainly before anything is written. What it
    // reports separately is how many were OUTSIDE the chilled water
    // systems, so the reach of it is visible rather than assumed.
    public class QACHWFLOW
    {
        private static readonly BuiltInCategory[] Pipes =
        {
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves
        };

        public class Result
        {
            // Pipes that had some and now do not
            public int Cleared { get; set; }

            // Every pipe in the project
            public int Looked { get; set; }

            // Of those, the ones outside the ticked chilled water
            // systems - reported so the reach is visible
            public int Outside { get; set; }

            // Already at nothing
            public int Already { get; set; }

            public int Skipped { get; set; }

            // What was taken off, litres per second, so the size of the
            // change is visible rather than a bare count
            public double LitresRemoved { get; set; }

            public List<string> Notes { get; set; } = new List<string>();

            public void Note(string text)
            {
                if (!Notes.Contains(text))
                    Notes.Add(text);
            }
        }

        // What is there now, without changing anything - so the window
        // can say what pressing the button would do.
        //
        // No scope: this is the whole project either way, and taking a
        // scope it then ignored would be a lie in the signature.
        public static Result Survey(Document doc, QASETTINGS settings)
        {
            return Walk(doc, settings, false);
        }

        // Must be called inside a transaction
        public static Result Clear(Document doc, QASETTINGS settings)
        {
            return Walk(doc, settings, true);
        }

        private static Result Walk(
            Document doc,
            QASETTINGS settings,
            bool write)
        {
            var result = new Result();

            // No guard on the ticked types any more: this is every
            // pipe in the project, whatever system it is on and
            // whether it is on one at all.
            var filter = new ElementMulticategoryFilter(
                new List<BuiltInCategory>(Pipes));

            var collector = new FilteredElementCollector(doc)
                .WherePasses(filter)
                .WhereElementIsNotElementType();

            foreach (Element pipe in collector)
            {
                long systemType = QACHW.SystemTypeOf(pipe, null);

                if (QACHW.SideOf(systemType, settings) == null)
                    result.Outside++;

                result.Looked++;

                One(pipe, write, result);
            }

            return result;
        }

        private static void One(Element pipe, bool write, Result result)
        {
            Parameter extra = pipe.get_Parameter(
                BuiltInParameter.RBS_PIPE_ADDITIONAL_FLOW_PARAM);

            if (extra == null || !extra.HasValue)
            {
                result.Already++;

                return;
            }

            double now;

            try
            {
                now = extra.AsDouble();
            }
            catch
            {
                result.Skipped++;

                return;
            }

            if (Math.Abs(now) < 1e-12)
            {
                result.Already++;

                return;
            }

            double litres = UnitUtils.ConvertFromInternalUnits(
                now, UnitTypeId.LitersPerSecond);

            if (!write)
            {
                result.Cleared++;
                result.LitresRemoved += litres;

                return;
            }

            if (extra.IsReadOnly)
            {
                result.Skipped++;

                result.Note(
                    "Some pipes have a read-only Additional Flow. That " +
                    "is usually a workshared model where somebody else " +
                    "owns the element, or a pipe inside a group.");

                return;
            }

            try
            {
                extra.Set(0.0);

                result.Cleared++;
                result.LitresRemoved += litres;
            }
            catch (Exception ex)
            {
                result.Skipped++;

                result.Note("Some pipes would not take it: " + Short(ex));
            }
        }

        private static string Short(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 140)
                message = message.Substring(0, 137) + "...";

            return message.Length == 0 ? ex.GetType().Name : message;
        }
    }
}
