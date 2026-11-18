using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// INSULATOR (user, 2026-11-18: "the duct insulation addin should be called
// insulator, and it should also work for pipes aswell ... two tabs ... a
// checkbox in duct that gives option to insulate duct accessory and same in
// pipe"). The rules files - DUCT INSULATION's own file read unchanged,
// accessories on for ducts and off for pipes until ticked - and the real
// window, off-screen: two tabs, a row per system type, the ticks, what Save
// writes, a wrong row refused on its own tab.
//
//   insulatortest                      the checks
//   insulatortest preview <folder> [1.5]   renders both tabs - LOOK at them
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string folder = Path.Combine(Path.GetTempPath(), "insulatortest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        INSULATIONSETTINGS.Folder = folder;

        Application.EnableVisualStyles();

        try
        {
            if (args.Length > 1 && args[0] == "preview")
            {
                float scale = 1f;

                if (args.Length > 2)
                    float.TryParse(args[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale);

                return Preview(args[1], scale);
            }

            Files(folder);
            Window(folder);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch { }
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "All insulator checks passed." : _failed + " FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // ==================================================
    // The rules files
    // ==================================================

    private static void Files(string folder)
    {
        Console.WriteLine("Rules files");

        INSULATIONSETTINGS ducts = INSULATIONSETTINGS.Ducts;
        INSULATIONSETTINGS pipes = INSULATIONSETTINGS.Pipes;

        Check("the duct rules are DUCT INSULATION's own file", Path.GetFileName(ducts.SettingsPath) == "duct_insulation.txt");
        Check("the pipe rules have a file of their own", Path.GetFileName(pipes.SettingsPath) == "pipe_insulation.txt");

        // Nothing saved yet
        ducts.Reload();
        pipes.Reload();

        Check("no file: ducts off", !ducts.Enabled);
        Check("no file: duct accessories ticked (as DUCT INSULATION did)", ducts.Accessories);
        Check("no file: pipe accessories unticked", !pipes.Accessories);
        Check("no file: no rules", ducts.RulesBySystemType.Count == 0 && pipes.RulesBySystemType.Count == 0);

        // A file DUCT INSULATION wrote before the type went per system type
        File.WriteAllText(ducts.SettingsPath,
            "# ATKINSMEC duct insulation rules\r\n" +
            "enabled=1\r\n" +
            "type=Fibreglass 25\r\n" +
            "Supply Air\t25\r\n" +
            "Return Air\t0\r\n" +
            "Fresh Air\t40\tElastomeric\r\n");

        ducts.Reload();

        Check("old file: on", ducts.Enabled);
        Check("old file: accessories still insulated (no line = ticked)", ducts.Accessories);
        Check("old file: Covers a duct and a duct accessory", ducts.Covers(false) && ducts.Covers(true));
        Check("old file: the one type folded into a rule with none", ducts.TypeNameFor("Supply Air") == "Fibreglass 25");
        Check("old file: a rule's own type kept", ducts.TypeNameFor("Fresh Air") == "Elastomeric");
        Check("old file: thickness read", ducts.RuleFor("fresh air") != null && ducts.RuleFor("fresh air").ThicknessMm == 40);
        Check("old file: an unknown system type has no rule", ducts.RuleFor("Kitchen Extract") == null && ducts.TypeNameFor("Kitchen Extract") == "");

        // Accessories off, saved and read back
        ducts.Accessories = false;
        ducts.Save();
        ducts.Reload();

        Check("duct accessories unticked survive a save", !ducts.Accessories && ducts.Enabled);
        Check("Covers a duct, not a duct accessory", ducts.Covers(false) && !ducts.Covers(true));
        Check("the legacy type= line is not written again", !File.ReadAllText(ducts.SettingsPath).Contains("type="));
        Check("the folded type is written on the rule", ducts.TypeNameFor("Supply Air") == "Fibreglass 25");

        // Pipes, on their own
        pipes.Enabled = true;
        pipes.Accessories = true;
        pipes.RulesBySystemType = new Dictionary<string, INSULATIONSETTINGS.Rule>(StringComparer.OrdinalIgnoreCase)
        {
            { "Chilled Water Supply", new INSULATIONSETTINGS.Rule { ThicknessMm = 32.5, InsulationTypeName = "Phenolic" } }
        };
        pipes.Save();
        pipes.Reload();

        Check("pipe rules round trip", pipes.Enabled && pipes.Accessories && pipes.RuleFor("Chilled Water Supply").ThicknessMm == 32.5 && pipes.TypeNameFor("chilled water supply") == "Phenolic");
        Check("pipes saved without touching the duct file", ducts.RuleFor("Supply Air") != null && !File.ReadAllText(ducts.SettingsPath).Contains("Chilled"));

        pipes.Enabled = false;
        Check("off covers nothing", !pipes.Covers(false) && !pipes.Covers(true));

        File.WriteAllText(pipes.SettingsPath, "enabled=1\r\naccessories=1\r\nnot a rule\r\nCHW\tabc\tX\r\nLTHW\t20\r\n");
        pipes.Reload();
        Check("a damaged line is skipped, the rest read", pipes.Enabled && pipes.RuleFor("CHW") == null && pipes.RuleFor("LTHW") != null && pipes.TypeNameFor("LTHW") == "");

        File.Delete(ducts.SettingsPath);
        File.Delete(pipes.SettingsPath);
        Console.WriteLine();
    }

    // ==================================================
    // The real window, off-screen
    // ==================================================

    private static IEnumerable<T> All<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T)
                yield return (T)c;

            foreach (T inside in All<T>(c))
                yield return inside;
        }
    }

    private static CheckBox Tick(TabPage page, string starts)
    {
        return All<CheckBox>(page).First(c => c.Text.StartsWith(starts));
    }

    private static void Settle()
    {
        for (int i = 0; i < 5; i++)
            Application.DoEvents();
    }

    private static INSULATIONForm.Model Ducts()
    {
        return new INSULATIONForm.Model
        {
            SystemTypes = new List<string> { "Exhaust Air", "Fresh Air", "Return Air", "Supply Air" },
            InsulationTypes = new List<string> { "Elastomeric", "Fibreglass 25" }
        };
    }

    private static INSULATIONForm.Model Pipes(bool types)
    {
        return new INSULATIONForm.Model
        {
            SystemTypes = new List<string> { "Chilled Water Return", "Chilled Water Supply", "Condensate Drain", "Domestic Cold Water" },
            InsulationTypes = types ? new List<string> { "Phenolic", "Rubber 19" } : new List<string>()
        };
    }

    private static INSULATIONForm Open(INSULATIONForm.Model ducts, INSULATIONForm.Model pipes, List<string> said)
    {
        var form = new INSULATIONForm(ducts, pipes)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-4000, -4000),
            ShowInTaskbar = false
        };

        form.Say = text => said.Add(text);
        form.Show();
        Settle();

        return form;
    }

    private static void Window(string folder)
    {
        Console.WriteLine("The window");

        INSULATIONSETTINGS ducts = INSULATIONSETTINGS.Ducts;
        INSULATIONSETTINGS pipes = INSULATIONSETTINGS.Pipes;

        // A duct rule saved for a system type this model has not got
        File.WriteAllText(ducts.SettingsPath, "enabled=1\r\nSupply Air\t25\tFibreglass 25\r\nKitchen Extract\t50\tGone Type\r\n");

        var said = new List<string>();
        INSULATIONForm form = Open(Ducts(), Pipes(true), said);

        TabControl tabs = All<TabControl>(form).Single();

        Check("titled INSULATOR", form.Text == "INSULATOR");
        Check("two tabs, Ducts then Pipes", tabs.TabPages.Count == 2 && tabs.TabPages[0].Text == "Ducts" && tabs.TabPages[1].Text == "Pipes");

        TabPage ductPage = tabs.TabPages[0], pipePage = tabs.TabPages[1];

        CheckBox ductOn = Tick(ductPage, "Insulate ducts"), ductAcc = Tick(ductPage, "Also insulate duct accessories");
        CheckBox pipeOn = Tick(pipePage, "Insulate pipes"), pipeAcc = Tick(pipePage, "Also insulate pipe accessories");

        DataGridView ductGrid = All<DataGridView>(ductPage).Single(), pipeGrid = All<DataGridView>(pipePage).Single();

        Check("ducts on, from the file", ductOn.Checked && ductOn.Enabled);
        Check("duct accessories ticked (no line in the file)", ductAcc.Checked && ductAcc.Enabled);
        Check("pipes off, pipe accessories unticked and greyed", !pipeOn.Checked && !pipeAcc.Checked && !pipeAcc.Enabled);

        Check("a row per duct system type + the saved one not in the model", ductGrid.Rows.Count == 5);
        Check("the saved one not in the model is greyed, last", (string)ductGrid.Rows[4].Cells[0].Value == "Kitchen Extract" && ductGrid.Rows[4].DefaultCellStyle.ForeColor == SystemColors.GrayText);
        Check("Supply Air shows its rule", (string)ductGrid.Rows[3].Cells[1].Value == "25" && (string)ductGrid.Rows[3].Cells[2].Value == "Fibreglass 25");
        Check("a saved type not in the model is still offered", ((DataGridViewComboBoxColumn)ductGrid.Columns[2]).Items.Contains("Gone Type"));
        Check("a row per pipe system type", pipeGrid.Rows.Count == 4 && (string)pipeGrid.Columns[0].HeaderText == "Pipe system type");
        Check("the pipe types offered", ((DataGridViewComboBoxColumn)pipeGrid.Columns[2]).Items.Contains("Phenolic"));

        pipeOn.Checked = true;
        Check("ticking pipes un-greys their accessories", pipeAcc.Enabled);

        // A thickness with no type is refused, on its own tab
        tabs.SelectedIndex = 0;
        pipeGrid.Rows[1].Cells[1].Value = "32";
        Settle();

        bool saved = form.SaveRules();

        Check("a thickness with no type: not saved", !saved && !File.Exists(pipes.SettingsPath));
        Check("... said so", said.Count == 1 && said[0].Contains("Chilled Water Supply") && said[0].Contains("no insulation type"));
        Check("... on the Pipes tab, at the type cell", tabs.SelectedIndex == 1 && pipeGrid.CurrentCell != null && pipeGrid.CurrentCell.RowIndex == 1 && pipeGrid.CurrentCell.ColumnIndex == 2);

        pipeGrid.Rows[1].Cells[2].Value = "Phenolic";
        pipeAcc.Checked = true;
        ductAcc.Checked = false;
        ductGrid.Rows[1].Cells[1].Value = "abc";
        said.Clear();

        Check("a thickness that is not a number: not saved", !form.SaveRules() && tabs.SelectedIndex == 0 && said.Count == 1 && said[0].Contains("\"abc\""));

        ductGrid.Rows[1].Cells[1].Value = "40";
        ductGrid.Rows[1].Cells[2].Value = "Elastomeric";
        said.Clear();

        Check("saved", form.SaveRules() && said.Count == 1);
        // 3: Supply Air, Fresh Air and the saved Kitchen Extract
        Check("... the summary names both", said[0].Contains("Ducts: insulated as drawn on 3 system type(s), not accessories") && said[0].Contains("Pipes: insulated as drawn on 1 system type(s), accessories too"));

        if (!said[0].Contains("on 3 system"))
            Console.WriteLine("        said: " + said[0].Replace("\r\n", " | "));

        form.Close();
        form.Dispose();

        ducts.Reload();
        pipes.Reload();

        Check("ducts written: on, accessories off, Fresh Air 40 Elastomeric", ducts.Enabled && !ducts.Accessories && ducts.RuleFor("Fresh Air").ThicknessMm == 40 && ducts.TypeNameFor("Fresh Air") == "Elastomeric");
        Check("the rule not in the model kept", ducts.TypeNameFor("Kitchen Extract") == "Gone Type" && ducts.RuleFor("Kitchen Extract").ThicknessMm == 50);
        Check("pipes written: on, accessories on, CHW supply 32 Phenolic", pipes.Enabled && pipes.Accessories && pipes.RuleFor("Chilled Water Supply").ThicknessMm == 32 && pipes.TypeNameFor("Chilled Water Supply") == "Phenolic");
        Check("a pipe system type left at 0 is saved bare", pipes.RuleFor("Condensate Drain") != null && pipes.RuleFor("Condensate Drain").ThicknessMm == 0);

        // A model with no pipe insulation types: the pipe ticks cannot be
        // changed there, and saving the ducts leaves pipes on for others
        said.Clear();
        form = Open(Ducts(), Pipes(false), said);
        tabs = All<TabControl>(form).Single();
        pipePage = tabs.TabPages[1];
        pipeOn = Tick(pipePage, "Insulate pipes");
        pipeAcc = Tick(pipePage, "Also insulate pipe accessories");

        Check("no pipe insulation types: the pipe ticks greyed, as saved", !pipeOn.Enabled && pipeOn.Checked && !pipeAcc.Enabled && pipeAcc.Checked);
        Check("... and said on the tab", All<Label>(pipePage).Any(l => l.Text.Contains("no pipe insulation types")));
        Check("... saving keeps pipes on", form.SaveRules() && INSULATIONSETTINGS.Pipes.Enabled && INSULATIONSETTINGS.Pipes.Accessories);

        form.Close();
        form.Dispose();
        Console.WriteLine();
    }

    // ==================================================
    // insulatortest preview <folder> [scale] - LOOK at both tabs. Scale
    // 1.5 = a screen at 150%, as roomtagtest does it.
    // ==================================================

    private static int Preview(string folder, float scale)
    {
        Directory.CreateDirectory(folder);

        File.WriteAllText(INSULATIONSETTINGS.Ducts.SettingsPath, "enabled=1\r\nSupply Air\t25\tFibreglass 25\r\nFresh Air\t40\tElastomeric\r\nKitchen Extract\t50\tGone Type\r\n");
        File.WriteAllText(INSULATIONSETTINGS.Pipes.SettingsPath, "enabled=1\r\naccessories=0\r\nChilled Water Supply\t32\tPhenolic\r\nChilled Water Return\t32\tPhenolic\r\n");

        var form = new INSULATIONForm(Ducts(), Pipes(true))
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20)
        };

        if (scale != 1f)
        {
            var own = new List<KeyValuePair<Control, Font>>();
            Action<Control> find = null;

            find = c =>
            {
                if (c.Parent == null || !c.Font.Equals(c.Parent.Font))
                    own.Add(new KeyValuePair<Control, Font>(c, c.Font));

                foreach (Control child in c.Controls)
                    find(child);
            };

            find(form);

            foreach (KeyValuePair<Control, Font> pair in own)
                pair.Key.Font = new Font(pair.Value.FontFamily, pair.Value.Size * scale, pair.Value.Style);

            form.Scale(new SizeF(scale, scale));
        }

        form.Show();
        Settle();

        TabControl tabs = All<TabControl>(form).Single();
        string tag = scale == 1f ? string.Empty : "-" + scale.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + "x";

        for (int i = 0; i < tabs.TabCount; i++)
        {
            tabs.SelectedIndex = i;
            Settle();

            string path = Path.Combine(folder, "insulator-" + tabs.TabPages[i].Text.ToLowerInvariant() + tag + ".png");

            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(path);
            }

            Console.WriteLine("  " + path + "  " + form.Width + "x" + form.Height);
        }

        form.Close();

        return 0;
    }
}
