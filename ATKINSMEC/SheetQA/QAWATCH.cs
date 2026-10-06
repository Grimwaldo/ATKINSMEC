using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // What makes the AVF markers live.
    //
    // AVF does not recompute itself the way a view filter does, so
    // something has to notice the model changing and push new values.
    // This listens to DocumentChanged and asks QAEVENT to repaint.
    //
    // DocumentChanged is read-only - nothing may be written from inside
    // it - so all it does is raise the external event, which does the
    // work in a context that is allowed to.
    public class QAWATCH
    {
        // The transaction names this add-in uses when painting. A change
        // made by us must not start another repaint, or the two chase
        // each other forever.
        public const string PaintTransaction = "Show zero flow markers";
        public const string ClearTransaction = "Clear zero flow markers";

        private static Application _app;
        private static ExternalEvent _event;
        private static QAEVENT _handler;

        private static bool _on;

        // Subscribing and unsubscribing are themselves Revit API calls,
        // so Start and Stop may only be called from inside an API
        // context - that is, from QAEVENT, never from a form's own
        // event handler. Disarm touches no API and can be called from
        // anywhere, which is what window closing needs.
        private static bool _armed = true;

        // Set while our own paint is running, as a second guard on top
        // of the transaction name
        public static bool Painting { get; set; }

        public static bool IsOn
        {
            get { return _on; }
        }

        // MUST be called from inside a Revit API context
        public static void Start(
            Application app, QAEVENT handler, ExternalEvent externalEvent)
        {
            _armed = true;

            if (_on)
                return;

            _app = app;
            _handler = handler;
            _event = externalEvent;

            _app.DocumentChanged += OnChanged;

            _on = true;
        }

        // MUST be called from inside a Revit API context
        public static void Stop()
        {
            _armed = false;

            if (!_on)
                return;

            try
            {
                if (_app != null)
                    _app.DocumentChanged -= OnChanged;
            }
            catch
            {
                // Revit going down underneath us
            }

            _on = false;

            _app = null;
            _handler = null;
            _event = null;
        }

        // Safe from anywhere - no API call. Silences the watcher when
        // there is no window left to talk to; the unsubscribe itself
        // happens later, from a context that is allowed to make it.
        public static void Disarm()
        {
            _armed = false;
        }

        private static void OnChanged(
            object sender, DocumentChangedEventArgs e)
        {
            if (!_on || !_armed || Painting)
                return;

            try
            {
                if (IsOurOwnChange(e))
                    return;

                if (!TouchesFlow(e))
                    return;

                // Read only in here. All this does is ask for a repaint.
                _handler.Action = QAEVENT.Request.Repaint;

                _event.Raise();
            }
            catch
            {
                // An event handler that throws would be a nuisance in
                // the middle of somebody's edit
            }
        }

        private static bool IsOurOwnChange(DocumentChangedEventArgs e)
        {
            foreach (string name in e.GetTransactionNames())
            {
                if (name == PaintTransaction || name == ClearTransaction)
                    return true;
            }

            return false;
        }

        // Repainting after every nudge of a wall would be wasteful, so
        // only changes that could alter a flow reading count
        private static bool TouchesFlow(DocumentChangedEventArgs e)
        {
            Document doc = e.GetDocument();

            if (doc == null)
                return false;

            var ids = new List<ElementId>();

            ids.AddRange(e.GetModifiedElementIds());
            ids.AddRange(e.GetAddedElementIds());

            // A deleted element cannot be looked at, but it may well
            // have been a tag or a terminal, so a deletion always counts
            if (e.GetDeletedElementIds().Count > 0)
                return true;

            foreach (ElementId id in ids)
            {
                Element element = doc.GetElement(id);

                if (element == null || element.Category == null)
                    continue;

                long category = element.Category.Id.ToValue();

                if (IsFlowCategory(category) || IsTagCategory(category))
                    return true;

                // A system being recalculated is the usual reason a flow
                // changes without anyone touching the terminal
                if (element is MEPSystem)
                    return true;
            }

            return false;
        }

        private static bool IsFlowCategory(long category)
        {
            return category == (long)BuiltInCategory.OST_DuctTerminal
                || category == (long)BuiltInCategory.OST_DuctCurves
                || category == (long)BuiltInCategory.OST_FlexDuctCurves
                || category == (long)BuiltInCategory.OST_DuctFitting
                || category == (long)BuiltInCategory.OST_DuctAccessory
                || category == (long)BuiltInCategory.OST_PipeCurves
                || category == (long)BuiltInCategory.OST_FlexPipeCurves
                || category == (long)BuiltInCategory.OST_PipeFitting
                || category == (long)BuiltInCategory.OST_PipeAccessory;
        }

        private static bool IsTagCategory(long category)
        {
            return category == (long)BuiltInCategory.OST_DuctTerminalTags
                || category == (long)BuiltInCategory.OST_DuctTags
                || category == (long)BuiltInCategory.OST_PipeTags
                || category == (long)BuiltInCategory.OST_DuctFittingTags
                || category == (long)BuiltInCategory.OST_PipeFittingTags
                || category == (long)BuiltInCategory.OST_DuctAccessoryTags
                || category == (long)BuiltInCategory.OST_PipeAccessoryTags;
        }
    }
}
