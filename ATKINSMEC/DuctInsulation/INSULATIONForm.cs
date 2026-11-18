using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // THE INSULATOR WINDOW: two tabs, Ducts and Pipes, each with its own
    // on/off tick, an accessories tick and a thickness + insulation type
    // per system type. Revit-free, built in code: INSULATIONCommand hands
    // in this model's system types and insulation types.
    public class INSULATIONForm : Form
    {
        // What the model has, for one tab
        public class Model
        {
            public List<string> SystemTypes = new List<string>();
            public List<string> InsulationTypes = new List<string>();
        }

        private class Side
        {
            public INSULATIONSETTINGS Settings;
            public Model Model;
            public string Plural;      // "ducts"
            public TabPage Page;
            public CheckBox Enabled, Accessories;
            public DataGridView Grid;
        }

        private readonly Side _ducts, _pipes;
        private TabControl _tabs;

        // A message box unless tests\insulatortest answers instead
        public Action<string> Say = text => MessageBox.Show(text, "INSULATOR");

        public INSULATIONForm(Model ducts, Model pipes)
        {
            INSULATIONSETTINGS.Ducts.Reload();
            INSULATIONSETTINGS.Pipes.Reload();

            _ducts = new Side { Settings = INSULATIONSETTINGS.Ducts, Model = ducts ?? new Model(), Plural = "ducts" };
            _pipes = new Side { Settings = INSULATIONSETTINGS.Pipes, Model = pipes ?? new Model(), Plural = "pipes" };

            Build();

            FORMSIZE.Remember(this);
        }

        private void Build()
        {
            SuspendLayout();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F);
            Text = "INSULATOR";
            ClientSize = new Size(720, 520);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(Page(_ducts, "Ducts", "duct"));
            _tabs.TabPages.Add(Page(_pipes, "Pipes", "pipe"));

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, RowCount = 1, Padding = new Padding(6) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            bottom.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 6, 3, 0),
                Text = "Rules: " + INSULATIONSETTINGS.Folder
            }, 0, 0);

            var save = new Button { Text = "Save rules", Width = 120, Height = 30 };
            save.Click += (s, e) => SaveRules();
            bottom.Controls.Add(save, 1, 0);

            var close = new Button { Text = "Close", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            close.Click += (s, e) => Close();
            bottom.Controls.Add(close, 2, 0);

            CancelButton = close;

            Controls.Add(_tabs);
            Controls.Add(bottom);

            ResumeLayout(false);
            PerformLayout();
        }

        private TabPage Page(Side side, string title, string noun)
        {
            side.Page = new TabPage(title) { Padding = new Padding(8), UseVisualStyleBackColor = true };

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };

            // Without it the one column takes the width of its widest
            // control - the note's single line - and the grid runs off
            // the tab
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            side.Enabled = new CheckBox
            {
                AutoSize = true,
                Text = "Insulate " + side.Plural + " and " + noun + " fittings as they are drawn",
                Checked = side.Settings.Enabled
            };

            side.Accessories = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(21, 3, 3, 6),
                Text = "Also insulate " + noun + " accessories",
                Checked = side.Settings.Accessories
            };

            side.Enabled.CheckedChanged += (s, e) => side.Accessories.Enabled = side.Enabled.Checked;
            side.Accessories.Enabled = side.Enabled.Checked;

            side.Grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
            };

            side.Grid.DataError += (s, e) => e.ThrowException = false;

            side.Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = char.ToUpper(noun[0]) + noun.Substring(1) + " system type", ReadOnly = true, FillWeight = 45 });
            side.Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Thickness (mm)", FillWeight = 18 });

            var types = new DataGridViewComboBoxColumn { HeaderText = "Insulation type", FillWeight = 37, DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox };
            types.Items.Add(string.Empty);

            foreach (string name in side.Model.InsulationTypes)
                types.Items.Add(name);

            // Saved names not in this model must still be valid values
            foreach (INSULATIONSETTINGS.Rule rule in side.Settings.RulesBySystemType.Values)
            {
                if (!string.IsNullOrEmpty(rule.InsulationTypeName) && !types.Items.Contains(rule.InsulationTypeName))
                    types.Items.Add(rule.InsulationTypeName);
            }

            side.Grid.Columns.Add(types);

            foreach (string systemType in side.Model.SystemTypes)
                AddRow(side, systemType, false);

            // Rules saved for system types this model has not got are kept
            foreach (string saved in side.Settings.RulesBySystemType.Keys.ToList())
            {
                if (!side.Model.SystemTypes.Any(t => string.Equals(t, saved, StringComparison.OrdinalIgnoreCase)))
                    AddRow(side, saved, true);
            }

            string note = side.Model.SystemTypes.Count + " " + noun + " system type(s) in this model. " +
                          "Insulated when it has a thickness above 0 and an insulation type. Grey: saved, not in this model.";

            // No insulation types: nothing can be insulated here, and the
            // saved on/off is left as it was rather than switched off for
            // every model
            if (side.Model.InsulationTypes.Count == 0)
            {
                side.Enabled.Enabled = false;
                side.Accessories.Enabled = false;
                note = "This model has no " + noun + " insulation types - load or create one first. " + note;
            }

            layout.Controls.Add(side.Enabled, 0, 0);
            layout.Controls.Add(side.Accessories, 0, 1);
            layout.Controls.Add(side.Grid, 0, 2);
            // Anchored both sides, an AutoSize label wraps to the column
            layout.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 6, 3, 0), Text = note }, 0, 3);

            side.Page.Controls.Add(layout);

            return side.Page;
        }

        private static void AddRow(Side side, string systemType, bool greyed)
        {
            INSULATIONSETTINGS.Rule rule = side.Settings.RuleFor(systemType);

            int row = side.Grid.Rows.Add(
                systemType,
                (rule == null ? 0 : rule.ThicknessMm).ToString(CultureInfo.InvariantCulture),
                rule == null ? string.Empty : (rule.InsulationTypeName ?? string.Empty));

            if (greyed)
                side.Grid.Rows[row].DefaultCellStyle.ForeColor = SystemColors.GrayText;
        }

        // Reads one tab's grid; null (and the tab shown) when a row is wrong
        private Dictionary<string, INSULATIONSETTINGS.Rule> Read(Side side)
        {
            side.Grid.EndEdit();

            var rules = new Dictionary<string, INSULATIONSETTINGS.Rule>(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in side.Grid.Rows)
            {
                string systemType = (Convert.ToString(row.Cells[0].Value) ?? string.Empty).Trim();

                if (systemType.Length == 0)
                    continue;

                string text = (Convert.ToString(row.Cells[1].Value) ?? string.Empty).Trim();
                string type = (Convert.ToString(row.Cells[2].Value) ?? string.Empty).Trim();

                double thickness = 0;

                string problem = null;

                if (text.Length > 0 && !double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out thickness))
                    problem = "\"" + text + "\" is not a valid thickness for " + systemType + ".";
                else if (thickness < 0)
                    problem = "The thickness for " + systemType + " cannot be negative.";
                else if (thickness > 0 && type.Length == 0)
                    problem = systemType + " has a thickness but no insulation type.\r\n\r\nPick a type, or set the thickness to 0.";

                if (problem != null)
                {
                    _tabs.SelectedTab = side.Page;
                    side.Grid.CurrentCell = row.Cells[problem.Contains("no insulation type") ? 2 : 1];
                    Say(problem);
                    return null;
                }

                rules[systemType] = new INSULATIONSETTINGS.Rule { ThicknessMm = thickness, InsulationTypeName = type };
            }

            return rules;
        }

        public bool SaveRules()
        {
            Dictionary<string, INSULATIONSETTINGS.Rule> ducts = Read(_ducts);

            if (ducts == null)
                return false;

            Dictionary<string, INSULATIONSETTINGS.Rule> pipes = Read(_pipes);

            if (pipes == null)
                return false;

            try
            {
                Store(_ducts, ducts);
                Store(_pipes, pipes);
            }
            catch (Exception ex)
            {
                Say("Could not save the rules:\r\n\r\n" + ex.Message);
                return false;
            }

            Say("Saved.\r\n\r\nDucts: " + Summary(_ducts) + "\r\nPipes: " + Summary(_pipes));

            return true;
        }

        private static void Store(Side side, Dictionary<string, INSULATIONSETTINGS.Rule> rules)
        {
            side.Settings.RulesBySystemType = rules;
            side.Settings.Enabled = side.Enabled.Checked;
            side.Settings.Accessories = side.Accessories.Checked;
            side.Settings.Save();
        }

        private static string Summary(Side side)
        {
            if (!side.Settings.Enabled)
                return "off";

            int active = side.Settings.RulesBySystemType.Values.Count(r => r.ThicknessMm > 0 && !string.IsNullOrEmpty(r.InsulationTypeName));

            return "insulated as drawn on " + active + " system type(s)" +
                   (side.Settings.Accessories ? ", accessories too" : ", not accessories");
        }
    }
}
