using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ATKINSMEC
{
    public partial class INSULATIONForm : System.Windows.Forms.Form
    {
        private readonly Document _doc;

        private List<string> _insulationTypeNames = new List<string>();

        public INSULATIONForm(Document doc)
        {
            InitializeComponent();

            FORMSIZE.Remember(this);

            _doc = doc;
        }

        private void INSULATIONForm_Load(object sender, EventArgs e)
        {
            INSULATIONSETTINGS.Reload();

            chkEnabled.Checked = INSULATIONSETTINGS.Enabled;

            LoadInsulationTypes();
            LoadSystemTypes();

            lblPath.Text = "Rules file:\r\n" + INSULATIONSETTINGS.SettingsPath;
        }

        private void LoadInsulationTypes()
        {
            _insulationTypeNames =
                new FilteredElementCollector(_doc)
                .OfClass(typeof(DuctInsulationType))
                .Cast<DuctInsulationType>()
                .Select(t => t.Name)
                .OrderBy(name => name)
                .ToList();

            if (_insulationTypeNames.Count == 0)
            {
                chkEnabled.Checked = false;
                chkEnabled.Enabled = false;

                MessageBox.Show(
                    "This model has no duct insulation types, so insulation " +
                    "cannot be created.\r\n\r\nLoad or create one first.",
                    "DUCT INSULATION");
            }
        }

        private void LoadSystemTypes()
        {
            var systemTypes =
                new FilteredElementCollector(_doc)
                .OfClass(typeof(MechanicalSystemType))
                .Cast<MechanicalSystemType>()
                .OrderBy(t => t.Name)
                .ToList();

            // The type column offers the model's insulation types, plus a
            // blank for system types that are drawn bare
            colInsulationType.Items.Clear();
            colInsulationType.Items.Add(string.Empty);

            foreach (string name in _insulationTypeNames)
                colInsulationType.Items.Add(name);

            // Saved names that are not in this model must still be valid
            // values for the cell, or the grid rejects them
            foreach (var saved in INSULATIONSETTINGS.RulesBySystemType)
            {
                string name = saved.Value.InsulationTypeName;

                if (!string.IsNullOrEmpty(name) &&
                    !colInsulationType.Items.Contains(name))
                {
                    colInsulationType.Items.Add(name);
                }
            }

            dgvRules.Rows.Clear();

            foreach (MechanicalSystemType systemType in systemTypes)
                AddRule(systemType.Name, false);

            // Rules saved for system types that this model does not have
            // are kept, so switching projects does not lose them
            foreach (var pair in INSULATIONSETTINGS.RulesBySystemType)
            {
                bool present = systemTypes.Any(
                    t => string.Equals(
                        t.Name,
                        pair.Key,
                        StringComparison.OrdinalIgnoreCase));

                if (!present)
                    AddRule(pair.Key, true);
            }

            lblRulesInfo.Text =
                $"{systemTypes.Count} system type(s) in this model. " +
                "A system type is insulated only when it has both a " +
                "thickness above 0 and an insulation type. " +
                "Greyed rows are saved rules for system types not in this model.";
        }

        private void AddRule(string systemTypeName, bool greyed)
        {
            INSULATIONSETTINGS.Rule rule =
                INSULATIONSETTINGS.RuleFor(systemTypeName);

            double thickness = rule == null ? 0 : rule.ThicknessMm;

            string insulationType = rule == null
                ? string.Empty
                : (rule.InsulationTypeName ?? string.Empty);

            int row = dgvRules.Rows.Add(
                systemTypeName,
                thickness.ToString(CultureInfo.InvariantCulture),
                insulationType);

            if (greyed)
            {
                dgvRules.Rows[row].DefaultCellStyle.ForeColor =
                    System.Drawing.SystemColors.GrayText;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            var rules =
                new Dictionary<string, INSULATIONSETTINGS.Rule>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in dgvRules.Rows)
            {
                string systemType = Convert.ToString(row.Cells[0].Value);

                if (string.IsNullOrEmpty(systemType))
                    continue;

                string text = Convert.ToString(row.Cells[1].Value);

                double thickness;

                if (string.IsNullOrEmpty(text))
                {
                    thickness = 0;
                }
                else if (!double.TryParse(
                             text.Trim(),
                             NumberStyles.Any,
                             CultureInfo.InvariantCulture,
                             out thickness))
                {
                    MessageBox.Show(
                        $"\"{text}\" is not a valid thickness for " +
                        $"{systemType}.",
                        "DUCT INSULATION");

                    return;
                }

                if (thickness < 0)
                {
                    MessageBox.Show(
                        $"The thickness for {systemType} cannot be negative.",
                        "DUCT INSULATION");

                    return;
                }

                string insulationType =
                    (Convert.ToString(row.Cells[2].Value) ?? string.Empty).Trim();

                // A thickness with no type would silently insulate nothing
                if (thickness > 0 && insulationType.Length == 0)
                {
                    MessageBox.Show(
                        $"{systemType} has a thickness but no insulation " +
                        "type.\r\n\r\nPick a type for it, or set the " +
                        "thickness to 0 to leave that system type bare.",
                        "DUCT INSULATION");

                    return;
                }

                rules[systemType.Trim()] = new INSULATIONSETTINGS.Rule
                {
                    ThicknessMm = thickness,
                    InsulationTypeName = insulationType
                };
            }

            INSULATIONSETTINGS.RulesBySystemType = rules;
            INSULATIONSETTINGS.Enabled = chkEnabled.Checked;

            try
            {
                INSULATIONSETTINGS.Save();

                int active = rules.Count(r => r.Value.ThicknessMm > 0);

                MessageBox.Show(
                    chkEnabled.Checked
                        ? $"Saved. New ducts on {active} system type(s) will " +
                          "be insulated as they are drawn."
                        : "Saved. Automatic insulation is switched off.",
                    "DUCT INSULATION");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not save the rules:\r\n\r\n" + ex.Message,
                    "DUCT INSULATION");
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
