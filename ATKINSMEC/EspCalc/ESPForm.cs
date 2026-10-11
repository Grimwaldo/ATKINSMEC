using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // THE ESP WINDOW. Modal, like CHW VOLUME: it runs inside the command's
    // API context, so Calculate reads the models directly and Write opens
    // its own transaction - no external event.
    //
    // REVIT-FREE ALL THE SAME. Everything that touches Revit is handed in
    // as a callback by ESPCommand, and the window deals only in ESPROW and
    // ESPNET objects, so scratchpad/esppreview renders it with no model.
    //
    // BUILT IN CODE, NOT IN A DESIGNER FILE, and laid out with table
    // panels and docking rather than fixed positions - so it resizes as
    // every window in the suite must (see "Every window resizes" in
    // CLAUDE.md) without 200 anchors to get right by hand.
    public class ESPForm : Form
    {
        private readonly string _title;
        private readonly List<ESPLINKROW> _links;
        private readonly Func<List<long>, List<ESPUNITROW>> _readUnits;
        private readonly Func<List<long>, List<string>, ESPSETTINGS, ESPRUN> _calculate;
        private readonly Func<List<KeyValuePair<string, double>>, string, string> _write;

        private ESPSETTINGS _settings;
        private List<ESPUNITROW> _units = new List<ESPUNITROW>();

        // Ticked units by key - kept apart from the grid so a filter, or
        // reading the units again, never loses a tick
        private readonly HashSet<string> _ticked = new HashSet<string>();

        private ESPRUN _run;

        private static readonly Color Ink = Color.FromArgb(31, 41, 51);
        private static readonly Color Muted = Color.FromArgb(107, 119, 133);
        private static readonly Color Warn = Color.FromArgb(154, 91, 0);
        private static readonly Color Pale = Color.FromArgb(232, 241, 248);

        // ---- controls
        private TabControl _tabs;

        private CheckedListBox _lstLinks;
        private TextBox _txtFilter;
        private DataGridView _gridUnits;
        private Label _lblUnits;

        private NumericUpDown _numTolerance, _numMargin, _numDensity, _numRoughness, _numFlex, _numTerminal;
        private TextBox _txtParameter;

        private Label _lblRun, _lblTableWarning;
        private DataGridView _gridResults, _gridPorts, _gridSteps;
        private Button _btnExport, _btnWrite, _btnLog;

        private DataGridView _gridTable, _gridFamilies;
        private Label _lblTableNote;

        private Label _lblStatus;

        public ESPForm(
            string title,
            List<ESPLINKROW> links,
            List<string> picked,
            Func<List<long>, List<ESPUNITROW>> readUnits,
            Func<List<long>, List<string>, ESPSETTINGS, ESPRUN> calculate,
            Func<List<KeyValuePair<string, double>>, string, string> write,
            ESPSETTINGS settings)
        {
            _title = title ?? string.Empty;
            _links = links ?? new List<ESPLINKROW>();
            _readUnits = readUnits;
            _calculate = calculate;
            _write = write;
            _settings = settings ?? new ESPSETTINGS();

            foreach (string key in picked ?? new List<string>())
                _ticked.Add(key);

            Build();
            FillSettings();
            FillTable();

            FORMSIZE.Remember(this);
        }

        // ==================================================
        // Building the window
        // ==================================================

        private void Build()
        {
            // SCALED BY ITS FONT, as a designer form is. Everything below
            // is sized for Segoe UI 9 at 96 DPI, and WinForms multiplies it
            // up on a screen at 125 or 150% - a window built in code gets
            // none of that unless asked, and its text then outgrows its
            // fixed widths. TableLayoutPanel scales its absolute rows and
            // columns along with it.
            SuspendLayout();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            Text = "ESP - external static pressure, ductwork  -  " + _title;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1180, 780);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.White;

            // ITS OWN MINIMUM, SMALLER THAN ITS DESIGN SIZE - which
            // FORMSIZE then leaves alone. The suite's designer windows
            // cannot shrink below their design size because anchors move
            // controls without reflowing them; this one is laid out in
            // table panels, which do reflow. And it has to be able to: at
            // 150% the design size is about 1770 x 1170, and a 1920 x 1080
            // laptop at 150% has about 1030 pixels above the taskbar.
            MinimumSize = new Size(900, 600);

            // Fitted to the screen it opens on, for the same reason
            Load += (s, e) => FitToScreen();

            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };

            _tabs.TabPages.Add(UnitsPage());
            _tabs.TabPages.Add(ResultsPage());
            _tabs.TabPages.Add(TablePage());

            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                ColumnCount = 2,
                Padding = new Padding(10, 6, 10, 6)
            };

            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Muted,
                AutoEllipsis = true,
                Text = "Tick the units, and the linked models their ductwork runs through, then Calculate."
            };

            var close = new Button { Text = "Close", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            close.Click += (s, e) => Close();

            bottom.Controls.Add(_lblStatus, 0, 0);
            bottom.Controls.Add(close, 1, 0);

            Controls.Add(_tabs);
            Controls.Add(bottom);

            CancelButton = close;

            FormClosing += (s, e) =>
            {
                ReadSettings();
                _settings.Save();
            };

            ResumeLayout(false);
            PerformLayout();
        }

        private static GroupBox Group(string text)
        {
            return new GroupBox
            {
                Text = text,
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 8, 10, 10),
                Margin = new Padding(6)
            };
        }

        // A label that wraps to whatever width it is given
        private static Label Wrapping(string text, Color colour)
        {
            var label = new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = colour,
                Margin = new Padding(3, 2, 3, 6)
            };

            label.ParentChanged += (s, e) =>
            {
                if (label.Parent != null)
                {
                    label.MaximumSize = new Size(Math.Max(100, label.Parent.ClientSize.Width - 20), 0);
                    label.Parent.Resize += (s2, e2) =>
                        label.MaximumSize = new Size(Math.Max(100, label.Parent.ClientSize.Width - 20), 0);
                }
            };

            return label;
        }

        private static DataGridView Grid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = Pale;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // Without this the header of the current cell's column is
            // drawn in selection blue, which reads as a button
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Pale;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 230, 244);
            grid.DefaultCellStyle.SelectionForeColor = Ink;

            return grid;
        }

        private static DataGridViewTextBoxColumn Column(string header, float weight, bool readOnly = true, string format = null)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = weight,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            if (format != null)
            {
                column.DefaultCellStyle.Format = format;
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            return column;
        }

        // ---- 1  Units

        private TabPage UnitsPage()
        {
            var page = new TabPage("1   Units and models") { Padding = new Padding(6), BackColor = Color.White };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));

            // -- models
            GroupBox models = Group("Linked models in the calculation");

            var modelsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            modelsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            modelsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modelsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            modelsLayout.Controls.Add(Wrapping(
                "This model is always in. Tick the links whose ductwork joins it - an AHU or a riser " +
                "drawn in another model. Open duct ends in different models that meet are joined.",
                Muted), 0, 0);

            // Link names run to fifty characters and the column is not
            // that wide: a scroll bar rather than a name cut off at the
            // part that tells two placements apart
            _lstLinks = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            foreach (ESPLINKROW link in _links)
                _lstLinks.Items.Add(link, false);

            var read = new Button { Text = "Read the units", AutoSize = true, Height = 28, Margin = new Padding(0, 8, 0, 0) };
            read.Click += (s, e) => ReadUnits();

            modelsLayout.Controls.Add(_lstLinks, 0, 1);
            modelsLayout.Controls.Add(read, 0, 2);
            models.Controls.Add(modelsLayout);

            // -- units
            GroupBox units = Group("Units - mechanical equipment with duct connectors");

            var unitsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            unitsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            unitsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };

            tools.Controls.Add(new Label { Text = "Filter", AutoSize = true, Margin = new Padding(0, 7, 6, 0) });

            _txtFilter = new TextBox { Width = 220 };
            _txtFilter.TextChanged += (s, e) => ShowUnits();
            tools.Controls.Add(_txtFilter);

            var all = new Button { Text = "Tick shown", AutoSize = true, Margin = new Padding(10, 0, 0, 0) };
            all.Click += (s, e) => TickShown(true);
            tools.Controls.Add(all);

            var none = new Button { Text = "Untick shown", AutoSize = true };
            none.Click += (s, e) => TickShown(false);
            tools.Controls.Add(none);

            _lblUnits = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(12, 7, 0, 0) };
            tools.Controls.Add(_lblUnits);

            _gridUnits = Grid();
            _gridUnits.SelectionMode = DataGridViewSelectionMode.CellSelect;

            _gridUnits.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Calc", FillWeight = 8 });
            _gridUnits.Columns.Add(Column("Unit", 40));
            _gridUnits.Columns.Add(Column("Mark", 12));
            _gridUnits.Columns.Add(Column("Model", 26));
            _gridUnits.Columns.Add(Column("Duct connectors", 20));

            _gridUnits.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_gridUnits.IsCurrentCellDirty)
                    _gridUnits.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _gridUnits.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 0)
                    return;

                var row = _gridUnits.Rows[e.RowIndex];
                var unit = row.Tag as ESPUNITROW;

                if (unit == null)
                    return;

                if (Equals(row.Cells[0].Value, true))
                    _ticked.Add(unit.Key);
                else
                    _ticked.Remove(unit.Key);

                CountUnits();
            };

            unitsLayout.Controls.Add(tools, 0, 0);
            unitsLayout.Controls.Add(_gridUnits, 0, 1);
            units.Controls.Add(unitsLayout);

            // -- the calculation
            GroupBox calc = Group("Calculation");

            var calcLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            calcLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            calcLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 3, AutoSize = true };

            for (int i = 0; i < 3; i++)
            {
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            }

            _numTolerance = Number(1, 2000, 0, 5);
            _numMargin = Number(0, 100, 1, 1);
            _numDensity = Number(0.5m, 1.5m, 3, 0.005m);
            _numRoughness = Number(0, 10, 3, 0.01m);
            _numFlex = Number(0, 20, 2, 0.1m);
            _numTerminal = Number(0, 500, 1, 1);
            _txtParameter = new TextBox { Width = 230, Anchor = AnchorStyles.Left };

            Field(fields, 0, 0, "Joint between models (mm)", _numTolerance);
            Field(fields, 1, 0, "Margin (%)", _numMargin);
            Field(fields, 2, 0, "Air density (kg/m³)", _numDensity);
            Field(fields, 0, 1, "Duct roughness (mm)", _numRoughness);
            Field(fields, 1, 1, "Flexible duct (mm)", _numFlex);
            Field(fields, 2, 1, "Terminal with no data (Pa)", _numTerminal);

            var paramLabel = new Label { Text = "Unit parameter for the ESP", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 6, 3) };
            fields.Controls.Add(paramLabel, 0, 2);
            fields.Controls.Add(_txtParameter, 1, 2);
            fields.SetColumnSpan(_txtParameter, 3);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true };

            var calculate = new Button
            {
                Text = "Calculate",
                Width = 150,
                Height = 40,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(21, 98, 155),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            calculate.FlatAppearance.BorderSize = 0;
            calculate.Click += (s, e) => Calculate();

            right.Controls.Add(calculate, 0, 0);
            right.Controls.Add(new Label
            {
                Text = "ESP = worst outlet + worst inlet,\nplus the margin.",
                AutoSize = true,
                ForeColor = Muted,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.TopRight,
                Margin = new Padding(3, 8, 3, 3)
            }, 0, 1);

            calcLayout.Controls.Add(fields, 0, 0);
            calcLayout.Controls.Add(right, 1, 0);
            calc.Controls.Add(calcLayout);

            layout.Controls.Add(models, 0, 0);
            layout.Controls.Add(units, 1, 0);
            layout.Controls.Add(calc, 0, 1);
            layout.SetColumnSpan(calc, 2);

            page.Controls.Add(layout);

            return page;
        }

        private static NumericUpDown Number(decimal min, decimal max, int decimals, decimal step)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = step,
                Width = 100,
                Anchor = AnchorStyles.Left,
                TextAlign = HorizontalAlignment.Right
            };
        }

        private static void Field(TableLayoutPanel fields, int group, int row, string text, Control box)
        {
            fields.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(group == 0 ? 3 : 18, 8, 6, 3)
            }, group * 2, row);

            fields.Controls.Add(box, group * 2 + 1, row);
        }

        // ---- 2  Results

        private TabPage ResultsPage()
        {
            var page = new TabPage("2   Results") { Padding = new Padding(10), BackColor = Color.White };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // summary
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // table warning
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));       // units
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // label
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 22));       // ports
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // label
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));       // steps
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // buttons

            _lblRun = Wrapping("Nothing calculated yet.", Ink);
            _lblTableWarning = Wrapping(string.Empty, Warn);

            _gridResults = Grid();
            _gridResults.Columns.Add(Column("Unit", 30));
            _gridResults.Columns.Add(Column("Mark", 10));
            _gridResults.Columns.Add(Column("Model", 20));
            _gridResults.Columns.Add(Column("Worst outlet (Pa)", 10, true, "0"));
            _gridResults.Columns.Add(Column("Worst inlet (Pa)", 10, true, "0"));
            _gridResults.Columns.Add(Column("ESP (Pa)", 9, true, "0"));
            _gridResults.Columns.Add(Column("ESP + margin (Pa)", 10, true, "0"));
            _gridResults.Columns.Add(Column("On the unit now", 10, true));
            _gridResults.Columns.Add(Column("From defaults on index runs", 10, true));
            _gridResults.SelectionChanged += (s, e) => ShowPorts();

            _gridPorts = Grid();
            _gridPorts.Columns.Add(Column("Port", 14));
            _gridPorts.Columns.Add(Column("Index (Pa)", 8, true, "0.0"));
            _gridPorts.Columns.Add(Column("Index run ends at", 28));
            _gridPorts.Columns.Add(Column("Terminals", 7, true));
            _gridPorts.Columns.Add(Column("Flow at unit (L/s)", 8, true, "0"));
            _gridPorts.Columns.Add(Column("Flow at terminals (L/s)", 9, true, "0"));
            _gridPorts.Columns.Add(Column("Joints to other models", 8, true));
            _gridPorts.Columns.Add(Column("Notes", 40));
            _gridPorts.SelectionChanged += (s, e) => ShowSteps();

            _gridSteps = Grid();
            _gridSteps.Columns.Add(Column("What", 14));
            _gridSteps.Columns.Add(Column("Size", 9));
            _gridSteps.Columns.Add(Column("L/s", 6, true, "0"));
            _gridSteps.Columns.Add(Column("m/s", 6, true, "0.00"));
            _gridSteps.Columns.Add(Column("Length (m)", 7, true, "0.00"));
            _gridSteps.Columns.Add(Column("Pa/m", 6, true, "0.000"));
            _gridSteps.Columns.Add(Column("C", 5, true, "0.00"));
            _gridSteps.Columns.Add(Column("Loss (Pa)", 7, true, "0.0"));
            _gridSteps.Columns.Add(Column("Running (Pa)", 7, true, "0.0"));
            _gridSteps.Columns.Add(Column("Figure from", 26));
            _gridSteps.Columns.Add(Column("Model / element", 26));

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };

            _btnExport = new Button { Text = "Export to Excel...", AutoSize = true, Height = 30, Enabled = false };
            _btnExport.Click += (s, e) => Export();

            _btnWrite = new Button { Text = "Write ESP + margin to the units", AutoSize = true, Height = 30, Enabled = false };
            _btnWrite.Click += (s, e) => Write();

            _btnLog = new Button { Text = "Open the log", AutoSize = true, Height = 30, Enabled = false };
            _btnLog.Click += (s, e) => Open(_run == null ? null : _run.LogPath);

            buttons.Controls.Add(_btnExport);
            buttons.Controls.Add(_btnWrite);
            buttons.Controls.Add(_btnLog);

            layout.Controls.Add(_lblRun, 0, 0);
            layout.Controls.Add(_lblTableWarning, 0, 1);
            layout.Controls.Add(_gridResults, 0, 2);
            layout.Controls.Add(Wrapping("Ports of the unit picked above - each is a run of its own", Muted), 0, 3);
            layout.Controls.Add(_gridPorts, 0, 4);
            layout.Controls.Add(Wrapping("The index run of the port picked above, from the unit outwards. Amber rows came from a default.", Muted), 0, 5);
            layout.Controls.Add(_gridSteps, 0, 6);
            layout.Controls.Add(buttons, 0, 7);

            page.Controls.Add(layout);

            return page;
        }

        // ---- 3  Coefficients

        private TabPage TablePage()
        {
            var page = new TabPage("3   Coefficients") { Padding = new Padding(10), BackColor = Color.White };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lblTableNote = Wrapping(string.Empty, Warn);
            layout.Controls.Add(_lblTableNote, 0, 0);
            layout.SetColumnSpan(_lblTableNote, 2);

            layout.Controls.Add(Wrapping(
                "Fittings: loss = C x velocity pressure. Supply is air leaving the unit (diverging), " +
                "return is air coming back (converging). A family's own coefficient beats this table.", Muted), 0, 1);

            layout.Controls.Add(Wrapping(
                "Your values for families, in Pa. These beat everything, including what the family says - " +
                "for attenuators, fire dampers, grilles, filters.", Muted), 1, 1);

            _gridTable = Grid();
            _gridTable.Columns.Add(Column("Kind", 26));
            _gridTable.Columns.Add(Column("Supply C", 10, false, "0.00"));
            _gridTable.Columns.Add(Column("Return C", 10, false, "0.00"));
            _gridTable.Columns.Add(Column("Note", 44));
            _gridTable.CellEndEdit += (s, e) => ReadTable();

            _gridFamilies = Grid();
            _gridFamilies.AllowUserToAddRows = true;
            _gridFamilies.AllowUserToDeleteRows = true;
            _gridFamilies.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _gridFamilies.Columns.Add(Column("Family : Type", 70, false));
            _gridFamilies.Columns.Add(Column("Pa", 14, false, "0.0"));

            layout.Controls.Add(_gridTable, 0, 2);
            layout.Controls.Add(_gridFamilies, 1, 2);

            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var restore = new Button { Text = "Put the built-in values back", AutoSize = true, Height = 30 };
            restore.Click += (s, e) =>
            {
                _settings.Table = ESPSETTINGS.DefaultTable();
                FillTable();
            };
            left.Controls.Add(restore);

            var rightButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            var flagged = new Button { Text = "Add the families the last run had no data for", AutoSize = true, Height = 30 };
            flagged.Click += (s, e) => AddFlagged();
            rightButtons.Controls.Add(flagged);

            layout.Controls.Add(left, 0, 3);
            layout.Controls.Add(rightButtons, 1, 3);

            page.Controls.Add(layout);

            return page;
        }

        private void FitToScreen()
        {
            try
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;

                int width = Math.Min(Width, area.Width);
                int height = Math.Min(Height, area.Height);

                if (width == Width && height == Height)
                    return;

                MinimumSize = new Size(Math.Min(MinimumSize.Width, width), Math.Min(MinimumSize.Height, height));
                Size = new Size(width, height);
                Location = new Point(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2);
            }
            catch
            {
                // a window a little too big is not worth failing over
            }
        }

        // ==================================================
        // Settings in and out of the controls
        // ==================================================

        private static decimal Clamp(NumericUpDown box, double value)
        {
            decimal v = (decimal)value;

            return Math.Max(box.Minimum, Math.Min(box.Maximum, v));
        }

        private void FillSettings()
        {
            _numTolerance.Value = Clamp(_numTolerance, _settings.ToleranceMm);
            _numMargin.Value = Clamp(_numMargin, _settings.MarginPercent);
            _numDensity.Value = Clamp(_numDensity, _settings.Density);
            _numRoughness.Value = Clamp(_numRoughness, _settings.RoughnessMm);
            _numFlex.Value = Clamp(_numFlex, _settings.FlexRoughnessMm);
            _numTerminal.Value = Clamp(_numTerminal, _settings.TerminalDropPa);
            _txtParameter.Text = _settings.Parameter;
        }

        private void ReadSettings()
        {
            _settings.ToleranceMm = (double)_numTolerance.Value;
            _settings.MarginPercent = (double)_numMargin.Value;
            _settings.Density = (double)_numDensity.Value;
            _settings.RoughnessMm = (double)_numRoughness.Value;
            _settings.FlexRoughnessMm = (double)_numFlex.Value;
            _settings.TerminalDropPa = (double)_numTerminal.Value;

            if (_txtParameter.Text.Trim().Length > 0)
                _settings.Parameter = _txtParameter.Text.Trim();

            ReadTable();
            ReadFamilies();
        }

        private void FillTable()
        {
            _gridTable.Rows.Clear();

            foreach (ESPSETTINGS.Row row in _settings.Table)
            {
                int i = _gridTable.Rows.Add(row.Kind, row.Supply, row.Return, row.Note);
                _gridTable.Rows[i].Tag = row;
            }

            _gridFamilies.Rows.Clear();

            foreach (KeyValuePair<string, double> family in _settings.Families.OrderBy(f => f.Key))
                _gridFamilies.Rows.Add(family.Key, family.Value);

            TableNote();
        }

        private static bool Parse(object value, out double number)
        {
            number = 0;

            if (value == null)
                return false;

            if (value is double)
            {
                number = (double)value;
                return true;
            }

            string text = value.ToString().Trim().Replace(',', '.');

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        private void ReadTable()
        {
            foreach (DataGridViewRow row in _gridTable.Rows)
            {
                var source = row.Tag as ESPSETTINGS.Row;

                if (source == null)
                    continue;

                double supply, ret;

                if (Parse(row.Cells[1].Value, out supply) && supply >= 0)
                    source.Supply = supply;

                if (Parse(row.Cells[2].Value, out ret) && ret >= 0)
                    source.Return = ret;

                row.Cells[1].Value = source.Supply;
                row.Cells[2].Value = source.Return;
            }

            TableNote();
        }

        private void ReadFamilies()
        {
            _settings.Families.Clear();

            foreach (DataGridViewRow row in _gridFamilies.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string name = (row.Cells[0].Value ?? string.Empty).ToString().Trim();

                double pa;

                // A family with no value typed yet is not a value of zero
                if (name.Length > 0 && Parse(row.Cells[1].Value, out pa) && pa >= 0)
                    _settings.Families[name] = pa;
            }
        }

        private void TableNote()
        {
            string text = _settings.TableIsBuiltIn()
                ? "These are the add-in's BUILT-IN rough coefficients - typical figures chosen for it, not the ASHRAE " +
                  "Duct Fitting Database or CIBSE Guide C. Put your own values in; they are saved and used from then on."
                : string.Empty;

            _lblTableNote.Text = text;
            _lblTableNote.Visible = text.Length > 0;

            if (_lblTableWarning != null)
            {
                _lblTableWarning.Text = _settings.TableIsBuiltIn()
                    ? "Fitting coefficients are the built-in rough values - see the Coefficients tab before relying on these figures."
                    : string.Empty;
                _lblTableWarning.Visible = _lblTableWarning.Text.Length > 0;
            }
        }

        // ==================================================
        // Units
        // ==================================================

        private List<long> TickedLinks()
        {
            return _lstLinks.CheckedItems.Cast<ESPLINKROW>().Select(l => l.Id).ToList();
        }

        private void ReadUnits()
        {
            if (_readUnits == null)
                return;

            Cursor = Cursors.WaitCursor;

            try
            {
                _units = _readUnits(TickedLinks()) ?? new List<ESPUNITROW>();

                foreach (ESPUNITROW unit in _units.Where(u => u.Picked))
                    _ticked.Add(unit.Key);

                ShowUnits();

                Status(_units.Count + " unit(s) with duct connectors in this model" +
                       (TickedLinks().Count > 0 ? " and " + TickedLinks().Count + " linked model(s)." : "."));
            }
            catch (Exception ex)
            {
                Status("Could not read the units: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // For the preview harness, which has no Revit to read from
        public void SetUnits(List<ESPUNITROW> units)
        {
            _units = units ?? new List<ESPUNITROW>();

            foreach (ESPUNITROW unit in _units.Where(u => u.Picked))
                _ticked.Add(unit.Key);

            ShowUnits();
        }

        private IEnumerable<ESPUNITROW> Filtered()
        {
            string filter = _txtFilter.Text.Trim();

            return filter.Length == 0
                ? _units
                : _units.Where(u =>
                    (u.Name + " " + u.Mark + " " + u.Model + " " + u.Ports)
                    .IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ShowUnits()
        {
            _gridUnits.Rows.Clear();

            foreach (ESPUNITROW unit in Filtered())
            {
                int i = _gridUnits.Rows.Add(_ticked.Contains(unit.Key), unit.Name, unit.Mark, unit.Model, unit.Ports);
                _gridUnits.Rows[i].Tag = unit;
            }

            CountUnits();
        }

        private void CountUnits()
        {
            int shown = _gridUnits.Rows.Count;
            int ticked = _units.Count(u => _ticked.Contains(u.Key));

            _lblUnits.Text = ticked + " ticked" + (shown != _units.Count ? ",  " + shown + " of " + _units.Count + " shown" : ",  " + _units.Count + " in all");
        }

        private void TickShown(bool on)
        {
            foreach (DataGridViewRow row in _gridUnits.Rows)
            {
                var unit = row.Tag as ESPUNITROW;

                if (unit == null)
                    continue;

                if (on)
                    _ticked.Add(unit.Key);
                else
                    _ticked.Remove(unit.Key);
            }

            ShowUnits();
        }

        // ==================================================
        // Calculate
        // ==================================================

        private void Calculate()
        {
            ReadSettings();
            _settings.Save();

            List<string> keys = _units.Where(u => _ticked.Contains(u.Key)).Select(u => u.Key).ToList();

            if (keys.Count == 0)
            {
                Status("Tick at least one unit first - press Read the units if the list is empty.");
                return;
            }

            if (_calculate == null)
                return;

            Cursor = Cursors.WaitCursor;
            Status("Reading the ductwork of " + (TickedLinks().Count + 1) + " model(s)...");
            Refresh();

            try
            {
                ShowRun(_calculate(TickedLinks(), keys, _settings));
                _tabs.SelectedIndex = 1;
            }
            catch (Exception ex)
            {
                Status("The calculation failed: " + ex.GetType().Name + " - " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // Public for the preview harness, which calculates without Revit
        public void ShowRun(ESPRUN run)
        {
            _run = run;

            _gridResults.Rows.Clear();
            _gridPorts.Rows.Clear();
            _gridSteps.Rows.Clear();

            if (run == null)
                return;

            _gridResults.Columns[7].HeaderText = "'" + _settings.Parameter + "' now (Pa)";

            foreach (ESPNET.Result result in run.Results)
            {
                double? now;
                run.InModel.TryGetValue(result.Unit.Key, out now);

                int flagged = result.Runs.Sum(r => r.FlaggedOnIndex);

                int i = _gridResults.Rows.Add(
                    result.Unit.Family,
                    result.Unit.Mark,
                    result.Unit.Model,
                    result.OutSide,
                    result.InSide,
                    result.Esp,
                    result.EspWithMargin,
                    now.HasValue ? Math.Round(now.Value).ToString(CultureInfo.InvariantCulture) : "-",
                    flagged == 0 ? "none" : flagged + " item(s)");

                DataGridViewRow row = _gridResults.Rows[i];
                row.Tag = result;

                if (flagged > 0)
                    row.Cells[8].Style.ForeColor = Warn;

                if (result.Runs.Any(r => !r.Connected) || result.Runs.Any(r => r.Connected && r.Terminals + r.Equipment == 0))
                    row.Cells[0].Style.ForeColor = Warn;
            }

            int bridged = run.Bridges.Count;

            _lblRun.Text =
                run.Results.Count + " unit(s) from " + run.Models.Count + " model(s), " + run.Parts +
                " duct element(s) read.  " + bridged + " joint(s) made between models" +
                (run.NearMisses.Count > 0
                    ? ", and " + run.NearMisses.Count + " pair(s) of facing ends just beyond the tolerance - see the log."
                    : ".") +
                (run.Missing.Count > 0 ? "  " + run.Missing.Count + " unit(s) not found - read the units again." : string.Empty);

            TableNote();

            bool any = run.Results.Count > 0;

            _btnExport.Enabled = any;
            _btnWrite.Enabled = any && run.Results.Any(r => r.Unit.LinkId == 0);
            _btnLog.Enabled = !string.IsNullOrEmpty(run.LogPath) && File.Exists(run.LogPath);

            Status("Calculated. Pick a unit to see its ports, and a port to see its index run.");

            if (_gridResults.Rows.Count > 0)
            {
                _gridResults.ClearSelection();
                _gridResults.Rows[0].Selected = true;
                ShowPorts();
            }
        }

        private void ShowPorts()
        {
            _gridPorts.Rows.Clear();
            _gridSteps.Rows.Clear();

            ESPNET.Result result = _gridResults.SelectedRows.Count == 0
                ? null
                : _gridResults.SelectedRows[0].Tag as ESPNET.Result;

            if (result == null)
                return;

            foreach (ESPNET.Run run in result.Runs)
            {
                string port = (run.System.Length == 0 ? "?" : run.System == "Other" ? "Other air" : run.System) +
                              (run.Converging ? "  (in)" : "  (out)");

                int i = _gridPorts.Rows.Add(
                    port,
                    run.Connected ? run.Index : (object)null,
                    run.Connected ? run.EndsAt : "not joined to anything",
                    run.Terminals,
                    run.FlowAtUnit * 1000,
                    run.FlowAtEnds * 1000,
                    run.Bridges,
                    string.Join("  ", run.Notes) +
                    (run.FlaggedOnIndex > 0 ? (run.Notes.Count > 0 ? "  " : string.Empty) +
                     run.FlaggedOnIndex + " step(s) of the index run from defaults." : string.Empty));

                _gridPorts.Rows[i].Tag = run;

                if (!run.Connected || run.Terminals + run.Equipment == 0)
                    _gridPorts.Rows[i].DefaultCellStyle.ForeColor = Warn;
            }

            if (_gridPorts.Rows.Count > 0)
            {
                _gridPorts.ClearSelection();
                _gridPorts.Rows[0].Selected = true;
                ShowSteps();
            }
        }

        private void ShowSteps()
        {
            _gridSteps.Rows.Clear();

            ESPNET.Run run = _gridPorts.SelectedRows.Count == 0
                ? null
                : _gridPorts.SelectedRows[0].Tag as ESPNET.Run;

            if (run == null)
                return;

            foreach (ESPNET.Step step in run.Steps)
            {
                int i = _gridSteps.Rows.Add(
                    step.What,
                    step.Size,
                    step.Flow * 1000,
                    step.Velocity > 0 ? step.Velocity : (object)null,
                    step.Length > 0 ? step.Length : (object)null,
                    step.PerMetre > 0 ? step.PerMetre : (object)null,
                    step.Coefficient > 0 ? step.Coefficient : (object)null,
                    step.Loss,
                    step.Running,
                    step.Source,
                    step.Part.Model + "  /  " + step.Part.Label);

                if (step.Flagged)
                    _gridSteps.Rows[i].DefaultCellStyle.ForeColor = Warn;
            }
        }

        // ==================================================
        // Export, write, log
        // ==================================================

        private void Export()
        {
            if (_run == null)
                return;

            using (var dialog = new SaveFileDialog
            {
                Filter = "Excel workbook (*.xlsx)|*.xlsx",
                FileName = "ESP " + Safe(_title) + " " + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    ESPEXPORT.Write(dialog.FileName, _run, _settings, _title);

                    Status("Exported to " + dialog.FileName);

                    if (MessageBox.Show(this, "Exported. Open it now?", "ESP",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        Open(dialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not write the workbook:\n\n" + ex.Message, "ESP",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private static string Safe(string text)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                text = text.Replace(c, '_');

            return text.Length > 60 ? text.Substring(0, 60) : text;
        }

        private void Write()
        {
            if (_run == null || _write == null)
                return;

            ReadSettings();

            var here = _run.Results.Where(r => r.Unit.LinkId == 0).ToList();
            int linked = _run.Results.Count - here.Count;

            string question =
                "Write the ESP + margin to '" + _settings.Parameter + "' on " + here.Count + " unit(s) in this model?" +
                (linked > 0 ? "\n\n" + linked + " unit(s) in linked models cannot be written from here." : string.Empty) +
                (_settings.TableIsBuiltIn()
                    ? "\n\nThe fitting coefficients are still the built-in rough values."
                    : string.Empty);

            if (MessageBox.Show(this, question, "ESP", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                string said = _write(
                    here.Select(r => new KeyValuePair<string, double>(r.Unit.Key, r.EspWithMargin)).ToList(),
                    _settings.Parameter);

                foreach (ESPNET.Result r in here)
                    _run.InModel[r.Unit.Key] = Math.Round(r.EspWithMargin, 1);

                ShowRun(_run);
                Status(said);
            }
            catch (Exception ex)
            {
                Status("Could not write: " + ex.Message);
            }
        }

        private void AddFlagged()
        {
            if (_run == null)
            {
                Status("Calculate first - this lists the families the run had to use defaults for.");
                return;
            }

            ReadFamilies();

            var known = new HashSet<string>(_settings.Families.Keys, StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in _gridFamilies.Rows)
            {
                if (!row.IsNewRow && row.Cells[0].Value != null)
                    known.Add(row.Cells[0].Value.ToString().Trim());
            }

            int added = 0;

            foreach (string family in _run.Results.SelectMany(r => r.Runs).SelectMany(r => r.FlaggedFamilies)
                         .Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (known.Contains(family))
                    continue;

                _gridFamilies.Rows.Add(family, null);
                known.Add(family);
                added++;
            }

            Status(added == 0
                ? "Every family the last run used a default for already has a row."
                : added + " famil(ies) added - type a value in Pa against each, then Calculate again.");
        }

        private void Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                // UseShellExecute: on .NET 8 Process.Start(file) no longer
                // opens a document by its association without it
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Status("Could not open " + path + ": " + ex.Message);
            }
        }

        private void Status(string text)
        {
            _lblStatus.Text = text;
        }

        // For the preview harness
        public void ShowTab(int index)
        {
            _tabs.SelectedIndex = index;
        }
    }
}
