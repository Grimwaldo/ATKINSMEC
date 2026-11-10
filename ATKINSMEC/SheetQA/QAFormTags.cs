using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // The Tags checker tab (user, 2026-11-10: "another window in the
    // QAcheck addin called tags checker, where it checks for annotations
    // or tags clashing or overlapping each other"). A check page like the
    // others - scope, checks, findings, and the buttons along the bottom
    // (Show, Mark, Export) - so it is a Page, run through QAEVENT.Run.
    //
    // Built in code rather than in the designer, as a COPY OF THE FLOWRATE
    // PAGE: every control takes its place and anchors from its twin there,
    // read when the window loads - after WinForms has scaled the form - so
    // it lines up at any DPI without a second set of designer numbers.
    public partial class QAForm
    {
        private Page _clash;
        private TabPage _tabClash;
        private NumericUpDown _numClashMm;

        private void MakeClashPage()
        {
            _tabClash = new TabPage(QAGROUPS.Tags)
            {
                UseVisualStyleBackColor = true,
                Size = tabFlow.Size
            };

            // 1. What to check - the sheets, as on the flowrate page
            GroupBox scope = Twin(_tabClash, grpScopeFlow, new GroupBox { TabStop = false });

            RadioButton all = Twin(scope, rbAllFlow, new RadioButton { UseVisualStyleBackColor = true });
            RadioButton picked = Twin(scope, rbPickedFlow, new RadioButton { UseVisualStyleBackColor = true });
            RadioButton active = Twin(scope, rbActiveFlow, new RadioButton { UseVisualStyleBackColor = true });

            all.Checked = true;

            CheckedListBox sheets = Twin(scope, clbSheetsFlow, new CheckedListBox
            {
                CheckOnClick = true,
                Enabled = false,
                FormattingEnabled = true,
                IntegralHeight = false
            });

            Button tickAll = Twin(scope, btnAllSheetsFlow, new Button { Enabled = false, UseVisualStyleBackColor = true });
            Button tickNone = Twin(scope, btnNoSheetsFlow, new Button { Enabled = false, UseVisualStyleBackColor = true });

            // 2. Checks to run, and how far in counts as overlapping
            GroupBox checks = Twin(_tabClash, grpChecksFlow, new GroupBox { TabStop = false });

            CheckedListBox list = Twin(checks, clbFlow, new CheckedListBox
            {
                CheckOnClick = true,
                FormattingEnabled = true,
                IntegralHeight = false
            });

            Button run = Twin(checks, btnRunFlow, new Button { UseVisualStyleBackColor = true });

            Label info = Twin(checks, lblFlowInfo, new Label { ForeColor = SystemColors.GrayText, Text = string.Empty });

            ToleranceRow(checks, run);

            // 3. What was found
            GroupBox found = Twin(_tabClash, grpFindingsFlow, new GroupBox { TabStop = false });

            DataGridView table = Twin(found, dgvFlow, new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = true,
                BackgroundColor = SystemColors.Window,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                MultiSelect = true,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            });

            Label summary = Twin(found, lblSummaryFlow, new Label { Text = string.Empty });

            all.CheckedChanged += (s, e) => Scope_CheckedChanged(_clash);
            picked.CheckedChanged += (s, e) => Scope_CheckedChanged(_clash);
            active.CheckedChanged += (s, e) => Scope_CheckedChanged(_clash);

            tickAll.Click += (s, e) => TickSheets(_clash, true);
            tickNone.Click += (s, e) => TickSheets(_clash, false);

            list.SelectedIndexChanged += (s, e) => Explain(_clash);
            run.Click += (s, e) => Run(_clash);

            _clash = new Page
            {
                Group = QAGROUPS.Tags,
                All = all,
                Picked = picked,
                Active = active,
                Picks = sheets,
                TickAll = tickAll,
                TickNone = tickNone,
                List = list,
                Info = info,
                Table = table,
                Summary = summary,

                // Mark colours the two tags in the view; live markers would
                // measure every tag in scope again on every change
                CanWatch = false
            };

            tabChecks.TabPages.Add(_tabClash);
        }

        // "Overlapping by more than [0.5] mm on paper", beside Run
        private void ToleranceRow(GroupBox checks, Button run)
        {
            // How much the form was scaled by: the run button is 30 high
            // in the designer
            float k = run.Height / 30f;

            var before = new Label { AutoSize = true, Text = "Overlapping by more than" };

            checks.Controls.Add(before);

            before.Location = new Point(
                run.Right + (int)(24 * k),
                run.Top + (run.Height - before.PreferredHeight) / 2);

            _numClashMm = new NumericUpDown
            {
                DecimalPlaces = 1,
                Increment = 0.1M,
                Minimum = 0M,
                Maximum = 20M,
                Value = 0.5M,
                Size = numGridTol.Size
            };

            checks.Controls.Add(_numClashMm);

            _numClashMm.Location = new Point(
                before.Left + before.PreferredWidth + (int)(4 * k),
                run.Top + (run.Height - _numClashMm.Height) / 2);

            var after = new Label { AutoSize = true, Text = "mm on paper" };

            checks.Controls.Add(after);

            after.Location = new Point(
                _numClashMm.Right + (int)(6 * k),
                run.Top + (run.Height - after.PreferredHeight) / 2);
        }

        // A control in the place of its twin on the flowrate page: same
        // caption, bounds and anchors, in the matching parent - added
        // first and placed after, as the designer does, so its anchors
        // are worked out against a parent already the twin's size
        private static T Twin<T>(Control parent, Control twin, T control) where T : Control
        {
            // Captions only: a list's Text is its selected item
            if (control is ButtonBase || control is GroupBox)
                control.Text = twin.Text;

            parent.Controls.Add(control);

            control.Bounds = twin.Bounds;
            control.Anchor = twin.Anchor;
            control.TabIndex = twin.TabIndex;

            return control;
        }
    }
}
