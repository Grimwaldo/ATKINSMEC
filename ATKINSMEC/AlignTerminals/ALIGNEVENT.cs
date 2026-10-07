using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // ALIGNForm is modeless, so the model stays usable while it is open
    // and a terminal can be shown in the view from the list. Everything
    // that touches the API goes through here - calling Revit straight
    // from a modeless form is a latent crash.
    public class ALIGNEVENT : IExternalEventHandler
    {
        public enum Request
        {
            None,
            Refresh,
            Find,
            Pick,
            Apply,
            Zoom
        }

        public Request Action { get; set; }

        // The model the window was opened on, set by the command that
        // opened it: nothing is done in any other (SAMEDOC)
        public Document Home { get; set; }
        public string HomeTitle { get; set; }

        public ALIGNForm Form { get; set; }

        // Inputs
        public ALIGNTERMINALS.Options Options { get; set; }
        public List<ALIGNTERMINALS.Match> ToApply { get; set; }
        public long ZoomTo { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                // NOT IN SOME OTHER MODEL (SAMEDOC): the window's ids belong
                // to its own model, and in another one they are other
                // elements or nothing
                string wrong = Action == Request.None
                    ? null
                    : SAMEDOC.Problem(app, Home, HomeTitle);

                if (wrong != null)
                {
                    if (Form != null && !Form.IsDisposed)
                        Form.ReportError(wrong);

                    return;
                }

                switch (Action)
                {
                    case Request.Refresh:
                        DoRefresh(app);
                        break;

                    case Request.Find:
                        DoFind(app);
                        break;

                    case Request.Pick:
                        DoPick(app);
                        break;

                    case Request.Apply:
                        DoApply(app);
                        break;

                    case Request.Zoom:
                        DoZoom(app);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (Form != null && !Form.IsDisposed)
                    Form.ReportError(ex.Message);
            }
            finally
            {
                Action = Request.None;
            }
        }

        public string GetName()
        {
            return "ATKINSMEC align terminals";
        }

        // ==================================================

        private Document Doc(UIApplication app)
        {
            return app.ActiveUIDocument == null
                ? null
                : app.ActiveUIDocument.Document;
        }

        private void DoRefresh(UIApplication app)
        {
            Document doc = Doc(app);

            if (doc == null || Form == null)
                return;

            Form.ShowLinks(
                RVTLINKSELECTION.GetLinks(doc),
                ALIGNTERMINALS.CountInModel(doc),
                CurrentTerminals(app));
        }

        private void DoFind(UIApplication app)
        {
            Document doc = Doc(app);

            if (doc == null || Form == null || Options == null)
                return;

            Form.ShowMatches(
                ALIGNTERMINALS.Plan(doc, Options),
                ALIGNTERMINALS.CountInLinks(doc, Options));
        }

        // Picking is natural now the window is modeless: no closing and
        // reopening, the user just clicks in the model
        private void DoPick(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null)
                return;

            try
            {
                IList<Reference> references = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new ALIGNSELECTIONFILTER(),
                    "Pick the air terminals to align, then Finish");

                var chosen = new HashSet<long>();

                foreach (Reference reference in references)
                    chosen.Add(reference.ElementId.ToValue());

                Form.SetPicked(chosen);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Escape - leave the previous selection alone
            }
        }

        private void DoApply(UIApplication app)
        {
            Document doc = Doc(app);

            if (doc == null || Form == null || ToApply == null)
                return;

            int moved;

            // One transaction, so the whole alignment is a single undo
            // step rather than hundreds
            using (var transaction = new Transaction(
                       doc, "Align air terminals to link"))
            {
                transaction.Start();

                moved = ALIGNTERMINALS.Apply(doc, ToApply);

                transaction.Commit();
            }

            Form.Applied(moved, ToApply.Count);
        }

        private void DoZoom(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null)
                return;

            ElementId id = REVITCOMPAT.NewId(ZoomTo);

            Element element = uiDoc.Document.GetElement(id);

            if (element == null)
                return;

            // Selected as well as shown, so it is obvious which one
            uiDoc.Selection.SetElementIds(new List<ElementId> { id });

            uiDoc.ShowElements(id);

            BoundingBoxXYZ box = element.get_BoundingBox(null);

            if (box == null)
                return;

            // ShowElements frames it tightly; pull back for some context
            XYZ margin = new XYZ(2, 2, 0);

            foreach (UIView view in uiDoc.GetOpenUIViews())
            {
                if (view.ViewId != uiDoc.ActiveView.Id)
                    continue;

                view.ZoomAndCenterRectangle(
                    box.Min - margin,
                    box.Max + margin);

                break;
            }
        }

        // Whatever air terminals happen to be selected right now
        private static HashSet<long> CurrentTerminals(UIApplication app)
        {
            var terminals = new HashSet<long>();

            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null)
                return terminals;

            Document doc = uiDoc.Document;

            foreach (ElementId id in uiDoc.Selection.GetElementIds())
            {
                Element element = doc.GetElement(id);

                if (element == null || element.Category == null)
                    continue;

                if (element.Category.Id.ToValue() ==
                    (long)BuiltInCategory.OST_DuctTerminal)
                    terminals.Add(id.ToValue());
            }

            return terminals;
        }
    }
}
