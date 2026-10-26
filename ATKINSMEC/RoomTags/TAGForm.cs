using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // THE ROOM TAGS WINDOW. Modal, like CHW VOLUME and DUCT ESP: it runs in
    // the command's API context, so Check reads the model directly and Tag
    // missing / Remove duplicates open their own transactions - no external
    // event.
    //
    // Revit-free all the same: TAGCommand hands in the reads as callbacks,
    // and the window deals only in TAGPLAN objects. Built in code, in table
    // panels, as ESPForm is.
    public class TAGForm : Form
    {
        private readonly string _title;
        private readonly string _model;
        private readonly List<TAGPLAN.Link> _links;
        private readonly List<TypeItem> _types;
        private readonly Func<List<long>, TAGPLAN.Read> _check;
        private readonly Func<Dictionary<long, List<TAGPLAN.Room>>, long, TAGPLAN.Done> _tag;
        private readonly Func<List<long>, TAGPLAN.Removal> _remove;

        private readonly TAGPLAN.Choice _before;

        // Ticked links by id - kept apart from the list, which a search
        // refills
        private readonly HashSet<long> _ticked;

        // Plans the user unticked, by view id - outlives a check
        private readonly HashSet<long> _unticked = new HashSet<long>();

        private TAGPLAN.Read _read;
        private List<TAGPLAN.Row> _rows;

        private bool _filling;

        // Asked before tagging or removing; a message box unless
        // tests\roomtagtest answers instead
        public Func<string, bool> Confirm;

        private static readonly Color Ink = Color.FromArgb(31, 41, 51);
        private static readonly Color Muted = Color.FromArgb(107, 119, 133);
        private static readonly Color Warn = Color.FromArgb(154, 91, 0);
        private static readonly Color Pale = Color.FromArgb(232, 241, 248);

        private const string Picked = "Missing and duplicate tags in the picked plan";

        private Font _bold;

        private Font Bold
        {
            get { return _bold ?? (_bold = new Font(Font, FontStyle.Bold)); }
        }

        private TextBox _txtLinks, _txtSheets;
        private CheckedListBox _lstLinks;
        private Label _lblLinks, _lblRows, _lblPicked, _lblStatus;
        private DataGridView _grid;
        private ListBox _lstMissing;
        private ComboBox _cmbType;
        private CheckBox _chkAnyType;
        private Button _btnCheck, _btnTag, _btnRemove, _btnLog;

        private class TypeItem
        {
            public long Id;
            public string Name;

            public override string ToString()
            {
                return Name;
            }
        }

        public TAGForm(
            string title,
            string model,
            List<TAGPLAN.Link> links,
            List<KeyValuePair<long, string>> types,
            long preferredType,
            Func<List<long>, TAGPLAN.Read> check,
            Func<Dictionary<long, List<TAGPLAN.Room>>, long, TAGPLAN.Done> tag,
            Func<List<long>, TAGPLAN.Removal> remove)
        {
            _title = title ?? string.Empty;
            _model = model;
            _links = links ?? new List<TAGPLAN.Link>();
            _types = (types ?? new List<KeyValuePair<long, string>>())
                .Select(t => new TypeItem { Id = t.Key, Name = t.Value })
                .ToList();
            _check = check;
            _tag = tag;
            _remove = remove;

            _before = TAGPLAN.Recall(_model);
            _ticked = TAGPLAN.Ticked(_links, _before);

            Build();

            ShowLinks();
            FillTypes(preferredType);
            ShowRows();

            FORMSIZE.Remember(this);
        }

        // ==================================================
        // Building the window
        // ==================================================

        private void Build()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;

            Text = "Room tags  -  " + _title;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1080, 700);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = true;
            BackColor = Color.White;

            // Its own minimum, below its design size: table panels reflow
            // (ESPForm has why)
            MinimumSize = new Size(820, 520);

            Load += (s, e) => FitToScreen();

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(4) };

            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            body.Controls.Add(LinksGroup(), 0, 0);
            body.Controls.Add(PlansGroup(), 1, 0);

            Controls.Add(body);
            Controls.Add(Bottom());

            FormClosing += (s, e) => Remember();

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

        // "Search" and a box filling the rest of the line
        private static TableLayoutPanel SearchLine(out TextBox box)
        {
            var line = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };

            line.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            line.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            line.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(0, 6, 6, 0) }, 0, 0);

            box = new TextBox { Dock = DockStyle.Fill };
            line.Controls.Add(box, 1, 0);

            return line;
        }

        private GroupBox LinksGroup()
        {
            GroupBox group = Group("Linked models");

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.Controls.Add(SearchLine(out _txtLinks), 0, 0);
            _txtLinks.TextChanged += (s, e) => ShowLinks();

            // Link names run to fifty characters: a scroll bar rather than
            // a name cut off at the part that tells two placements apart
            _lstLinks = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            _lstLinks.ItemCheck += LinkChecked;

            layout.Controls.Add(_lstLinks, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };

            var all = new Button { Text = "All", Width = 70, Height = 26 };
            all.Click += (s, e) => TickLinks(true);

            var none = new Button { Text = "None", Width = 70, Height = 26 };
            none.Click += (s, e) => TickLinks(false);

            buttons.Controls.Add(all);
            buttons.Controls.Add(none);
            layout.Controls.Add(buttons, 0, 2);

            _lblLinks = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(3, 4, 3, 0) };
            _lblLinks.MaximumSize = new Size(300, 0);
            layout.Controls.Add(_lblLinks, 0, 3);

            group.Controls.Add(layout);

            return group;
        }

        private GroupBox PlansGroup()
        {
            GroupBox group = Group("Plans on sheets");

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

            var tools = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 1, Margin = new Padding(0, 0, 0, 6) };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _btnCheck = new Button { Text = "Check sheets", AutoSize = true, Height = 28, Margin = new Padding(0, 3, 12, 3) };
            _btnCheck.Click += (s, e) => Check();

            var all = new Button { Text = "Tick all", AutoSize = true, Height = 28 };
            all.Click += (s, e) => TickRows(true);

            var none = new Button { Text = "Untick all", AutoSize = true, Height = 28 };
            none.Click += (s, e) => TickRows(false);

            tools.Controls.Add(_btnCheck, 0, 0);
            tools.Controls.Add(all, 1, 0);
            tools.Controls.Add(none, 2, 0);
            tools.Controls.Add(new Label { Text = "Search", AutoSize = true, Margin = new Padding(16, 7, 6, 0) }, 3, 0);

            _txtSheets = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 0) };
            _txtSheets.TextChanged += (s, e) => FilterRows();
            tools.Controls.Add(_txtSheets, 4, 0);

            layout.Controls.Add(tools, 0, 0);

            _lblRows = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(3, 0, 3, 6) };
            layout.Controls.Add(_lblRows, 0, 1);

            _grid = Grid();

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Fix",
                FillWeight = 28,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ToolTipText = "The plans Tag missing and Remove duplicates act on"
            });
            _grid.Columns.Add(Column("Sheet", 60));
            _grid.Columns.Add(Column("Sheet name", 150));
            _grid.Columns.Add(Column("View", 170));
            _grid.Columns.Add(Column("Rooms", 42, true));
            _grid.Columns.Add(Column("Tagged", 42, true));
            _grid.Columns.Add(Column("Missing", 46, true));
            _grid.Columns.Add(Column("Duplicates", 56, true));

            // A tick takes effect at once, not when the cell is left
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _grid.CellValueChanged += RowTicked;
            _grid.SelectionChanged += (s, e) => ShowPicked();

            layout.Controls.Add(_grid, 0, 2);

            _lblPicked = new Label { AutoSize = true, ForeColor = Muted, Margin = new Padding(3, 8, 3, 4), Text = Picked };
            layout.Controls.Add(_lblPicked, 0, 3);

            _lstMissing = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                HorizontalScrollbar = true,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = SelectionMode.None
            };

            layout.Controls.Add(_lstMissing, 0, 4);

            group.Controls.Add(layout);

            return group;
        }

        private TableLayoutPanel Bottom()
        {
            var bottom = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 74,
                ColumnCount = 8,
                RowCount = 2,
                Padding = new Padding(10, 8, 10, 4)
            };

            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            bottom.Controls.Add(new Label { Text = "Tag type", AutoSize = true, Margin = new Padding(0, 9, 6, 0) }, 0, 0);

            _cmbType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 0, 3) };
            bottom.Controls.Add(_cmbType, 1, 0);

            _btnTag = new Button { Text = "Tag missing", AutoSize = true, Height = 28, Margin = new Padding(10, 3, 0, 3) };
            _btnTag.Click += (s, e) => TagMissing();
            bottom.Controls.Add(_btnTag, 2, 0);

            _btnRemove = new Button { Text = "Remove duplicates", AutoSize = true, Height = 28, Margin = new Padding(18, 3, 0, 3) };
            _btnRemove.Click += (s, e) => RemoveDuplicates();
            bottom.Controls.Add(_btnRemove, 3, 0);

            // A name tag and an area tag of one room are two tags on
            // purpose in many offices: off, only tags of one type are
            // duplicates
            _chkAnyType = new CheckBox { Text = "Any tag type", AutoSize = true, Margin = new Padding(10, 9, 0, 0) };
            _chkAnyType.CheckedChanged += (s, e) => Recount();
            bottom.Controls.Add(_chkAnyType, 4, 0);

            _btnLog = new Button { Text = "Log", Width = 70, Height = 28 };
            _btnLog.Click += (s, e) => OpenLog();
            bottom.Controls.Add(_btnLog, 6, 0);

            var close = new Button { Text = "Close", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
            close.Click += (s, e) => Close();
            bottom.Controls.Add(close, 7, 0);

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Muted,
                AutoEllipsis = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            bottom.Controls.Add(_lblStatus, 0, 1);
            bottom.SetColumnSpan(_lblStatus, 8);

            CancelButton = close;

            return bottom;
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
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Pale;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Ink;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 230, 244);
            grid.DefaultCellStyle.SelectionForeColor = Ink;

            return grid;
        }

        private static DataGridViewTextBoxColumn Column(string header, float weight, bool number = false)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                FillWeight = weight,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            if (number)
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            return column;
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

        // Esc clears a search first, then closes
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                foreach (TextBox box in new[] { _txtLinks, _txtSheets })
                {
                    if (box != null && box.Focused && box.TextLength > 0)
                    {
                        box.Clear();
                        return true;
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ==================================================
        // Links
        // ==================================================

        // The links the search matches; ticks stand while hidden
        private void ShowLinks()
        {
            _filling = true;

            try
            {
                _lstLinks.BeginUpdate();
                _lstLinks.Items.Clear();

                foreach (TAGPLAN.Link link in _links.Where(l => VENTLINKFILTER.Matches(l.Label, _txtLinks.Text)))
                    _lstLinks.Items.Add(link, _ticked.Contains(link.Id));
            }
            finally
            {
                _lstLinks.EndUpdate();
                _filling = false;
            }

            SayLinks();
        }

        private void SayLinks()
        {
            int shown = _lstLinks.Items.Count;

            if (_links.Count == 0)
            {
                _lblLinks.Text = "No linked models loaded.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtLinks.Text))
            {
                _lblLinks.Text = _ticked.Count + " of " + _links.Count + " ticked.";
                return;
            }

            int hidden = _links.Count(l => _ticked.Contains(l.Id) && !VENTLINKFILTER.Matches(l.Label, _txtLinks.Text));

            _lblLinks.Text = VENTLINKFILTER.Said(shown, _links.Count, hidden);
        }

        private void LinkChecked(object sender, ItemCheckEventArgs e)
        {
            if (_filling)
                return;

            var link = _lstLinks.Items[e.Index] as TAGPLAN.Link;

            if (link == null)
                return;

            if (e.NewValue == CheckState.Checked)
                _ticked.Add(link.Id);
            else
                _ticked.Remove(link.Id);

            // ItemCheck comes before the box changes: say it after
            BeginInvoke(new Action(() =>
            {
                SayLinks();
                Stale();
            }));
        }

        // All / None act on the links showing
        private void TickLinks(bool on)
        {
            foreach (TAGPLAN.Link link in _links.Where(l => VENTLINKFILTER.Matches(l.Label, _txtLinks.Text)))
            {
                if (on)
                    _ticked.Add(link.Id);
                else
                    _ticked.Remove(link.Id);
            }

            ShowLinks();
            Stale();
        }

        // A check made with other links no longer says what is missing
        private void Stale()
        {
            if (_rows == null)
                return;

            _read = null;
            _rows = null;

            ShowRows();
            Say("The links changed - Check sheets again.");
        }

        private void FillTypes(long preferred)
        {
            foreach (TypeItem type in _types)
                _cmbType.Items.Add(type);

            string saved = _before == null ? null : _before.TagType;

            TypeItem pick = _types.FirstOrDefault(t => !string.IsNullOrEmpty(saved) &&
                                                       string.Equals(t.Name, saved, StringComparison.OrdinalIgnoreCase))
                            ?? _types.FirstOrDefault(t => t.Id == preferred)
                            ?? _types.FirstOrDefault();

            if (pick != null)
                _cmbType.SelectedItem = pick;
        }

        // ==================================================
        // Plans
        // ==================================================

        private void Check()
        {
            if (_ticked.Count == 0)
            {
                Say("Tick a linked model.");
                return;
            }

            Cursor = Cursors.WaitCursor;
            Say("Reading the plans on sheets...");
            _lblStatus.Refresh();

            try
            {
                ReadModel();

                ShowRows();
                WriteLog(null, null);

                int missing = TAGPLAN.Missing(_rows);
                int duplicates = TAGPLAN.ToRemove(_rows).Count;

                Say(missing == 0 && duplicates == 0
                    ? "Every room shown is tagged, once."
                    : missing + " room tag(s) missing, " + duplicates + " duplicate(s).");
            }
            catch (Exception ex)
            {
                Say("The check failed: " + ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        // The plans, their tags, and the duplicates as the option says
        private void ReadModel()
        {
            _read = _check(_links.Where(l => _ticked.Contains(l.Id)).Select(l => l.Id).ToList());
            _rows = TAGPLAN.Check(_read.Views, _read.Tagged);

            TAGPLAN.Doubles(_rows, _read.Tags, _chkAnyType.Checked);
        }

        // Any tag type changed: the same tags, counted again
        private void Recount()
        {
            if (_rows == null)
                return;

            TAGPLAN.Doubles(_rows, _read.Tags, _chkAnyType.Checked);

            ShowRows();
        }

        private void ShowRows()
        {
            _filling = true;

            try
            {
                _grid.Rows.Clear();

                foreach (TAGPLAN.Row row in _rows ?? new List<TAGPLAN.Row>())
                {
                    TAGPLAN.View v = row.View;
                    bool any = row.Fixable;

                    int index = _grid.Rows.Add(
                        any && !_unticked.Contains(v.Id),
                        v.SheetNumber,
                        v.SheetName,
                        v.Name + (v.Ceiling ? "  (ceiling)" : string.Empty),
                        row.Shown,
                        row.Tagged,
                        row.Missing.Count,
                        row.Duplicates.Count);

                    DataGridViewRow line = _grid.Rows[index];

                    line.Tag = row;

                    if (!any)
                    {
                        line.Cells[0].ReadOnly = true;
                        line.DefaultCellStyle.ForeColor = Muted;
                    }
                    else
                    {
                        foreach (int column in new[] { 6, 7 })
                        {
                            if (!0.Equals(line.Cells[column].Value))
                            {
                                line.Cells[column].Style.ForeColor = Warn;
                                line.Cells[column].Style.Font = Bold;
                            }
                        }
                    }

                    if (v.Notes.Count > 0)
                        line.Cells[3].ToolTipText = string.Join(Environment.NewLine, v.Notes.ToArray());
                }
            }
            finally
            {
                _filling = false;
            }

            FilterRows();
            ShowPicked();
        }

        // The search only hides rows: ticks stand
        private void FilterRows()
        {
            int shown = 0;

            foreach (DataGridViewRow line in _grid.Rows)
            {
                var row = line.Tag as TAGPLAN.Row;

                bool match = row != null && VENTLINKFILTER.Matches(
                    row.View.SheetNumber + " " + row.View.SheetName + " " + row.View.Name, _txtSheets.Text);

                if (!match && line.Selected)
                    line.Selected = false;

                // A row cannot be hidden while it is the current one
                if (!match && _grid.CurrentRow == line)
                    _grid.CurrentCell = null;

                line.Visible = match;

                if (match)
                    shown++;
            }

            SayRows(shown);
        }

        private void SayRows(int shown)
        {
            if (_rows == null)
            {
                _lblRows.Text = "Tick the linked models, then Check sheets.";
                _btnTag.Enabled = false;
                _btnRemove.Enabled = false;
                return;
            }

            int plans = _rows.Count;
            int ticked = Ticked().Count;
            int tags = TAGPLAN.Missing(Ticked());
            int duplicates = TAGPLAN.ToRemove(Ticked()).Count;

            string said = plans == 0
                ? "No floor or ceiling plans on sheets."
                : (shown == plans ? plans + " plan(s) on " + _read.Sheets + " sheet(s)." : "Showing " + shown + " of " + plans + " plan(s).") +
                  " " + ticked + " ticked, " + tags + " tag(s) to place, " + duplicates + " duplicate(s) to remove.";

            _lblRows.Text = said;
            _btnTag.Enabled = tags > 0 && _types.Count > 0;
            _btnRemove.Enabled = duplicates > 0;
        }

        private void RowTicked(object sender, DataGridViewCellEventArgs e)
        {
            if (_filling || e.RowIndex < 0 || e.ColumnIndex != 0)
                return;

            DataGridViewRow line = _grid.Rows[e.RowIndex];
            var row = line.Tag as TAGPLAN.Row;

            if (row == null)
                return;

            if (true.Equals(line.Cells[0].Value))
                _unticked.Remove(row.View.Id);
            else
                _unticked.Add(row.View.Id);

            SayRows(_grid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible));
        }

        // Tick all / Untick all act on the rows showing with something to fix
        private void TickRows(bool on)
        {
            _filling = true;

            try
            {
                foreach (DataGridViewRow line in _grid.Rows)
                {
                    var row = line.Tag as TAGPLAN.Row;

                    if (row == null || !line.Visible || !row.Fixable)
                        continue;

                    line.Cells[0].Value = on;

                    if (on)
                        _unticked.Remove(row.View.Id);
                    else
                        _unticked.Add(row.View.Id);
                }
            }
            finally
            {
                _filling = false;
            }

            SayRows(_grid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible));
        }

        // Ticked rows - hidden by the search or not
        private List<TAGPLAN.Row> Ticked()
        {
            return (_rows ?? new List<TAGPLAN.Row>())
                .Where(r => r.Fixable && !_unticked.Contains(r.View.Id))
                .ToList();
        }

        private void ShowPicked()
        {
            _lstMissing.BeginUpdate();
            _lstMissing.Items.Clear();

            try
            {
                DataGridViewRow line = _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : null;
                var row = line == null ? null : line.Tag as TAGPLAN.Row;

                if (row == null)
                {
                    _lblPicked.Text = Picked;
                    return;
                }

                _lblPicked.Text = "Missing and duplicate tags in " + row.View.Name +
                                  (TAGPLAN.OwnerOf(row.View) != row.View.Id ? " - a dependent view: tags go in its primary" : string.Empty);

                foreach (string note in row.View.Notes)
                    _lstMissing.Items.Add("!  " + note);

                if (row.View.OutsideCrop > 0)
                    _lstMissing.Items.Add("   " + row.View.OutsideCrop + " room(s) outside the crop - left alone");

                foreach (TAGPLAN.Room room in row.Missing)
                {
                    TAGPLAN.Link link = _links.FirstOrDefault(l => l.Id == room.Link);

                    _lstMissing.Items.Add(room.Number + "   " + room.Name + "      " + (link == null ? string.Empty : link.Label));
                }

                foreach (IGrouping<string, TAGPLAN.Tag> room in _read.Tags
                             .Where(t => row.Duplicates.Contains(t.Id))
                             .GroupBy(t => t.Key))
                {
                    TAGPLAN.Room shown = _read.Views.SelectMany(v => v.Shown).FirstOrDefault(r => r.Key == room.Key);
                    TAGPLAN.Link link = _links.FirstOrDefault(l => l.Id == room.First().Link);

                    _lstMissing.Items.Add((shown == null ? "room " + room.Key : shown.Number + "   " + shown.Name) +
                                          "      " + (link == null ? string.Empty : link.Label) +
                                          "   - " + room.Count() + " duplicate tag(s)");
                }

                if (!row.Fixable && row.View.Notes.Count == 0)
                    _lstMissing.Items.Add(row.Shown == 0 ? "No room of the ticked links shows here." : "Every room shown is tagged, once.");
            }
            finally
            {
                _lstMissing.EndUpdate();
            }
        }

        // ==================================================
        // Tagging
        // ==================================================

        private void TagMissing()
        {
            var type = _cmbType.SelectedItem as TypeItem;

            if (_rows == null || type == null)
                return;

            Dictionary<long, List<TAGPLAN.Room>> plan = TAGPLAN.ToTag(Ticked());

            int tags = plan.Sum(p => p.Value.Count);

            if (tags == 0)
            {
                Say("Nothing to tag.");
                return;
            }

            string question = "Place " + tags + " room tag(s) in " + plan.Count + " view(s)?";

            if (!Ask(question))
                return;

            Cursor = Cursors.WaitCursor;
            Say("Placing the tags...");
            _lblStatus.Refresh();

            TAGPLAN.Done done;

            try
            {
                done = _tag(plan, type.Id);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Say("Tagging failed: " + ex.Message);
                return;
            }

            // What is missing NOW, read from the model
            try
            {
                ReadModel();
            }
            catch
            {
                _read = null;
                _rows = null;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            ShowRows();
            WriteLog(done, null);

            string said = "Placed " + done.Placed + " room tag(s).";

            if (done.Placed < done.Asked)
                said += " " + (done.Asked - done.Placed) + " not placed - see the log.";

            if (!string.IsNullOrEmpty(done.Error))
                said = done.Error;

            Say(said);
        }

        // THE DUPLICATES of the ticked plans, each once (TAGPLAN.Doubles has
        // which stay)
        private void RemoveDuplicates()
        {
            if (_rows == null)
                return;

            List<long> ids = TAGPLAN.ToRemove(Ticked());

            if (ids.Count == 0)
            {
                Say("No duplicates to remove.");
                return;
            }

            if (!Ask("Delete " + ids.Count + " duplicate room tag(s)? Every plan keeps a tag of each room."))
                return;

            Cursor = Cursors.WaitCursor;
            Say("Removing the duplicates...");
            _lblStatus.Refresh();

            TAGPLAN.Removal removal;

            try
            {
                removal = _remove(ids);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Say("Removing failed: " + ex.Message);
                return;
            }

            try
            {
                ReadModel();
            }
            catch
            {
                _read = null;
                _rows = null;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            ShowRows();
            WriteLog(null, removal);

            string said = "Removed " + removal.Removed + " duplicate tag(s).";

            if (removal.Removed < removal.Asked)
                said += " " + (removal.Asked - removal.Removed) + " not removed - see the log.";

            if (!string.IsNullOrEmpty(removal.Error))
                said = removal.Error;

            Say(said);
        }

        private bool Ask(string question)
        {
            return Confirm != null
                ? Confirm(question)
                : MessageBox.Show(this, question, "Room tags", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        // ==================================================
        // Log, status, settings
        // ==================================================

        private void WriteLog(TAGPLAN.Done done, TAGPLAN.Removal removal)
        {
            try
            {
                File.WriteAllText(TAGPLAN.LogPath, TAGPLAN.Report(
                    _title,
                    _links.Where(l => _ticked.Contains(l.Id)).Select(l => l.Label),
                    _read,
                    _rows,
                    done,
                    removal));
            }
            catch
            {
                // a log that cannot be written is not a reason to stop
            }
        }

        private void OpenLog()
        {
            try
            {
                if (File.Exists(TAGPLAN.LogPath))
                    Process.Start(new ProcessStartInfo(TAGPLAN.LogPath) { UseShellExecute = true });
                else
                    Say("No log yet - Check sheets first.");
            }
            catch (Exception ex)
            {
                Say("Could not open the log: " + ex.Message);
            }
        }

        private void Say(string text)
        {
            _lblStatus.Text = text ?? string.Empty;
        }

        private void Remember()
        {
            var type = _cmbType.SelectedItem as TypeItem;

            TAGPLAN.Keep(new TAGPLAN.Choice
            {
                Model = _model,
                Links = TAGPLAN.Saved(_links, _ticked, _before),
                TagType = type == null ? string.Empty : type.Name
            });
        }
    }
}
