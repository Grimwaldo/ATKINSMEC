using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Modeless, so a finding can be shown in the model while the list
    // stays up. Nothing here touches the Revit API - every request goes
    // through QAEVENT and comes back through the Show* methods.
    //
    // Two tabs, each a whole page with its own scope, checks and
    // findings. `Page` below is one tab's worth of window, so the code
    // that runs a tab is written once rather than once per tab.
    public partial class QAForm : System.Windows.Forms.Form
    {
        private const int ColShowIndex = 0;

        private const string BubbleTopLeft = "top and left";
        private const string BubbleTopRight = "top and right";
        private const string BubbleBottomLeft = "bottom and left";
        private const string BubbleBottomRight = "bottom and right";

        public class SheetItem
        {
            public long Id { get; set; }
            public string Number { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Number + "   -   " + Name;
            }
        }

        // One tab: its controls, its checks, and what it found. The two
        // pages never have to agree - flowrate over every sheet while
        // the grids are checked on the view open now is a normal thing
        // to want.
        private class Page
        {
            public string Group;

            public List<IQACHECK> Checks = new List<IQACHECK>();
            public List<QAFinding> Findings = new List<QAFinding>();

            public string ScopeDescription = string.Empty;

            public RadioButton All;
            public RadioButton Picked;
            public RadioButton Active;

            // Sheets on two pages, levels on the ducts page - the
            // control is the same, only what fills it differs
            public CheckedListBox Picks;
            public Button TickAll;
            public Button TickNone;

            public CheckedListBox List;
            public Label Info;

            public DataGridView Table;
            public Label Summary;

            // What this page's first two radio buttons mean
            public QASCOPE.Kind AllKind = QASCOPE.Kind.AllSheets;
            public QASCOPE.Kind PickedKind = QASCOPE.Kind.PickedSheets;

            // A duct belongs to the model rather than to a sheet, so
            // that page counts what it found differently, names its
            // columns differently, and cannot mark anything in red -
            // marking needs a view to colour it in
            public bool AboutTheModel;
            public bool CanMark = true;

            public string ThirdColumn = "Sheet";
            public string FourthColumn = "View";

            // How a model-wide page words its own summary. The duct page
            // counts connectors, the FCU page counts units; neither
            // should read as if it were the other.
            public string Everything = "Every sheet in the model.";
            public string Counted = "finding(s)";
            public string GroupedBy = "sheet";
        }

        private Page _flow;
        private Page _grids;
        private Page _ducts;
        private Page _fcu;
        private Page _chw;
        private Page _chwJoints;

        // The sizing table ships with the add-in - see
        // CHWSIZESHEET.Builtin - and that is still what it opens on.
        // It can be exported, edited and imported back, so this is no
        // longer readonly; every lookup in one run still compares
        // against the same rows, because a run reads it once.
        private CHWSIZESHEET.Table _chwTable = CHWSIZESHEET.Builtin();

        // Empty while the built-in table is in use. Kept so the label
        // can say where the numbers came from - a check run against
        // somebody's edited file and one run against the office
        // standard must never look the same on screen.
        private string _chwTableFrom = string.Empty;

        // The schedule, read once when it is chosen. Reading Excel needs
        // no Revit, so it happens right here rather than through QAEVENT.
        private List<FCUSHEET.Unit> _fcuUnits = new List<FCUSHEET.Unit>();
        private string _fcuPath = string.Empty;

        // The page whose run is in flight, so the findings come back to
        // the table that asked for them
        private Page _running;

        public class LevelItem
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public double Metres { get; set; }

            public override string ToString()
            {
                return Name + "   -   " + Metres.ToString("N3") + " m";
            }
        }

        private readonly Document _doc;

        // Read once in the constructor: the constructor runs inside the
        // command's API context, a button handler does not
        private readonly string _docTitle;
        private readonly QAEVENT _handler;
        private readonly ExternalEvent _event;

        // Needed to subscribe the live watcher
        private readonly Autodesk.Revit.ApplicationServices.Application _app;

        // Set while the window is setting its own controls, so their
        // change events do not act on it
        private bool _loadingUi;

        // What this session marked, so clearing is exact rather than a
        // sweep over every override in the model
        private List<QAMARK.Spot> _marks = new List<QAMARK.Spot>();

        public QAForm(
            Document doc,
            Autodesk.Revit.ApplicationServices.Application app,
            QAEVENT handler,
            ExternalEvent externalEvent)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
            _app = app;
            _handler = handler;
            _event = externalEvent;

            _docTitle = doc.Title;

            this.Text = "SHEET QA - " + _docTitle;
        }

        private void QAForm_Load(object sender, EventArgs e)
        {
            _flow = new Page
            {
                Group = QAGROUPS.Flowrate,
                All = rbAllFlow,
                Picked = rbPickedFlow,
                Active = rbActiveFlow,
                Picks = clbSheetsFlow,
                TickAll = btnAllSheetsFlow,
                TickNone = btnNoSheetsFlow,
                List = clbFlow,
                Info = lblFlowInfo,
                Table = dgvFlow,
                Summary = lblSummaryFlow
            };

            _grids = new Page
            {
                Group = QAGROUPS.Grids,
                All = rbAllGrids,
                Picked = rbPickedGrids,
                Active = rbActiveGrids,
                Picks = clbSheetsGrids,
                TickAll = btnAllSheetsGrids,
                TickNone = btnNoSheetsGrids,
                List = clbGrids,
                Info = lblGridsInfo,
                Table = dgvGrids,
                Summary = lblSummaryGrids
            };

            _ducts = new Page
            {
                Group = QAGROUPS.Ducts,
                All = rbAllDucts,
                Picked = rbPickedDucts,
                Active = rbActiveDucts,
                Picks = clbLevelsDucts,
                TickAll = btnAllLevelsDucts,
                TickNone = btnNoLevelsDucts,
                List = clbDucts,
                Info = lblDuctsInfo,
                Table = dgvDucts,
                Summary = lblSummaryDucts,

                AllKind = QASCOPE.Kind.WholeModel,
                PickedKind = QASCOPE.Kind.PickedLevels,
                AboutTheModel = true,
                CanMark = false,
                ThirdColumn = "System",
                FourthColumn = "Kind",
                Everything = "Every duct in the model.",
                Counted = "open connector(s)",
                GroupedBy = "system"
            };

            _chwJoints = new Page
            {
                Group = QAGROUPS.ChwJoints,
                All = rbAllChwJ,
                Picked = rbPickedChwJ,
                Active = rbActiveChwJ,
                Picks = clbLevelsChwJ,
                TickAll = btnAllLevelsChwJ,
                TickNone = btnNoLevelsChwJ,
                List = clbChwJ,
                Info = lblChwJInfo,
                Table = dgvChwJ,
                Summary = lblSummaryChwJ,

                // A pipe belongs to the MODEL, not to a sheet - the
                // same call the duct page makes, and the reason neither
                // can be marked in red
                AllKind = QASCOPE.Kind.WholeModel,
                PickedKind = QASCOPE.Kind.PickedLevels,
                AboutTheModel = true,
                CanMark = false,
                ThirdColumn = "System",
                FourthColumn = "What it is",
                Everything =
                    "Every hydronic pipe in the model, supply and return.",
                Counted = "open end(s)",
                GroupedBy = "system"
            };

            _fcu = new Page
            {
                Group = QAGROUPS.Fcu,
                All = rbAllFcu,
                Picked = rbPickedFcu,
                Active = rbActiveFcu,
                Picks = clbLevelsFcu,
                TickAll = btnAllLevelsFcu,
                TickNone = btnNoLevelsFcu,
                List = clbFcu,
                Info = lblFcuInfo,
                Table = dgvFcu,
                Summary = lblSummaryFcu,

                AllKind = QASCOPE.Kind.WholeModel,
                PickedKind = QASCOPE.Kind.PickedLevels,
                AboutTheModel = true,
                CanMark = false,
                ThirdColumn = "FCU",
                FourthColumn = "Schedule",
                Everything = "Every FCU in the model.",
                Counted = "finding(s)",
                GroupedBy = "unit"
            };

            _chw = new Page
            {
                Group = QAGROUPS.Chw,
                All = rbAllChw,
                Picked = rbPickedChw,
                Active = rbActiveChw,
                Picks = clbLevelsChw,
                TickAll = btnAllLevelsChw,
                TickNone = btnNoLevelsChw,
                List = clbChw,
                Info = lblChwInfo,
                Table = dgvChw,
                Summary = lblSummaryChw,

                AllKind = QASCOPE.Kind.WholeModel,
                PickedKind = QASCOPE.Kind.PickedLevels,
                AboutTheModel = true,
                CanMark = false,
                ThirdColumn = "System",
                FourthColumn = "Size",
                Everything = "Every chilled water pipe in the model.",
                Counted = "finding(s)",
                GroupedBy = "system"
            };

            Fill(_flow);
            Fill(_grids);
            Fill(_ducts);
            Fill(_fcu);
            Fill(_chw);
            Fill(_chwJoints);

            ShowChwTable();

            // Four corners; the pair is read as (vertical grids get the
            // first word, horizontal grids the second)
            cmbBubbles.Items.Add(BubbleTopLeft);
            cmbBubbles.Items.Add(BubbleTopRight);
            cmbBubbles.Items.Add(BubbleBottomLeft);
            cmbBubbles.Items.Add(BubbleBottomRight);
            cmbBubbles.SelectedIndex = 0;

            Raise(QAEVENT.Request.Refresh);
        }

        private void Fill(Page page)
        {
            page.Checks = QACHECKS.InGroup(page.Group);

            foreach (IQACHECK check in page.Checks)
                page.List.Items.Add(check.Name, true);

            if (page.List.Items.Count > 0)
                page.List.SelectedIndex = 0;

            Columns(page.Table);

            page.Table.Columns["colSheet"].HeaderText = page.ThirdColumn;
            page.Table.Columns["colView"].HeaderText = page.FourthColumn;

            page.Table.CellContentClick += dgv_CellContentClick;
        }

        // Both tables get their columns from here, so the two cannot
        // drift apart
        private static void Columns(DataGridView table)
        {
            table.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colShow",
                HeaderText = string.Empty,
                Text = "Show",
                UseColumnTextForButtonValue = true,
                Width = 55
            });

            TextColumn(table, "colSeverity", string.Empty, 60);
            TextColumn(table, "colCheck", "Check", 185);
            TextColumn(table, "colSheet", "Sheet", 160);
            TextColumn(table, "colView", "View", 115);
            TextColumn(table, "colElement", "Element", 155);
            TextColumn(table, "colProblem", "Problem", 125);
            TextColumn(table, "colValue", "Value", 75);

            // The last column takes up whatever the table has left, so
            // there is no dead strip down the right at any DPI
            table.Columns["colValue"].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;
        }

        private static void TextColumn(
            DataGridView table, string name, string header, int width)
        {
            table.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width
            });
        }

        // The tab in front, which is what the shared buttons along the
        // bottom act on
        private Page Current
        {
            get
            {
                if (tabChecks.SelectedIndex == 5)
                    return _chwJoints;

                if (tabChecks.SelectedIndex == 4)
                    return _chw;

                if (tabChecks.SelectedIndex == 3)
                    return _fcu;

                if (tabChecks.SelectedIndex == 2)
                    return _ducts;

                return tabChecks.SelectedIndex == 1 ? _grids : _flow;
            }
        }

        private void Raise(QAEVENT.Request what)
        {
            if (_handler == null || _event == null)
                return;

            _handler.Action = what;

            _event.Raise();
        }

        // ==================================================
        // Coming back from QAEVENT
        // ==================================================

        public void ShowScope(
            List<SheetItem> sheets,
            List<LevelItem> levels,
            string activeViewName,
            List<RVTLINKSELECTION.LinkItem> links)
        {
            clbGridLinks.Items.Clear();

            foreach (RVTLINKSELECTION.LinkItem link in links)
                clbGridLinks.Items.Add(link, false);

            lblGridLink.Text = links.Count == 0
                ? "No linked models - the grid comparison needs one"
                : "The model the grids were copied from:";

            var sheetItems = new List<object>();

            foreach (SheetItem sheet in sheets)
                sheetItems.Add(sheet);

            var levelItems = new List<object>();

            foreach (LevelItem level in levels)
                levelItems.Add(level);

            Offer(_flow, sheetItems, $"Every sheet   ({sheets.Count})",
                activeViewName, sheets.Count == 0);

            Offer(_grids, sheetItems, $"Every sheet   ({sheets.Count})",
                activeViewName, sheets.Count == 0);

            // The ducts page is never short of somewhere to look: the
            // model is always there, whether or not anything is on a
            // sheet yet
            Offer(_ducts, levelItems, "Everywhere in the model",
                activeViewName, false);

            Offer(_fcu, levelItems, "Everywhere in the model",
                activeViewName, false);

            Offer(_chw, levelItems, "Everywhere in the model",
                activeViewName, false);

            Offer(_chwJoints, levelItems, "Everywhere in the model",
                activeViewName, false);
        }

        private static void Offer(
            Page page,
            List<object> items,
            string allText,
            string activeViewName,
            bool nothingToPick)
        {
            page.Picks.Items.Clear();

            foreach (object item in items)
                page.Picks.Items.Add(item, false);

            page.All.Text = allText;

            page.Active.Text = string.IsNullOrEmpty(activeViewName)
                ? "The view open now"
                : $"The view open now   ({activeViewName})";

            if (nothingToPick)
            {
                page.All.Enabled = false;
                page.Picked.Enabled = false;
                page.Active.Checked = true;
            }

            page.Picked.Enabled = page.Picked.Enabled && items.Count > 0;
        }

        public void ShowFindings(
            List<QAFinding> findings, int viewCount, int sheetCount)
        {
            Page page = _running ?? Current;

            _running = null;

            page.Findings = findings ?? new List<QAFinding>();

            page.Table.Rows.Clear();

            // The list comes back ordered by sheet. Shading alternate
            // sheets makes where one drawing ends and the next begins
            // obvious without a separator row.
            string previousSheet = null;
            bool shaded = false;

            var perSheet = new Dictionary<string, int>();

            foreach (QAFinding finding in page.Findings)
            {
                string sheet = finding.Sheet ?? string.Empty;

                if (previousSheet != null && sheet != previousSheet)
                    shaded = !shaded;

                previousSheet = sheet;

                string countKey = string.IsNullOrEmpty(finding.SheetNumber)
                    ? "(no sheet)"
                    : finding.SheetNumber;

                perSheet[countKey] =
                    perSheet.ContainsKey(countKey) ? perSheet[countKey] + 1 : 1;

                int index = page.Table.Rows.Add();

                DataGridViewRow row = page.Table.Rows[index];

                row.Tag = finding;

                if (shaded)
                {
                    row.DefaultCellStyle.BackColor =
                        System.Drawing.Color.FromArgb(234, 239, 247);
                }

                row.Cells[ColShowIndex].Value = "Show";
                row.Cells[1].Value = Label(finding.Severity);
                row.Cells[2].Value = finding.Check;
                row.Cells[3].Value = finding.Sheet;
                row.Cells[4].Value = finding.ViewName;
                row.Cells[5].Value = finding.Element;
                row.Cells[6].Value = finding.Problem;
                row.Cells[7].Value = finding.Value;

                row.Cells[2].ToolTipText = finding.Check ?? string.Empty;
                row.Cells[3].ToolTipText = finding.Sheet ?? string.Empty;
                row.Cells[5].ToolTipText = finding.Element ?? string.Empty;
                row.Cells[6].ToolTipText = finding.Problem ?? string.Empty;
                row.Cells[7].ToolTipText = finding.Value ?? string.Empty;

                if (finding.Severity == QASeverity.Issue)
                {
                    row.Cells[1].Style.ForeColor =
                        System.Drawing.Color.Firebrick;
                }
                else if (finding.Severity == QASeverity.Warning)
                {
                    row.Cells[1].Style.ForeColor =
                        System.Drawing.Color.DarkGoldenrod;
                }
            }

            // A DataGridView selects its first row the moment it has
            // one, and that is NOT somebody choosing it. Now that a
            // single row counts as a selection, leaving it would make
            // every button act on whichever row happened to be first -
            // which reported "nothing can be resized" on a list of 845
            // findings whose first row was a fitting.
            page.Table.ClearSelection();

            string looked = page.ScopeDescription;

            if (page.AboutTheModel)
            {
                page.Summary.Text = page.Findings.Count == 0
                    ? "Nothing found." + Environment.NewLine + looked
                    : $"{page.Findings.Count} {page.Counted}, grouped by " +
                      $"{page.GroupedBy}. Press Show on a row to find it " +
                      "in the model." + Environment.NewLine +
                      Worst(perSheet) + looked;
            }
            else
            {
                page.Summary.Text = page.Findings.Count == 0
                    ? $"Nothing found. Checked {viewCount} view(s) on " +
                      $"{sheetCount} sheet(s)." + Environment.NewLine + looked
                    : $"{page.Findings.Count} finding(s) across {viewCount} " +
                      $"view(s) on {sheetCount} sheet(s), grouped by sheet. " +
                      "Press Show on a row to find it in the model." +
                      Environment.NewLine + Worst(perSheet) + looked;
            }

            Buttons();

            Cursor = Cursors.Default;
        }

        public void ReportError(string message)
        {
            Cursor = Cursors.Default;

            _running = null;

            MessageBox.Show(
                this, message, "SHEET QA",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // The handful of sheets carrying the most, for triage
        private static string Worst(Dictionary<string, int> perSheet)
        {
            if (perSheet.Count == 0)
                return string.Empty;

            var ordered = new List<KeyValuePair<string, int>>(perSheet);

            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

            var parts = new List<string>();

            for (int i = 0; i < ordered.Count && i < 4; i++)
                parts.Add(ordered[i].Key + " (" + ordered[i].Value + ")");

            string tail = ordered.Count > 4
                ? ", and " + (ordered.Count - 4) + " more sheet(s)"
                : string.Empty;

            return "Most on: " + string.Join(", ", parts.ToArray()) +
                   tail + ".   ";
        }

        private static string Label(QASeverity severity)
        {
            switch (severity)
            {
                case QASeverity.Issue: return "Issue";
                case QASeverity.Warning: return "Warning";
                default: return "Info";
            }
        }

        // The shared buttons act on whichever tab is in front
        private void Buttons()
        {
            Page page = Current;

            btnExport.Enabled = page.Findings.Count > 0;

            // Marking colours an element in a view. A finding that
            // belongs to the model rather than to a sheet has no view to
            // colour it in, so the buttons say so by going grey.
            bool markable = page.CanMark && page.Findings.Count > 0;

            btnMark.Enabled = markable;
            chkMarkers.Enabled = page.CanMark;

            btnClearMarks.Enabled = _marks.Count > 0 || markable;

            btnFixGrids.Enabled = Fixable(_grids).Count > 0;
            btnFixDucts.Enabled = Fixable(_ducts).Count > 0;
            btnFixFcu.Enabled = Fixable(_fcu).Count > 0;
            btnFixChw.Enabled = Fixable(_chw).Count > 0;
            btnFixChwJ.Enabled = Fixable(_chwJoints).Count > 0;
        }

        // Everything on a page that some fixer knows how to put right
        private static List<QAFinding> Fixable(Page page)
        {
            var fixable = new List<QAFinding>();

            foreach (QAFinding finding in page.Findings)
            {
                if (!string.IsNullOrEmpty(finding.FixKind))
                    fixable.Add(finding);
            }

            return fixable;
        }

        private void tabChecks_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Buttons() asks every page whether it has anything
            // fixable, so a tab change before they are all built would
            // throw. Every page belongs in this list.
            if (_flow == null || _grids == null || _ducts == null
                || _fcu == null || _chw == null || _chwJoints == null)
                return;

            Buttons();
        }

        // ==================================================
        // Scope and checks
        // ==================================================

        private void ScopeFlow_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_flow);
        }

        private void ScopeGrids_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_grids);
        }

        private void ScopeDucts_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_ducts);
        }

        private void ScopeFcu_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_fcu);
        }

        private void ScopeChw_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_chw);
        }

        private void ScopeChwJ_CheckedChanged(object sender, EventArgs e)
        {
            Scope_CheckedChanged(_chwJoints);
        }

        private static void Scope_CheckedChanged(Page page)
        {
            if (page == null)
                return;

            bool picking = page.Picked.Checked;

            page.Picks.Enabled = picking;
            page.TickAll.Enabled = picking;
            page.TickNone.Enabled = picking;
        }

        private void btnAllSheetsFlow_Click(object sender, EventArgs e)
        {
            TickSheets(_flow, true);
        }

        private void btnNoSheetsFlow_Click(object sender, EventArgs e)
        {
            TickSheets(_flow, false);
        }

        private void btnAllSheetsGrids_Click(object sender, EventArgs e)
        {
            TickSheets(_grids, true);
        }

        private void btnNoSheetsGrids_Click(object sender, EventArgs e)
        {
            TickSheets(_grids, false);
        }

        private void btnAllLevelsDucts_Click(object sender, EventArgs e)
        {
            TickSheets(_ducts, true);
        }

        private void btnNoLevelsDucts_Click(object sender, EventArgs e)
        {
            TickSheets(_ducts, false);
        }

        private void btnAllLevelsChwJ_Click(object sender, EventArgs e)
        {
            TickSheets(_chwJoints, true);
        }

        private void btnNoLevelsChwJ_Click(object sender, EventArgs e)
        {
            TickSheets(_chwJoints, false);
        }

        private void clbChwJ_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_chwJoints);
        }

        private void btnCheckChwJ_Click(object sender, EventArgs e)
        {
            Run(_chwJoints);
        }

        private void btnAllLevelsFcu_Click(object sender, EventArgs e)
        {
            TickSheets(_fcu, true);
        }

        private void btnNoLevelsFcu_Click(object sender, EventArgs e)
        {
            TickSheets(_fcu, false);
        }

        private void btnAllLevelsChw_Click(object sender, EventArgs e)
        {
            TickSheets(_chw, true);
        }

        private void btnNoLevelsChw_Click(object sender, EventArgs e)
        {
            TickSheets(_chw, false);
        }

        private static void TickSheets(Page page, bool ticked)
        {
            for (int i = 0; i < page.Picks.Items.Count; i++)
                page.Picks.SetItemChecked(i, ticked);
        }

        private void clbFlow_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_flow);
        }

        private void clbGrids_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_grids);
        }

        private static void Explain(Page page)
        {
            if (page == null)
                return;

            int index = page.List.SelectedIndex;

            page.Info.Text = index >= 0 && index < page.Checks.Count
                ? page.Checks[index].Explanation
                : string.Empty;
        }

        private void clbDucts_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_ducts);
        }

        private void btnCheckDucts_Click(object sender, EventArgs e)
        {
            Run(_ducts);
        }

        private void clbFcu_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_fcu);
        }

        private void btnCheckFcu_Click(object sender, EventArgs e)
        {
            Run(_fcu);
        }

        private void clbChw_SelectedIndexChanged(object sender, EventArgs e)
        {
            Explain(_chw);
        }

        private void btnCheckChw_Click(object sender, EventArgs e)
        {
            Run(_chw);
        }

        private void btnRunFlow_Click(object sender, EventArgs e)
        {
            Run(_flow);
        }

        private void btnCheckGrids_Click(object sender, EventArgs e)
        {
            Run(_grids);
        }

        // A tab runs on its own button and fills its own table, so what
        // is shown is always the answer to one question
        private void Run(Page page)
        {
            List<IQACHECK> wanted = Ticked(page);

            if (wanted.Count == 0)
            {
                MessageBox.Show(
                    this, "Tick at least one check on this tab.", "SHEET QA");

                return;
            }

            QASCOPE scope = ReadScope(page);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _running = page;

            _handler.Scope = scope;
            _handler.Checks = wanted;
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.Run);
        }

        private static List<IQACHECK> Ticked(Page page)
        {
            var wanted = new List<IQACHECK>();

            foreach (int index in page.List.CheckedIndices)
            {
                if (index >= 0 && index < page.Checks.Count)
                    wanted.Add(page.Checks[index]);
            }

            return wanted;
        }

        private QASETTINGS ReadSettings()
        {
            string corner = cmbBubbles.SelectedItem as string
                            ?? BubbleTopLeft;

            var settings = new QASETTINGS
            {
                GridToleranceMm = (double)numGridTol.Value,
                ExtentToleranceMm = (double)numExtTol.Value,
                BubbleTop = corner == BubbleTopLeft
                            || corner == BubbleTopRight,
                BubbleLeft = corner == BubbleTopLeft
                             || corner == BubbleBottomLeft,

                DuctReachMm = (double)numDuctReach.Value,
                ChwJointReachMm = (double)numChwJReach.Value,
                ChwJoinUnknownSystem = chkChwJUnknown.Checked,
                DuctJoinMm = (double)numDuctJoin.Value,
                DuctFallBackToNearest = chkDuctFallBack.Checked,

                FcuUnits = new List<FCUSHEET.Unit>(_fcuUnits),
                FcuParameter = cmbFcuParam.SelectedItem as string
                               ?? QAFCU.ByMark,
                FcuTolerancePercent = (double)numFcuPct.Value,
                FcuToleranceLitres = (double)numFcuAbs.Value,

                ChwTable = _chwTable,
                ChwOversizeSteps = (int)numChwSteps.Value,
                ChwOversizeFromMm = (double)numChwFrom.Value,
                ChwKeepShortPipes = chkChwKeepShort.Checked
            };

            foreach (object item in clbChwJFamilies.CheckedItems)
            {
                var family = item as QAEQUIP.FamilyItem;

                if (family != null)
                    settings.ChwEquipFamilyIds.Add(family.Id);
            }

            Ticked(clbChwSupply, settings.ChwSupplyTypeIds);
            Ticked(clbChwReturn, settings.ChwReturnTypeIds);
            Ticked(clbChwJSystems, settings.ChwJointSystemTypeIds);

            foreach (object item in clbGridLinks.CheckedItems)
            {
                var link = item as RVTLINKSELECTION.LinkItem;

                if (link != null)
                    settings.GridLinkIds.Add(link.Id);
            }

            return settings;
        }

        // ==================================================
        // The CHW sizing table: export, edit, import
        // ==================================================

        // At the user's request. The table ships built in and that is
        // still the default and still the recommended answer - but an
        // office that revises the standard, or a job working to a
        // different one, needs a way in that is not a new build.
        //
        // Export writes the table in exactly the layout Read() detects,
        // so the trip out and back is closed rather than approximate.
        private void ShowChwTable()
        {
            bool builtin = _chwTableFrom.Length == 0;

            lblChwLoaded.Text =
                "Sizing table: " +
                (builtin
                     ? "ASHRAE 90.1, ASTM A53, built in"
                     : System.IO.Path.GetFileName(_chwTableFrom) +
                       "  (" + _chwTable.Worksheet + ")") +
                "  -  " + _chwTable.Sizes.Count + " sizes, DN" +
                _chwTable.Sizes[0].DN.ToString("N0") + " to DN" +
                _chwTable.Largest.DN.ToString("N0") + ".";

            // A table somebody edited is not the office standard, and
            // the difference has to be visible without reading the
            // filename
            lblChwLoaded.ForeColor = builtin
                ? System.Drawing.SystemColors.GrayText
                : System.Drawing.Color.DarkGreen;

            btnChwBuiltin.Enabled = !builtin;
        }

        private void btnChwExport_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.FileName = "ATKINSMEC_chw_sizing.xlsx";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    CHWSIZESHEET.Write(_chwTable, dialog.FileName);
                }
                catch (Exception ex)
                {
                    Told("Could not write that file:" +
                         Environment.NewLine + Environment.NewLine +
                         ex.Message, MessageBoxIcon.Warning);

                    return;
                }

                Told(_chwTable.Sizes.Count + " size(s) written to " +
                     System.IO.Path.GetFileName(dialog.FileName) + "." +
                     Environment.NewLine + Environment.NewLine +
                     "Edit the numbers and use Import to read it back. " +
                     "Keep the headings on row 2 - they are what the " +
                     "columns are found by.",
                     MessageBoxIcon.Information);
            }
        }

        private void btnChwImport_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Excel files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string trouble = string.Empty;

                CHWSIZESHEET.Table read = Sizing(dialog.FileName, ref trouble);

                if (read == null)
                {
                    Told("Could not read a sizing table from that file." +
                         Environment.NewLine + Environment.NewLine +
                         (trouble.Length == 0
                              ? "It has no worksheets in it."
                              : trouble),
                         MessageBoxIcon.Warning);

                    return;
                }

                _chwTable = read;
                _chwTableFrom = dialog.FileName;

                ShowChwTable();

                // The findings on screen were judged against the OLD
                // table, so saying the new one is loaded is only half
                // the message
                Told("Loaded " + read.Sizes.Count + " size(s) from " +
                     "worksheet \"" + read.Worksheet + "\", DN" +
                     read.Sizes[0].DN.ToString("N0") + " to DN" +
                     read.Largest.DN.ToString("N0") + "." +
                     Environment.NewLine + Environment.NewLine +
                     "Run the checks again - anything already listed " +
                     "was judged against the previous table.",
                     MessageBoxIcon.Information);
            }
        }

        // WHICH WORKSHEET IS FOUND, NOT ASKED FOR. Detect() already
        // knows what a sizing sheet looks like, so the first worksheet
        // that parses is the one used and the label then says which it
        // was. One less dialog between somebody and their own file, and
        // an exported file has only the one sheet anyway.
        //
        // The FIRST complaint is kept rather than the last: on a
        // workbook whose real sheet is first and whose cover sheet is
        // second, the last message would describe the cover sheet.
        private static CHWSIZESHEET.Table Sizing(
            string path, ref string trouble)
        {
            List<string> worksheets;

            try
            {
                worksheets = CHWSIZESHEET.WorksheetNames(path);
            }
            catch (Exception ex)
            {
                trouble = ex.Message;

                return null;
            }

            foreach (string name in worksheets)
            {
                try
                {
                    return CHWSIZESHEET.Read(path, name);
                }
                catch (Exception ex)
                {
                    if (trouble.Length == 0)
                        trouble = ex.Message;
                }
            }

            return null;
        }

        private void btnChwBuiltin_Click(object sender, EventArgs e)
        {
            _chwTable = CHWSIZESHEET.Builtin();
            _chwTableFrom = string.Empty;

            ShowChwTable();

            Told("Back to the built-in ASHRAE 90.1 table." +
                 Environment.NewLine + Environment.NewLine +
                 "Run the checks again - anything already listed was " +
                 "judged against the imported table.",
                 MessageBoxIcon.Information);
        }

        private void Told(string message, MessageBoxIcon icon)
        {
            MessageBox.Show(this, message, "SHEET QA",
                MessageBoxButtons.OK, icon);
        }

        private static void Ticked(
            CheckedListBox list, HashSet<long> into)
        {
            foreach (object item in list.CheckedItems)
            {
                var type = item as QACHW.SystemTypeItem;

                if (type != null)
                    into.Add(type.Id);
            }
        }

        // Null when the scope is not usable, having already said so
        private QASCOPE ReadScope(Page page)
        {
            var scope = new QASCOPE();

            if (page.Active.Checked)
            {
                scope.What = QASCOPE.Kind.ActiveView;
                page.ScopeDescription = "The view open when the checks ran.";

                return scope;
            }

            if (page.Picked.Checked)
            {
                scope.What = page.PickedKind;

                int picked = 0;

                foreach (object item in page.Picks.CheckedItems)
                {
                    var sheet = item as SheetItem;

                    if (sheet != null)
                    {
                        scope.SheetIds.Add(sheet.Id);
                        picked++;

                        continue;
                    }

                    var level = item as LevelItem;

                    if (level != null)
                    {
                        scope.LevelIds.Add(level.Id);
                        picked++;
                    }
                }

                string what = page.AboutTheModel ? "level" : "sheet";

                if (picked == 0)
                {
                    MessageBox.Show(
                        this,
                        "Tick at least one " + what +
                        ", or choose another scope.",
                        "SHEET QA");

                    return null;
                }

                page.ScopeDescription =
                    picked + " picked " + what + "(s).";

                return scope;
            }

            scope.What = page.AllKind;

            page.ScopeDescription = page.Everything;

            return scope;
        }

        // ==================================================
        // The tables
        // ==================================================

        private void dgv_CellContentClick(
            object sender, DataGridViewCellEventArgs e)
        {
            var table = sender as DataGridView;

            if (table == null
                || e.RowIndex < 0
                || e.ColumnIndex != ColShowIndex)
                return;

            var finding = table.Rows[e.RowIndex].Tag as QAFinding;

            if (finding == null || finding.ElementId == 0)
                return;

            _handler.ZoomTo = finding.ElementId;

            Raise(QAEVENT.Request.Zoom);
        }

        // The rows picked out on this page when there is a real
        // selection, otherwise everything it found
        // What a button acts on: the rows picked in the table, or the
        // whole list when nothing is picked.
        //
        // This used to need MORE THAN ONE row, so picking a single
        // finding and pressing Fix quietly did the entire model
        // instead - the opposite of what anybody picking one row
        // means. One row is a selection.
        private static List<QAFinding> Selected(Page page)
        {
            var picked = new List<QAFinding>();

            if (page.Table.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in page.Table.SelectedRows)
                {
                    var finding = row.Tag as QAFinding;

                    if (finding != null)
                        picked.Add(finding);
                }

                return picked;
            }

            return new List<QAFinding>(page.Findings);
        }

        // ==================================================
        // Putting the grids right
        // ==================================================

        private void btnFixGrids_Click(object sender, EventArgs e)
        {
            List<QAFinding> fixable = FixableIn(_grids);

            if (fixable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing in the list can be put right automatically." +
                    Environment.NewLine + Environment.NewLine +
                    "Run the grid checks first. A grid that is missing " +
                    "here, or that the link does not have, is left for " +
                    "you either way - drawing one and deleting one are " +
                    "not decisions for an add-in.",
                    "SHEET QA");

                return;
            }

            DialogResult go = MessageBox.Show(
                this,
                Describe(fixable) + Environment.NewLine +
                Environment.NewLine +
                "This changes the model. It is one undo step, so Ctrl+Z " +
                "puts it all back." + Environment.NewLine +
                Environment.NewLine + "Go ahead?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (go != DialogResult.Yes)
                return;

            QASCOPE scope = ReadScope(_grids);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToFix = fixable;

            // The checks run again straight after the fix, so the list
            // shows what is still wrong rather than what was
            _running = _grids;

            _handler.Scope = scope;
            _handler.Checks = Ticked(_grids);
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.FixGrids);
        }

        // What can be put right on a page, out of the rows picked out
        // if any are
        private static List<QAFinding> FixableIn(Page page)
        {
            var fixable = new List<QAFinding>();

            foreach (QAFinding finding in Selected(page))
            {
                if (!string.IsNullOrEmpty(finding.FixKind))
                    fixable.Add(finding);
            }

            return fixable;
        }

        // Said before anything is touched, in the words of the drawing
        // rather than the words of the code
        private string Describe(List<QAFinding> findings)
        {
            int extents = 0;
            int bubbles = 0;
            int moved = 0;

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == QAGRIDFIX.Extent)
                    extents++;
                else if (finding.FixKind == QAGRIDFIX.Bubble)
                    bubbles++;
                else if (finding.FixKind == QAGRIDFIX.Moved)
                    moved++;
            }

            var parts = new List<string>();

            if (extents > 0)
            {
                parts.Add(extents + " grid end(s) stretched out to the " +
                          "furthest grid in that view");
            }

            if (bubbles > 0)
            {
                parts.Add(bubbles + " bubble(s) put on the " +
                          (cmbBubbles.SelectedItem as string ?? BubbleTopLeft) +
                          " end, and taken off the other");
            }

            if (moved > 0)
            {
                parts.Add(moved + " grid(s) moved back onto the line the " +
                          "link has - sideways only, never up or down, " +
                          "and pinned grids left alone");
            }

            return "About to change:" + Environment.NewLine + "   -   " +
                   string.Join(
                       Environment.NewLine + "   -   ", parts.ToArray());
        }

        public void GridsFixed(QAGRIDFIX.Result result)
        {
            Cursor = Cursors.Default;

            string notes = result.Notes.Count == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  string.Join(
                      Environment.NewLine + Environment.NewLine,
                      result.Notes.ToArray());

            string left = result.Skipped == 0
                ? "."
                : ", and left " + result.Skipped + " alone.";

            MessageBox.Show(
                this,
                "Put " + result.Fixed + " grid finding(s) right" + left +
                Environment.NewLine + Environment.NewLine +
                "The checks have run again, so the list below is what is " +
                "still wrong. Ctrl+Z undoes the lot." + notes,
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==================================================
        // Additional Flow
        // ==================================================

        private void btnChwFlow_Click(object sender, EventArgs e)
        {
            QASCOPE scope = ReadScope(_chw);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            // Looked at first and acted on second, so the question can
            // say exactly how many pipes and how much flow - "clear the
            // additional flow" is meaningless without knowing there is
            // any
            _handler.Scope = scope;
            _handler.Checks = Ticked(_chw);
            _handler.Settings = ReadSettings();

            _chwAsking = true;

            Raise(QAEVENT.Request.SurveyChwFlow);
        }

        // Set while the survey is out, so the answer knows it was asked
        // for rather than arriving on its own
        private bool _chwAsking;

        public void ChwFlowSurveyed(QACHWFLOW.Result found)
        {
            Cursor = Cursors.Default;

            if (!_chwAsking)
                return;

            _chwAsking = false;

            if (found.Notes.Count > 0 && found.Looked == 0)
            {
                MessageBox.Show(this,
                    string.Join(Environment.NewLine, found.Notes.ToArray()),
                    "SHEET QA");

                return;
            }

            if (found.Cleared == 0)
            {
                MessageBox.Show(
                    this,
                    "None of the " + found.Looked + " pipe(s) in the " +
                    "project carry any Additional Flow." +
                    Environment.NewLine + Environment.NewLine +
                    "Nothing to clear.",
                    "SHEET QA",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            DialogResult go = MessageBox.Show(
                this,
                found.Cleared + " of " + found.Looked + " pipe(s) in " +
                "the WHOLE PROJECT carry Additional Flow, " +
                found.LitresRemoved.ToString("N2") + " L/s in all." +
                (found.Outside == 0
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine +
                      "This is every pipe, not only chilled water: " +
                      found.Outside + " of them are on other systems or " +
                      "on none at all. Additional Flow is a nuisance " +
                      "wherever it is, which is why this ignores the " +
                      "ticks and the scope - but it is your model, so " +
                      "it is worth knowing before you agree.") +
                Environment.NewLine + Environment.NewLine +
                "Additional Flow is flow typed onto a pipe on top of " +
                "what the system calculates. It does not show on the " +
                "drawing, it survives every later change, and it inflates " +
                "what the pipe reports carrying - so the size check reads " +
                "a flow the system does not actually produce." +
                Environment.NewLine + Environment.NewLine +
                "Setting it to zero changes what those pipes report. It " +
                "is one undo step." + Environment.NewLine +
                Environment.NewLine + "Clear it?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (go != DialogResult.Yes)
                return;

            Cursor = Cursors.WaitCursor;

            _running = _chw;

            Raise(QAEVENT.Request.ClearChwFlow);
        }

        public void ChwFlowCleared(QACHWFLOW.Result result)
        {
            Cursor = Cursors.Default;

            string notes = result.Notes.Count == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  string.Join(
                      Environment.NewLine + Environment.NewLine,
                      result.Notes.ToArray());

            string skipped = result.Skipped == 0
                ? string.Empty
                : ", left " + result.Skipped + " alone";

            MessageBox.Show(
                this,
                "Cleared the Additional Flow off " + result.Cleared +
                " pipe(s)" + skipped + " - " +
                result.LitresRemoved.ToString("N2") +
                " L/s taken off in all." + Environment.NewLine +
                Environment.NewLine +
                "The checks have run again, since the sizes are now " +
                "judged against what the system actually produces. " +
                "Ctrl+Z undoes the lot." + notes,
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==================================================
        // Resizing chilled water pipe
        // ==================================================

        private void btnFixChw_Click(object sender, EventArgs e)
        {
            // EVERYTHING picked goes over, not just the pipes.
            //
            // This used to send only the findings carrying a FixKind,
            // which is pipes alone - so a fitting was only ever put
            // right if it happened to touch a pipe that was being
            // resized. Once most of the pipes in a model are correct,
            // the fittings left over are unreachable and pressing this
            // appears to do nothing for them. QACHWFIX sorts pipes
            // from fittings itself, by asking the model what each one
            // is; the window has no business guessing.
            List<QAFinding> picked = Selected(_chw);

            List<QAFinding> fixable = new List<QAFinding>();

            int others = 0;

            foreach (QAFinding finding in picked)
            {
                if (finding.FixKind == QACHWFIX.Resize)
                    fixable.Add(finding);
                else if (finding.ElementId > 0)
                    others++;
            }

            if (fixable.Count == 0 && others == 0)
            {
                int rows = _chw.Table.SelectedRows.Count;

                MessageBox.Show(
                    this,
                    rows > 0
                        ? "None of the " + rows + " row(s) you have " +
                          "picked is a pipe that can be given a size." +
                          Environment.NewLine + Environment.NewLine +
                          "A fitting has no size of its own - it takes the " +
                          "size of the pipes on its ends - so pick the " +
                          "rows whose Element is a PIPE, or clear the " +
                          "selection (Ctrl+click the picked row) to resize " +
                          "the whole list."
                        : "Nothing in the list can be resized " +
                          "automatically." +
                          Environment.NewLine + Environment.NewLine +
                          "Only pipes can be given a size. A fitting takes " +
                          "its size from the pipes it joins, so putting " +
                          "those right is what puts it right - and a pipe " +
                          "carrying more than the largest size in the " +
                          "table has nowhere to go.",
                    "SHEET QA");

                return;
            }

            DialogResult go = MessageBox.Show(
                this,
                DescribeChw(fixable, others) + Environment.NewLine +
                Environment.NewLine +
                "This changes the model. Resizing a pipe changes the " +
                "hydraulics of everything downstream of it - the " +
                "balance, the pump head, the valve authority. It puts " +
                "the drawing in step with the table; it does not " +
                "redesign the system." + Environment.NewLine +
                Environment.NewLine +
                "It is one undo step, so Ctrl+Z puts it all back." +
                Environment.NewLine + Environment.NewLine + "Go ahead?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (go != DialogResult.Yes)
                return;

            QASCOPE scope = ReadScope(_chw);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToFix = picked;

            _running = _chw;

            _handler.Scope = scope;
            _handler.Checks = Ticked(_chw);
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.FixChw);
        }

        private string DescribeChw(List<QAFinding> findings, int others)
        {
            // Grouped by the size they are going to, which is what a
            // person would want to sanity-check before agreeing
            var toSize = new Dictionary<string, int>();

            foreach (QAFinding finding in findings)
            {
                string dn = string.IsNullOrEmpty(finding.FixData)
                    ? "?"
                    : finding.FixData;

                toSize[dn] = toSize.ContainsKey(dn) ? toSize[dn] + 1 : 1;
            }

            var parts = new List<string>();

            foreach (KeyValuePair<string, int> pair in toSize)
                parts.Add(pair.Value + " to DN" + pair.Key);

            parts.Sort();

            // Say plainly whether this is the whole list or the rows
            // that were picked - the difference is one pipe against a
            // whole model, and it should never be a surprise
            string mine = _chw.Table.SelectedRows.Count > 0
                ? " YOU PICKED"
                : string.Empty;

            // The fittings are half the job and were invisible here
            // until a model turned up where the pipes were already
            // right and only the fittings were wrong
            string also = others == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  "And making " + others + " fitting(s)" + mine +
                  " match the pipes on their ends. A fitting has no " +
                  "size of its own to be given, so that is read from " +
                  "its pipes at the time rather than from the list.";

            if (findings.Count == 0)
            {
                return "No pipe" + mine + " needs a new size." + also +
                       Environment.NewLine + Environment.NewLine +
                       "Ctrl+Z undoes all of it together.";
            }

            return "About to resize " + findings.Count + " pipe(s)" +
                   mine + " to the smallest size their flow allows:" +
                   Environment.NewLine + "   -   " +
                   string.Join(Environment.NewLine + "   -   ",
                       parts.ToArray()) + also +
                   Environment.NewLine + Environment.NewLine +
                   "WHOLE RUNS move together, so more pipes than these " +
                   "may change: a pipe joined to one of these through a " +
                   "plain elbow or union is part of the same length and " +
                   "has to match it. Resizing one pipe out of a run is " +
                   "what puts a reducer in the middle of a straight " +
                   "length." + Environment.NewLine + Environment.NewLine +
                   "THE FITTINGS ARE THEN MADE TO MATCH. A fitting is set " +
                   "to the size of the pipes on each of its ends, so a " +
                   "tee of three sizes stays a tee of three sizes. One " +
                   "that will not take a size - most are driven by a " +
                   "lookup table and cannot simply be told - is REPLACED " +
                   "by a fresh fitting of the right size, which loses " +
                   "anything written on that particular one. Valves and " +
                   "other accessories are never replaced: their size is " +
                   "a choice of type, so they are reported instead." +
                   Environment.NewLine + Environment.NewLine +
                   "A BIGGER FITTING NEEDS MORE ROOM, and a short pipe " +
                   "between two of them can be left with nothing - Revit " +
                   "deletes it and says \"Line is too short\". Where the " +
                   "pipework can absorb it, the jog is stretched instead " +
                   "and the pipe kept; where it cannot, that whole run is " +
                   "refused and left exactly as it is." +
                   Environment.NewLine + Environment.NewLine +
                   "Ctrl+Z undoes all of it together.";
        }

        public void ChwFixed(QACHWFIX.Result result)
        {
            Cursor = Cursors.Default;

            string notes = result.Notes.Count == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  string.Join(
                      Environment.NewLine + Environment.NewLine,
                      result.Notes.ToArray());

            var tail = new List<string>();

            if (result.Skipped > 0)
                tail.Add("left " + result.Skipped + " run(s) alone");

            if (result.Already > 0)
            {
                tail.Add(result.Already +
                         " run(s) were already the right size");
            }

            string said = tail.Count == 0
                ? "."
                : ", " + string.Join(", ", tail.ToArray()) + ".";

            // The joints are the part worth putting in front of somebody
            // rather than burying in a note
            string joints = string.Empty;

            if (result.Repaired > 0 || result.Broken > 0)
            {
                joints = Environment.NewLine + Environment.NewLine +
                         "Joints: " + result.Repaired +
                         " put back after coming apart";

                joints += result.Broken > 0
                    ? ", " + result.Broken + " COULD NOT BE and are open " +
                      "connectors now."
                    : ", none left open.";
            }

            // Pipes that would have been deleted outright are worth
            // their own line, since that is what was reported
            string room = result.Stretched == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  "Kept " + result.Stretched + " short pipe(s) that the " +
                  "bigger fittings would have left with no length at " +
                  "all, by stretching the jog rather than letting Revit " +
                  "delete them.";

            // The fittings are the half of this the model actually
            // shows, so they get their own line rather than a note
            string follow = string.Empty;

            if (result.Fittings > 0 || result.FittingsStubborn > 0)
            {
                var how = new List<string>();

                if (result.FittingsResized > 0)
                    how.Add(result.FittingsResized + " took the new size");

                if (result.FittingsReplaced > 0)
                {
                    how.Add(result.FittingsReplaced +
                            " would not, so were replaced with a fresh " +
                            "fitting of the right size");
                }

                if (result.FittingsStubborn > 0)
                {
                    how.Add(result.FittingsStubborn +
                            " COULD NOT BE PUT RIGHT and are still on the " +
                            "list");
                }

                follow = Environment.NewLine + Environment.NewLine +
                         "Fittings: " +
                         string.Join(", ", how.ToArray()) + ".";
            }

            // If anything died after all of that, it is the first thing
            // to say, not a note at the bottom
            string died = result.Vanished == 0 && result.Squashed == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  "PIPES LOST: " + result.Vanished + " gone and " +
                  result.Squashed + " left too short. Ctrl+Z puts it all " +
                  "back. Please send " + QACHWLOG.Where + " - it says " +
                  "exactly which pipes and at what point they went.";

            MessageBox.Show(
                this,
                "Resized " + result.Pipes + " pipe(s) across " +
                result.Runs + " run(s)" + said + died + room + follow + joints +
                Environment.NewLine + Environment.NewLine +
                "The checks have run again, so the list below is what is " +
                "still out. Ctrl+Z undoes the lot." + notes,
                "SHEET QA",
                MessageBoxButtons.OK,
                result.Broken > 0 || result.FittingsStubborn > 0
                        || result.Vanished > 0 || result.Squashed > 0
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
        }

        // Comes back from QAEVENT: reading the model's families is an
        // API call, so the window cannot do it itself.
        //
        // NOTHING is pre-ticked except what looks like a fan coil. That
        // is a guess, and a wrong guess here only widens the search
        // rather than joining anything wrong - the system match still
        // decides every joint.
        public void ShowChwEquipFamilies(List<QAEQUIP.FamilyItem> families)
        {
            clbChwJFamilies.Items.Clear();

            int guessed = 0;

            foreach (QAEQUIP.FamilyItem family in families)
            {
                bool looksRight =
                    Says(family.Name, "fcu") ||
                    Says(family.Name, "fan coil") ||
                    Says(family.Name, "fan-coil");

                clbChwJFamilies.Items.Add(family, looksRight);

                if (looksRight)
                    guessed++;
            }

            // "None ticked means all of them" is a surprising rule, so
            // it is stated in the label rather than only being true at
            // the moment the list is built - somebody who unticks the
            // lot should be able to read what that did
            lblChwJFamilies.Text =
                families.Count == 0
                    ? "Equipment families a pipe may be joined to  -  " +
                      "this model has no mechanical equipment placed"
                    : "Equipment families a pipe may be joined to  -  " +
                      families.Count + " in this model" +
                      (guessed == 0
                          ? string.Empty
                          : ", " + guessed + " pre-ticked as fan coils") +
                      ".  Tick none to search every family.";
        }

        private void btnAllChwJFamilies_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbChwJFamilies.Items.Count; i++)
                clbChwJFamilies.SetItemChecked(i, true);
        }

        private void btnNoChwJFamilies_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbChwJFamilies.Items.Count; i++)
                clbChwJFamilies.SetItemChecked(i, false);
        }

        // ==================================================
        // The CHW sizing table
        // ==================================================

        // Comes back from QAEVENT: reading the model's piping system
        // types is an API call, so the window cannot do it itself
        public void ShowChwSystemTypes(List<QACHW.SystemTypeItem> types)
        {
            clbChwSupply.Items.Clear();
            clbChwReturn.Items.Clear();

            // Both lists offer every piping system type. A guess is
            // pre-ticked where the NAME says chilled water and says
            // which side; a name that only says chilled water, with no
            // side to it, is left for the person to place.
            foreach (QACHW.SystemTypeItem type in types)
            {
                bool chilled = Says(type.Name, "chilled")
                               || Says(type.Name, "chw");

                clbChwSupply.Items.Add(type,
                    chilled && (Says(type.Name, "supply")
                                || Says(type.Name, "flow")));

                clbChwReturn.Items.Add(type,
                    chilled && Says(type.Name, "return"));
            }

            string none = types.Count == 0
                ? "  -  this model has none"
                : string.Empty;

            lblChwSupply.Text =
                "Chilled water SUPPLY system type(s)" + none;

            lblChwReturn.Text =
                "Chilled water RETURN system type(s)" + none;

            // The disconnection page reads the same list, off the same
            // API call, and pre-ticks on the CLASSIFICATION instead -
            // both hydronic sides, which is what that page checked
            // before it had a list of its own.
            ShowChwJointSystemTypes(types);
        }

        private void ShowChwJointSystemTypes(
            List<QACHW.SystemTypeItem> types)
        {
            clbChwJSystems.Items.Clear();

            int guessed = 0;

            foreach (QACHW.SystemTypeItem type in types)
            {
                clbChwJSystems.Items.Add(type, type.Hydronic);

                if (type.Hydronic)
                    guessed++;
            }

            // "None ticked means every hydronic one" is a surprising
            // rule, so it is said in the label rather than only being
            // true at the moment the list is built - the same reasoning
            // as the equipment families beside it
            lblChwJSystems.Text =
                types.Count == 0
                    ? "Piping system types to check  -  this model " +
                      "has none"
                    : "Piping system types to check  -  " +
                      types.Count + " in this model" +
                      (guessed == 0
                          ? string.Empty
                          : ", " + guessed + " pre-ticked as hydronic") +
                      ".  Tick none to check every hydronic one.";
        }

        private void btnAllChwJSystems_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbChwJSystems.Items.Count; i++)
                clbChwJSystems.SetItemChecked(i, true);
        }

        private void btnNoChwJSystems_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbChwJSystems.Items.Count; i++)
                clbChwJSystems.SetItemChecked(i, false);
        }

        private static bool Says(string name, string word)
        {
            return (name ?? string.Empty).IndexOf(
                word, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ==================================================
        // The FCU schedule
        // ==================================================

        // Excel needs no Revit, so the file is read here and the units
        // handed to the checks on QASETTINGS
        private void btnFcuBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter =
                    "Excel files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm";

                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                _fcuPath = dialog.FileName;

                txtFcuFile.Text = _fcuPath;

                List<string> worksheets;

                try
                {
                    worksheets = FCUSHEET.WorksheetNames(_fcuPath);
                }
                catch (Exception ex)
                {
                    Forget("Could not open that file:" +
                           Environment.NewLine + Environment.NewLine +
                           ex.Message);

                    return;
                }

                _loadingUi = true;

                cmbFcuSheet.Items.Clear();

                foreach (string name in worksheets)
                    cmbFcuSheet.Items.Add(name);

                _loadingUi = false;

                if (cmbFcuSheet.Items.Count == 0)
                {
                    Forget("That file has no worksheets in it.");

                    return;
                }

                // The one whose name mentions FCUs, if there is one
                int wanted = 0;

                for (int i = 0; i < cmbFcuSheet.Items.Count; i++)
                {
                    string name = cmbFcuSheet.Items[i] as string
                                  ?? string.Empty;

                    if (name.IndexOf("fcu",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        wanted = i;
                        break;
                    }
                }

                cmbFcuSheet.SelectedIndex = wanted;
            }
        }

        private void cmbFcuSheet_SelectedIndexChanged(
            object sender, EventArgs e)
        {
            if (_loadingUi)
                return;

            LoadSchedule();
        }

        private void LoadSchedule()
        {
            string worksheet = cmbFcuSheet.SelectedItem as string;

            try
            {
                _fcuUnits = FCUSHEET.Read(_fcuPath, worksheet);
            }
            catch (Exception ex)
            {
                Forget(ex.Message);

                return;
            }

            double total = 0;

            foreach (FCUSHEET.Unit unit in _fcuUnits)
                total += unit.FlowLitresPerSecond;

            lblFcuLoaded.Text =
                _fcuUnits.Count + " unit(s), " + total.ToString("N0") +
                " L/s in all";

            lblFcuLoaded.ForeColor = System.Drawing.SystemColors.GrayText;
        }

        // Say what went wrong and leave nothing half-loaded behind
        private void Forget(string message)
        {
            _fcuUnits = new List<FCUSHEET.Unit>();

            lblFcuLoaded.Text = "no schedule loaded";
            lblFcuLoaded.ForeColor = System.Drawing.Color.Firebrick;

            MessageBox.Show(this, message, "SHEET QA",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Comes back from QAEVENT: reading a parameter's name off the
        // equipment is an API call, so the window cannot do it itself
        public void ShowFcuParameters(List<string> names)
        {
            _loadingUi = true;

            cmbFcuParam.Items.Clear();

            foreach (string name in names)
                cmbFcuParam.Items.Add(name);

            _loadingUi = false;

            if (cmbFcuParam.Items.Count == 0)
            {
                cmbFcuParam.Items.Add(QAFCU.ByMark);
                cmbFcuParam.SelectedIndex = 0;

                return;
            }

            int mark = cmbFcuParam.Items.IndexOf(QAFCU.ByMark);

            cmbFcuParam.SelectedIndex = mark >= 0 ? mark : 0;
        }

        private void btnFixFcu_Click(object sender, EventArgs e)
        {
            List<QAFinding> fixable = FixableIn(_fcu);

            if (fixable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing in the list can be corrected automatically." +
                    Environment.NewLine + Environment.NewLine +
                    "Run the checks first. A unit with no air terminals " +
                    "downstream, or whose terminals all read zero, has no " +
                    "ratio to scale by - there is nothing to work from, " +
                    "so it is left for you.",
                    "SHEET QA");

                return;
            }

            DialogResult go = MessageBox.Show(
                this,
                DescribeFcus(fixable) + Environment.NewLine +
                Environment.NewLine +
                "Only the air terminals are written. The duct follows, " +
                "because Revit works a duct's flow out from the terminals " +
                "it feeds." + Environment.NewLine + Environment.NewLine +
                "It is one undo step, so Ctrl+Z puts it all back." +
                Environment.NewLine + Environment.NewLine + "Go ahead?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (go != DialogResult.Yes)
                return;

            QASCOPE scope = ReadScope(_fcu);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToFix = fixable;

            _running = _fcu;

            _handler.Scope = scope;
            _handler.Checks = Ticked(_fcu);
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.FixFcus);
        }

        private string DescribeFcus(List<QAFinding> findings)
        {
            return "About to change " + findings.Count +
                   " unit(s) to the airflow the schedule gives them." +
                   Environment.NewLine + Environment.NewLine +
                   "Each unit's air terminals are scaled by one ratio, so " +
                   "they keep their shares of the total. Four terminals " +
                   "reading 40/40/40/40 against a schedule of 200 all " +
                   "become 50; reading 10/10/10/130 they become " +
                   "12.5/12.5/12.5/162.5. Whoever balanced that branch " +
                   "had a reason, and a ratio keeps it.";
        }

        public void FcusFixed(QAFCUFIX.Result result)
        {
            Cursor = Cursors.Default;

            string notes = result.Notes.Count == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  string.Join(
                      Environment.NewLine + Environment.NewLine,
                      result.Notes.ToArray());

            var tail = new List<string>();

            if (result.Skipped > 0)
                tail.Add("left " + result.Skipped + " alone");

            if (result.Already > 0)
            {
                tail.Add(result.Already +
                         " were already right by the time it ran");
            }

            string said = tail.Count == 0
                ? "."
                : ", " + string.Join(", ", tail.ToArray()) + ".";

            MessageBox.Show(
                this,
                "Corrected " + result.Fixed + " unit(s) across " +
                result.Terminals + " air terminal(s)" + said +
                Environment.NewLine + Environment.NewLine +
                "The checks have run again, so the list below is what is " +
                "still out. Ctrl+Z undoes the lot." + notes,
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==================================================
        // Joining the ducts back up
        // ==================================================

        private void btnFixDucts_Click(object sender, EventArgs e)
        {
            List<QAFinding> fixable = FixableIn(_ducts);

            if (fixable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing in the list can be joined up automatically." +
                    Environment.NewLine + Environment.NewLine +
                    "Run the duct checks first. An open end with no duct " +
                    "within reach is usually a stub left on purpose, and " +
                    "guessing what it should reach is not a decision for " +
                    "an add-in - widen the reach on the right if it " +
                    "really should join something.",
                    "SHEET QA");

                return;
            }

            DialogResult go = MessageBox.Show(
                this,
                DescribeDucts(fixable) + Environment.NewLine +
                Environment.NewLine +
                "This adds and removes elements. It is one undo step, so " +
                "Ctrl+Z puts it all back, and anything Revit refuses is " +
                "rolled back on its own without spoiling the rest." +
                Environment.NewLine + Environment.NewLine + "Go ahead?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (go != DialogResult.Yes)
                return;

            QASCOPE scope = ReadScope(_ducts);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToFix = fixable;

            _running = _ducts;

            _handler.Scope = scope;
            _handler.Checks = Ticked(_ducts);
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.FixDucts);
        }

        private void btnFixChwJ_Click(object sender, EventArgs e)
        {
            List<QAFinding> fixable = FixableIn(_chwJoints);

            if (fixable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Nothing in the list can be joined up automatically." +
                    Environment.NewLine + Environment.NewLine +
                    "Run the CHW disconnection checks first. An open end " +
                    "with nothing within reach is usually a stub left on " +
                    "purpose, and an end whose system does not match any " +
                    "free connector is left alone rather than guessed at " +
                    "- joining flow to return is the one mistake here " +
                    "that nothing downstream would catch.",
                    "SHEET QA");

                return;
            }

            int onUnits = 0;

            foreach (QAFinding finding in fixable)
            {
                if (finding.FixKind == QACHWJOINTFIX.ToUnit)
                    onUnits++;
            }

            DialogResult go = MessageBox.Show(
                this,
                "About to join up " + fixable.Count + " end(s)." +
                Environment.NewLine + Environment.NewLine +
                (onUnits == 0
                    ? "All of them are pairs of ends that meet."
                    : onUnits + " of them are pipes that belong on a " +
                      "unit's connector. THE UNIT IS NEVER MOVED - it is " +
                      "where somebody put it. The PIPE is brought onto " +
                      "the connector carrying the same system, supply to " +
                      "supply and return to return.") +
                Environment.NewLine + Environment.NewLine +
                "A pipe that would have to bend, or be left too short to " +
                "exist, is refused and listed instead." +
                Environment.NewLine + Environment.NewLine +
                "This moves pipework. It is one undo step, so Ctrl+Z " +
                "puts it all back, and anything Revit refuses is rolled " +
                "back on its own without spoiling the rest." +
                Environment.NewLine + Environment.NewLine + "Go ahead?",
                "SHEET QA",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (go != DialogResult.Yes)
                return;

            QASCOPE scope = ReadScope(_chwJoints);

            if (scope == null)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToFix = fixable;

            _running = _chwJoints;

            _handler.Scope = scope;
            _handler.Checks = Ticked(_chwJoints);
            _handler.Settings = ReadSettings();

            Raise(QAEVENT.Request.FixChwJoints);
        }

        public void ChwJointsFixed(QACHWJOINTFIX.Result result)
        {
            Cursor = Cursors.Default;

            if (result == null)
                return;

            string said =
                result.Joined + " end(s) joined, " +
                result.Aligned + " pipe(s) brought onto a unit and " +
                "joined, " + result.Refused + " refused.";

            if (result.Said.Count > 0)
            {
                said += Environment.NewLine + Environment.NewLine +
                        string.Join(Environment.NewLine,
                            result.Said.ToArray());
            }

            said += Environment.NewLine + Environment.NewLine +
                    "Every step is in " + QACHWLOG.Where;

            MessageBox.Show(
                this,
                said,
                "SHEET QA",
                MessageBoxButtons.OK,
                result.Refused == 0
                    ? MessageBoxIcon.Information
                    : MessageBoxIcon.Warning);
        }

        private string DescribeDucts(List<QAFinding> findings)
        {
            int joins = 0;
            int taps = 0;
            int branches = 0;

            foreach (QAFinding finding in findings)
            {
                if (finding.FixKind == QADUCTFIX.Join)
                    joins++;
                else if (finding.FixKind == QADUCTFIX.Tap)
                    taps++;
                else if (finding.FixKind == QADUCTFIX.Branch)
                    branches++;
            }

            var parts = new List<string>();

            if (joins > 0)
            {
                parts.Add(joins + " pair(s) of ends that already meet " +
                          "joined to each other");
            }

            if (branches > 0)
            {
                parts.Add(branches + " open end(s) tapped into a duct - " +
                          "its own system where it has one, and Revit " +
                          "makes the takeoff fitting");
            }

            if (taps > 0)
            {
                parts.Add(taps + " hanging tap(s) REPLACED: the old " +
                          "fitting is deleted and a fresh takeoff made " +
                          "onto the duct, since a tap's main side has no " +
                          "connector to rejoin");
            }

            return "About to change:" + Environment.NewLine + "   -   " +
                   string.Join(
                       Environment.NewLine + "   -   ", parts.ToArray());
        }

        public void DuctsFixed(QADUCTFIX.Result result)
        {
            Cursor = Cursors.Default;

            string notes = result.Notes.Count == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  string.Join(
                      Environment.NewLine + Environment.NewLine,
                      result.Notes.ToArray());

            var tail = new List<string>();

            if (result.Skipped > 0)
                tail.Add("left " + result.Skipped + " alone");

            if (result.Already > 0)
            {
                tail.Add(result.Already +
                         " had already been joined up since the check ran");
            }

            string said = tail.Count == 0
                ? "."
                : ", " + string.Join(", ", tail.ToArray()) + ".";

            MessageBox.Show(
                this,
                "Joined up " + result.Fixed + " connector(s)" + said +
                Environment.NewLine + Environment.NewLine +
                "The checks have run again, so the list below is what is " +
                "still open. Ctrl+Z undoes the lot." + notes,
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==================================================
        // Marking on the drawings
        // ==================================================

        private void btnMark_Click(object sender, EventArgs e)
        {
            Page page = Current;

            if (page.Findings.Count == 0)
                return;

            Cursor = Cursors.WaitCursor;

            // Marking only the rows picked out, when some are
            _handler.ToMark = Selected(page);

            Raise(QAEVENT.Request.Mark);
        }

        private void btnClearMarks_Click(object sender, EventArgs e)
        {
            // Everything this session marked, plus everything currently
            // listed on either tab - so a re-run then Clear still tidies
            // up
            var spots = new List<QAMARK.Spot>(_marks);

            spots.AddRange(QAMARK.SpotsOf(_flow.Findings));
            spots.AddRange(QAMARK.SpotsOf(_grids.Findings));

            if (spots.Count == 0)
                return;

            Cursor = Cursors.WaitCursor;

            _handler.ToClear = spots;

            Raise(QAEVENT.Request.Clear);
        }

        public void Marked(QAMARK.Result result)
        {
            Cursor = Cursors.Default;

            _marks.AddRange(result.Spots);

            btnClearMarks.Enabled = true;

            string skipped = result.Skipped == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine +
                  $"{result.Skipped} could not be marked - the view does " +
                  "not allow graphic overrides.";

            MessageBox.Show(
                this,
                $"Marked {result.Marked} finding(s) in red on their own " +
                "views." + Environment.NewLine + Environment.NewLine +
                "The colour is saved with the model. Press Clear marks " +
                "when you are done, or Ctrl+Z now." + skipped,
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public void Cleared(int cleared)
        {
            Cursor = Cursors.Default;

            _marks.Clear();

            MessageBox.Show(
                this,
                $"Cleared {cleared} mark(s).",
                "SHEET QA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ==================================================
        // Analysis markers, kept live by QAWATCH
        // ==================================================

        private void chkMarkers_CheckedChanged(object sender, EventArgs e)
        {
            if (_loadingUi)
                return;

            Page page = Current;

            if (chkMarkers.Checked)
            {
                QASCOPE scope = ReadScope(page);

                if (scope == null)
                {
                    _loadingUi = true;
                    chkMarkers.Checked = false;
                    _loadingUi = false;

                    return;
                }

                Cursor = Cursors.WaitCursor;

                _handler.Scope = scope;
                _handler.Checks = Ticked(page);
                _handler.Settings = ReadSettings();

                // The watcher is started inside QAEVENT. Subscribing to
                // DocumentChanged is a Revit API call, and a form's own
                // event handler is not an API context.
                Raise(QAEVENT.Request.Paint);
            }
            else
            {
                Cursor = Cursors.WaitCursor;

                _handler.Scope = ReadScope(page) ?? new QASCOPE();

                Raise(QAEVENT.Request.Unpaint);
            }
        }

        public void Painted(QAAVF.Result result, int cleared)
        {
            Cursor = Cursors.Default;

            if (result == null)
            {
                lblMarkerInfo.Text =
                    $"Markers cleared from {cleared} view(s).";

                return;
            }

            string skipped = result.Skipped == 0
                ? string.Empty
                : $"  {result.Skipped} view(s) skipped.";

            lblMarkerInfo.Text =
                $"{result.Markers} marker(s) in {result.ViewsPainted} " +
                "view(s), kept up to date as you work." + skipped;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            Page page = Current;

            if (page.Findings.Count == 0)
                return;

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                saveFileDialog.FileName =
                    QAEXPORT.SuggestedFileName(_docTitle);
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.OverwritePrompt = true;

                if (saveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    QAEXPORT.Write(
                        saveFileDialog.FileName,
                        _docTitle,
                        page.Findings,
                        page.ScopeDescription);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Could not write the file:" + Environment.NewLine +
                        Environment.NewLine + ex.Message +
                        Environment.NewLine + Environment.NewLine +
                        "If it is already open in Excel, close it and try again.",
                        "SHEET QA");

                    return;
                }

                DialogResult open = MessageBox.Show(
                    "Saved to:" + Environment.NewLine +
                    saveFileDialog.FileName + Environment.NewLine +
                    Environment.NewLine + "Open it now?",
                    "SHEET QA",
                    MessageBoxButtons.YesNo);

                if (open == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(
                            saveFileDialog.FileName);
                    }
                    catch
                    {
                        // the file is written either way
                    }
                }
            }
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            using (QAHELPForm help = new QAHELPForm())
            {
                help.ShowDialog(this);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Let the command open a fresh window next time
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // A watcher with no window to talk to would keep repainting
            // for the rest of the session. Disarm takes effect at once
            // and touches no API; the unsubscribe and the clearing of
            // the markers happen in QAEVENT, which is allowed to.
            QAWATCH.Disarm();

            if (_handler != null && _event != null)
            {
                _handler.Action = QAEVENT.Request.Unpaint;

                try
                {
                    _event.Raise();
                }
                catch
                {
                    // Revit shutting down; the markers stay until the
                    // next run, which is not worth a dialog
                }
            }

            QACommand.Form = null;
        }
    }
}
