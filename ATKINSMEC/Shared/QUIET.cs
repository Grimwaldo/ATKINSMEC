using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace ATKINSMEC
{
    // Answers Revit's own dialogs so an unattended batch cannot stall
    // on one.
    //
    // A batch of twenty models opens twenty models, and any one of them
    // can put up "this was made in an earlier version", "there are
    // unresolved references", or a worksharing message. Every one of
    // those waits for a person. Left alone overnight the batch stops at
    // the first and does nothing for eight hours.
    //
    // SILENT IS NOT INVISIBLE. Every dialog answered is written to
    // NWCLOG with its id, its text and the button that was pressed, so
    // a run that quietly skipped something can still be read afterwards.
    // That is the whole difference between suppressing a dialog and
    // hiding a problem.
    //
    // Subscribing to a Revit event is itself an API call - see the note
    // in CLAUDE.md about DocumentChanged. This is only ever started
    // from NWC BATCH, which is a MODAL form running inside the
    // command's API context, so it is safe there. Start and Stop MUST
    // be balanced: a handler left subscribed would answer dialogs for
    // the rest of the Revit session, which is the QAWATCH trap in a
    // more dangerous place.
    public static class QUIET
    {
        private static UIApplication _ui;

        private static Autodesk.Revit.ApplicationServices.Application _app;

        private static int _answered;

        public static bool On
        {
            get { return _ui != null; }
        }

        public static int Answered
        {
            get { return _answered; }
        }

        public static void Start(
            UIApplication ui,
            Autodesk.Revit.ApplicationServices.Application app)
        {
            // Never twice: a second subscription would answer every
            // dialog twice and leave one handler behind on Stop
            if (_ui != null || ui == null)
                return;

            _answered = 0;

            _ui = ui;
            _app = app;

            _ui.DialogBoxShowing += OnDialog;

            if (_app != null)
                _app.FailuresProcessing += OnFailure;

            NWCLOG.Say("silent mode ON - Revit's dialogs will be answered");
        }

        public static void Stop()
        {
            if (_ui == null)
                return;

            try
            {
                _ui.DialogBoxShowing -= OnDialog;
            }
            catch
            {
                // Coming off is best-effort; what matters is that the
                // static is cleared so nothing thinks it is still on
            }

            try
            {
                if (_app != null)
                    _app.FailuresProcessing -= OnFailure;
            }
            catch
            {
            }

            _ui = null;
            _app = null;
        }

        // Which button to press, in order of preference.
        //
        // OverrideResult returns true when the code was accepted, so
        // these are tried until one lands rather than guessed at. The
        // order is deliberate:
        //
        //   1     IDOK          - the plain "yes, carry on"
        //   1001  first custom  - Revit's own wording for carrying on,
        //                         e.g. "Ignore and continue opening the
        //                         project" on the missing-links dialog
        //   8     IDCLOSE       - a dialog that only closes
        //   2     IDCANCEL      - nothing else was offered
        //   6/7   IDYES / IDNO  - last, because these are the ones that
        //                         can mean something destructive
        //
        // Nothing here can be reached by a "save changes?" dialog: the
        // batch closes every document with Close(false), which does not
        // ask.
        private static readonly int[] Buttons = { 1, 1001, 8, 2, 6, 7 };

        private static void OnDialog(
            object sender, DialogBoxShowingEventArgs args)
        {
            string what = Describe(args);

            foreach (int button in Buttons)
            {
                bool took;

                try
                {
                    took = args.OverrideResult(button);
                }
                catch
                {
                    continue;
                }

                if (!took)
                    continue;

                _answered++;

                NWCLOG.Say("   answered a dialog with " + Named(button) +
                           ": " + what);

                return;
            }

            // Nothing was accepted, so the dialog is about to be shown
            // to whoever is there. Worth saying so: this is exactly the
            // case that stalls an unattended run.
            NWCLOG.Say("   COULD NOT ANSWER a dialog, it will be shown: " +
                       what);
        }

        private static string Named(int button)
        {
            switch (button)
            {
                case 1: return "OK";
                case 2: return "Cancel";
                case 6: return "Yes";
                case 7: return "No";
                case 8: return "Close";
                case 1001: return "its first button";
                default: return button.ToString();
            }
        }

        private static string Describe(DialogBoxShowingEventArgs args)
        {
            string id = string.Empty;

            try
            {
                id = args.DialogId ?? string.Empty;
            }
            catch
            {
            }

            string text = string.Empty;

            var task = args as TaskDialogShowingEventArgs;

            if (task != null)
            {
                try
                {
                    text = task.Message ?? string.Empty;
                }
                catch
                {
                }
            }

            var box = args as MessageBoxShowingEventArgs;

            if (box != null)
            {
                try
                {
                    text = box.Message ?? string.Empty;
                }
                catch
                {
                }
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();

            if (text.Length > 200)
                text = text.Substring(0, 200) + "...";

            if (id.Length == 0 && text.Length == 0)
                return "(a dialog that would not say what it was)";

            return (id.Length == 0 ? "" : "[" + id + "] ") + text;
        }

        // Warnings raised while a model is opened. These are not
        // dialogs and DialogBoxShowing never sees them.
        //
        // Only WARNINGS are cleared. An error is left exactly as it is,
        // because resolving one can delete elements - and this add-in
        // is reading models, not repairing them.
        private static void OnFailure(
            object sender, Autodesk.Revit.DB.Events.FailuresProcessingEventArgs args)
        {
            try
            {
                FailuresAccessor failures = args.GetFailuresAccessor();

                if (failures == null)
                    return;

                var warnings = failures.GetFailureMessages();

                if (warnings == null || warnings.Count == 0)
                    return;

                int cleared = 0;

                foreach (FailureMessageAccessor one in warnings)
                {
                    if (one.GetSeverity() != FailureSeverity.Warning)
                        continue;

                    failures.DeleteWarning(one);

                    cleared++;
                }

                if (cleared > 0)
                {
                    _answered += cleared;

                    NWCLOG.Say("   cleared " + cleared + " warning(s)");

                    args.SetProcessingResult(
                        FailureProcessingResult.ProceedWithCommit);
                }
            }
            catch
            {
                // A failure handler that throws would take the open
                // down with it
            }
        }
    }
}
