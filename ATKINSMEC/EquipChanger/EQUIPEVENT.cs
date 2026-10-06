using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // EQUIPForm is MODELESS, so the model stays usable while it is open
    // and the selection can be changed without closing anything.
    // Everything that touches the API goes through here - calling Revit
    // straight from a modeless form is a latent crash, and this suite
    // has hit that for real in SHEET QA.
    public class EQUIPEVENT : IExternalEventHandler
    {
        public enum Request
        {
            None,
            Read,
            Look,
            Apply,
            Zoom
        }

        public Request Action { get; set; }

        public EQUIPForm Form { get; set; }

        // Inputs from the window
        public bool WholeCategory { get; set; }
        public bool ResizeDucts { get; set; }
        public long TypeId { get; set; }
        public long ZoomTo { get; set; }

        // What the last read found, kept so Look and Apply act on the
        // same elements the window is showing rather than on whatever
        // happens to be selected at that moment
        public List<long> Sources = new List<long>();

        public void Execute(UIApplication app)
        {
            try
            {
                switch (Action)
                {
                    case Request.Read:
                        DoRead(app);
                        break;

                    case Request.Look:
                        DoLook(app);
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
                    Form.Trouble(ex.Message);
            }
            finally
            {
                Action = Request.None;
            }
        }

        public string GetName()
        {
            return "ATKINSMEC equipment changer";
        }

        // ==================================================
        // What is selected now
        // ==================================================

        private void DoRead(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
                return;

            Document doc = uiDoc.Document;

            Sources = EQUIPCommand.Wanted(
                doc, uiDoc.Selection.GetElementIds());

            var picture = new EQUIPPICTURE();

            foreach (long id in Sources)
            {
                Element element = doc.GetElement(REVITCOMPAT.NewId(id));

                if (element == null)
                    continue;

                picture.Rows.Add(new EQUIPROW
                {
                    Id = id,
                    Name = EQUIPCHANGE.Named(element),
                    Category = element.Category == null
                        ? string.Empty
                        : element.Category.Name,
                    TypeNow = EQUIPCHANGE.TypeNameOf(element),
                    Connectors = EQUIPWALK.EndsOf(element).Count,
                    Follows = "(press Look)"
                });
            }

            var already = new HashSet<long>();

            foreach (long id in Sources)
            {
                var instance = doc.GetElement(
                    REVITCOMPAT.NewId(id)) as FamilyInstance;

                if (instance != null && instance.Symbol != null)
                    already.Add(instance.Symbol.Id.ToValue());
            }

            foreach (FamilySymbol symbol in EQUIPTYPES.For(
                         doc, Sources, WholeCategory))
            {
                picture.Types.Add(new EQUIPTYPEROW
                {
                    Id = symbol.Id.ToValue(),
                    Label = EQUIPTYPES.Label(symbol),
                    Current = already.Contains(symbol.Id.ToValue())
                });
            }

            picture.Said = Sources.Count == 0
                ? "Nothing suitable is selected. Pick mechanical " +
                  "equipment or air terminals in the model, then press " +
                  "Read the selection."
                : Sources.Count + " element(s) read from the selection.";

            if (Form != null && !Form.IsDisposed)
                Form.Picture(picture);
        }

        // ==================================================
        // What would follow
        // ==================================================

        private void DoLook(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
                return;

            Document doc = uiDoc.Document;

            var rows = new List<EQUIPROW>();

            int follows = 0;

            foreach (long id in Sources)
            {
                Element element = doc.GetElement(REVITCOMPAT.NewId(id));

                if (element == null)
                    continue;

                EQUIPCHANGE.Outcome outcome =
                    EQUIPCHANGE.Preview(doc, element, null);

                follows += outcome.Resized;

                rows.Add(new EQUIPROW
                {
                    Id = id,
                    Name = outcome.Name,
                    Category = element.Category == null
                        ? string.Empty
                        : element.Category.Name,
                    TypeNow = outcome.WasType,
                    Connectors = EQUIPWALK.EndsOf(element).Count,
                    Follows = string.IsNullOrEmpty(outcome.Said)
                        ? string.Join("   |   ", outcome.Branches.ToArray())
                        : outcome.Said,
                    Detail = string.Join(
                        Environment.NewLine, outcome.Branches.ToArray())
                });
            }

            if (Form != null && !Form.IsDisposed)
            {
                Form.Looked(
                    rows,
                    follows + " duct(s), fitting(s) and accessory(ies) " +
                    "would follow the change.");
            }
        }

        // ==================================================
        // Doing it
        // ==================================================

        private void DoApply(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
                return;

            EQUIPCHANGE.Result result = EQUIPCHANGE.Run(
                uiDoc.Document, Sources, TypeId, ResizeDucts);

            if (Form != null && !Form.IsDisposed)
                Form.Applied(result);
        }

        // ==================================================
        // Showing one in the view - worth having now the window is
        // modeless and the model is right there
        // ==================================================

        private void DoZoom(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || ZoomTo == 0)
                return;

            try
            {
                ElementId id = REVITCOMPAT.NewId(ZoomTo);

                uiDoc.Selection.SetElementIds(new List<ElementId> { id });

                uiDoc.ShowElements(id);
            }
            catch
            {
                // ShowElements throws when the element is in no open
                // view, which is not worth an error
            }
        }
    }
}
