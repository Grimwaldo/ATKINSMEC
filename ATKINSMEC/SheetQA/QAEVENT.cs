using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // QAForm is modeless, so a finding can be shown in the model while
    // the list stays open. Everything touching the API goes through here.
    //
    // Running the checks only reads. Marking the findings on the
    // drawings writes - one transaction, one undo step - and is the only
    // thing here that does.
    public class QAEVENT : IExternalEventHandler
    {
        public enum Request
        {
            None,
            Refresh,
            Run,
            Zoom,
            Mark,
            Clear,
            Paint,
            Unpaint,
            Repaint,
            FixGrids,
            FixDucts,
            FixFcus,
            FixChw,
            FixChwJoints,

            // Looked at first, acted on second: the question cannot be
            // asked without knowing how much is there
            SurveyChwFlow,
            ClearChwFlow
        }

        public Request Action { get; set; }

        // The model the window was opened on, set by the command that
        // opened it: nothing is done in any other (SAMEDOC)
        public Document Home { get; set; }
        public string HomeTitle { get; set; }

        public QAForm Form { get; set; }

        // Inputs
        public QASCOPE Scope { get; set; }
        public List<IQACHECK> Checks { get; set; }
        public QASETTINGS Settings { get; set; }
        public List<QAFinding> ToFix { get; set; }
        public long ZoomTo { get; set; }
        public List<QAFinding> ToMark { get; set; }
        public List<QAMARK.Spot> ToClear { get; set; }

        // The scope the markers were painted with, so clearing and
        // repainting do not depend on the window still being there
        private QASCOPE _painted;

        public void Execute(UIApplication app)
        {
            try
            {
                // NOT IN SOME OTHER MODEL (SAMEDOC). The live markers repaint
                // on their own whenever a model changes, so a repaint that
                // finds another model in front just waits - a message every
                // time would be unbearable
                string wrong = Action == Request.None
                    ? null
                    : SAMEDOC.Problem(app, Home, HomeTitle);

                if (wrong != null)
                {
                    if (Action != Request.Repaint && Form != null && !Form.IsDisposed)
                        Form.ReportError(wrong);

                    return;
                }

                switch (Action)
                {
                    case Request.Refresh:
                        DoRefresh(app);
                        break;

                    case Request.Run:
                        DoRun(app);
                        break;

                    case Request.Zoom:
                        DoZoom(app);
                        break;

                    case Request.Mark:
                        DoMark(app);
                        break;

                    case Request.Clear:
                        DoClear(app);
                        break;

                    case Request.Paint:
                        DoPaint(app, true);
                        break;

                    case Request.Unpaint:
                        DoPaint(app, false);
                        break;

                    case Request.Repaint:
                        DoRepaint(app);
                        break;

                    case Request.FixGrids:
                        DoFixGrids(app);
                        break;

                    case Request.FixDucts:
                        DoFixDucts(app);
                        break;

                    case Request.FixChwJoints:
                        DoFixChwJoints(app);
                        break;

                    case Request.FixFcus:
                        DoFixFcus(app);
                        break;

                    case Request.FixChw:
                        DoFixChw(app);
                        break;

                    case Request.SurveyChwFlow:
                        DoSurveyChwFlow(app);
                        break;

                    case Request.ClearChwFlow:
                        DoClearChwFlow(app);
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
            return "Atkins MEP Toolkit - sheet QA";
        }

        // ==================================================

        private void DoRefresh(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null)
                return;

            var sheets = new List<QAForm.SheetItem>();

            foreach (ViewSheet sheet in QACHECKS.AllSheets(uiDoc.Document))
            {
                sheets.Add(new QAForm.SheetItem
                {
                    Id = sheet.Id.ToValue(),
                    Number = sheet.SheetNumber,
                    Name = sheet.Name
                });
            }

            var levels = new List<QAForm.LevelItem>();

            var collector = new FilteredElementCollector(uiDoc.Document)
                .OfClass(typeof(Level));

            foreach (Element element in collector)
            {
                var level = element as Level;

                if (level == null)
                    continue;

                levels.Add(new QAForm.LevelItem
                {
                    Id = level.Id.ToValue(),
                    Name = level.Name,
                    Metres = UnitUtils.ConvertFromInternalUnits(
                        level.Elevation, UnitTypeId.Meters)
                });
            }

            levels.Sort((a, b) => a.Metres.CompareTo(b.Metres));

            string activeView = uiDoc.ActiveView == null
                ? string.Empty
                : uiDoc.ActiveView.Name;

            Form.ShowScope(
                sheets,
                levels,
                activeView,
                RVTLINKSELECTION.GetLinks(uiDoc.Document));

            // Rides along with the refresh rather than being its own
            // request: an ExternalEvent raised twice in a row runs once,
            // and the second Action would simply overwrite the first
            DoFcuParameters(app);

            Form.ShowChwSystemTypes(
                QACHW.SystemTypes(uiDoc.Document));

            Form.ShowChwEquipFamilies(
                QAEQUIP.Families(uiDoc.Document));
        }

        private void DoRun(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || Scope == null)
                return;

            Document doc = uiDoc.Document;

            QACONTEXT context = QACHECKS.BuildContext(
                doc, Scope, uiDoc.ActiveView);

            List<QAFinding> findings = QACHECKS.Run(doc, context, Checks, Settings);

            Form.ShowFindings(findings, context.Views.Count,
                context.Sheets.Count);
        }

        // The other things in this add-in that write to the model.
        // Each is one transaction, so one Ctrl+Z undoes the lot.
        private void DoMark(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToMark == null)
                return;

            Document doc = uiDoc.Document;

            QAMARK.Result result;

            using (var transaction = new Transaction(doc, "Mark QA findings"))
            {
                transaction.Start();

                result = QAMARK.Mark(doc, ToMark);

                transaction.Commit();
            }

            Form.Marked(result);
        }

        private void DoClear(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToClear == null)
                return;

            Document doc = uiDoc.Document;

            int cleared;

            using (var transaction = new Transaction(doc, "Clear QA marks"))
            {
                transaction.Start();

                cleared = QAMARK.Clear(doc, ToClear);

                transaction.Commit();
            }

            Form.Cleared(cleared);
        }

        // Analysis Visualisation: markers floated over the findings,
        // touching neither the elements nor the view's filters
        private void DoPaint(UIApplication app, bool on)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null)
                return;

            // Clearing falls back to whatever was painted, so it still
            // works when the window has gone and Scope is stale
            QASCOPE scope = on ? Scope : (_painted ?? Scope);

            if (scope == null)
                return;

            Document doc = uiDoc.Document;

            QACONTEXT context = QACHECKS.BuildContext(
                doc, scope, uiDoc.ActiveView);

            if (!on)
            {
                int cleared;

                QAWATCH.Painting = true;

                try
                {
                    using (var transaction = new Transaction(
                               doc, QAWATCH.ClearTransaction))
                    {
                        transaction.Start();

                        cleared = QAAVF.Clear(doc, context.Views);

                        transaction.Commit();
                    }
                }
                finally
                {
                    QAWATCH.Painting = false;
                }

                // Unsubscribing is an API call; here is where it is
                // allowed to happen
                QAWATCH.Stop();

                _painted = null;

                if (Form != null && !Form.IsDisposed)
                    Form.Painted(null, cleared);

                return;
            }

            // Subscribing is an API call too. It happens here rather
            // than in the form's checkbox handler, which is what threw
            // "Revit is currently not within an API context".
            QAWATCH.Start(app.Application, this, QACommand.Event);

            _painted = scope;

            List<QAFinding> findings = QACHECKS.Run(
                doc, context, Checks ?? QACHECKS.All(), Settings);

            QAAVF.Result result = PaintNow(doc, findings);

            if (Form != null && !Form.IsDisposed)
                Form.Painted(result, 0);
        }

        // The repaint the watcher asks for when the model changes
        private void DoRepaint(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            QASCOPE scope = _painted ?? Scope;

            if (uiDoc == null || scope == null)
                return;

            Document doc = uiDoc.Document;

            QACONTEXT context = QACHECKS.BuildContext(
                doc, scope, uiDoc.ActiveView);

            List<QAFinding> findings = QACHECKS.Run(
                doc, context, Checks ?? QACHECKS.All(), Settings);

            PaintNow(doc, findings);

            // The list is refreshed too, so the window agrees with the
            // markers rather than showing a stale count
            if (Form != null && !Form.IsDisposed)
            {
                Form.ShowFindings(
                    findings, context.Views.Count, context.Sheets.Count);
            }
        }

        private QAAVF.Result PaintNow(Document doc, List<QAFinding> findings)
        {
            QAAVF.Result result;

            // Our own transaction must not set the watcher off again
            QAWATCH.Painting = true;

            try
            {
                using (var transaction = new Transaction(
                           doc, QAWATCH.PaintTransaction))
                {
                    transaction.Start();

                    result = QAAVF.Paint(doc, findings);

                    transaction.Commit();
                }
            }
            finally
            {
                QAWATCH.Painting = false;
            }

            return result;
        }

        // Putting the grids right - the one thing here that changes
        // the drawing rather than the way it is coloured
        private void DoFixGrids(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToFix == null)
                return;

            Document doc = uiDoc.Document;

            QAGRIDFIX.Result result;

            using (var transaction = new Transaction(doc, "Fix grids"))
            {
                transaction.Start();

                result = QAGRIDFIX.Fix(
                    doc, ToFix, Settings ?? new QASETTINGS());

                transaction.Commit();
            }

            // Straight back through the checks, so the list shows what
            // is still wrong rather than what was. It happens inline
            // rather than by raising another request: Execute clears
            // Action on its way out, and a request raised from in here
            // would arrive with nothing to do.
            if (Scope != null && Checks != null)
                DoRun(app);

            Form.GridsFixed(result);
        }

        // Joining the duct network back up. The only thing in the
        // add-in that creates and deletes elements, which is why each
        // finding gets its own SubTransaction inside QADUCTFIX.
        private void DoFixDucts(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToFix == null)
                return;

            Document doc = uiDoc.Document;

            QADUCTFIX.Result result;

            using (var transaction = new Transaction(doc, "Join up ducts"))
            {
                transaction.Start();

                result = QADUCTFIX.Fix(
                    doc, ToFix, Settings ?? new QASETTINGS(), Scope);

                transaction.Commit();
            }

            // Straight back through the checks, for the same reason as
            // the grids: the list should say what is still wrong. Inline
            // rather than by raising another request - Execute clears
            // Action on its way out.
            if (Scope != null && Checks != null)
                DoRun(app);

            Form.DuctsFixed(result);
        }

        // Bringing chilled water pipework back onto what it serves.
        // The units are never moved; the pipes are.
        private void DoFixChwJoints(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToFix == null)
                return;

            Document doc = uiDoc.Document;

            QACHWJOINTFIX.Result result;

            using (var transaction = new Transaction(
                       doc, "Reconnect chilled water pipework"))
            {
                transaction.Start();

                result = QACHWJOINTFIX.Fix(
                    doc, ToFix, Settings ?? new QASETTINGS());

                transaction.Commit();
            }

            // Straight back through the checks, so the list says what
            // is STILL wrong rather than what was wrong before. Inline
            // rather than by raising another request - Execute clears
            // Action on its way out, so a request raised from in here
            // would arrive with nothing to do.
            if (Scope != null && Checks != null)
                DoRun(app);

            Form.ChwJointsFixed(result);
        }

        // Scaling air terminals so an FCU carries what the schedule
        // says. Writes only to the terminals - the duct follows.
        private void DoFixFcus(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToFix == null)
                return;

            Document doc = uiDoc.Document;

            QAFCUFIX.Result result;

            using (var transaction = new Transaction(
                       doc, "Correct FCU airflows"))
            {
                transaction.Start();

                result = QAFCUFIX.Fix(
                    doc, ToFix, Settings ?? new QASETTINGS(), Scope);

                transaction.Commit();
            }

            if (Scope != null && Checks != null)
                DoRun(app);

            Form.FcusFixed(result);
        }

        private void DoFcuParameters(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null)
                return;

            Form.ShowFcuParameters(
                QAFCU.ParameterNames(uiDoc.Document));
        }

        // Resizing chilled water pipe. Writes the PIPES; the fittings
        // follow when the document regenerates.
        private void DoFixChw(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null || ToFix == null)
                return;

            Document doc = uiDoc.Document;

            QACHWFIX.Result result;

            using (var transaction = new Transaction(
                       doc, "Resize chilled water pipe"))
            {
                transaction.Start();

                result = QACHWFIX.Fix(
                    doc, ToFix, Settings ?? new QASETTINGS(), Scope);

                transaction.Commit();
            }

            if (Scope != null && Checks != null)
                DoRun(app);

            Form.ChwFixed(result);
        }

        // Reads only - what is there, so the window can ask a
        // question with numbers in it
        private void DoSurveyChwFlow(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null)
                return;

            Form.ChwFlowSurveyed(
                QACHWFLOW.Survey(uiDoc.Document, Settings));
        }

        // Taking the Additional Flow off chilled water pipe. One
        // parameter each, so one transaction covers the lot.
        private void DoClearChwFlow(UIApplication app)
        {
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || Form == null)
                return;

            Document doc = uiDoc.Document;

            QACHWFLOW.Result result;

            using (var transaction = new Transaction(
                       doc, "Clear additional flow"))
            {
                transaction.Start();

                result = QACHWFLOW.Clear(doc, Settings);

                transaction.Commit();
            }

            // The flows have changed, so anything the list said about
            // sizes is now out of date
            if (Scope != null && Checks != null)
                DoRun(app);

            Form.ChwFlowCleared(result);
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

            uiDoc.Selection.SetElementIds(new List<ElementId> { id });

            uiDoc.ShowElements(id);

            BoundingBoxXYZ box = element.get_BoundingBox(null);

            if (box == null)
                return;

            // ShowElements frames a tag far too tightly to place it
            XYZ margin = new XYZ(4, 4, 0);

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
    }
}
