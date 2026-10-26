using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

// AUTOVENT's Fixtures... dialog, driven through WPF itself (user,
// 2026-10-17): one row per MODEL rather than per placement, several rows
// picked at once in both lists, a search over the families.
//
// The real dialog is shown off-screen and worked as a person would: a
// box clicked through ToggleButton's own OnClick, rows picked into the
// list's SelectedItems as a click does, Ctrl+A as ListBox.SelectAll,
// Space as a key event. What it must keep: opening and pressing OK
// changes nothing, ticks are what the rows say, and nothing a search
// hides is changed by a pick.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private const string Arc10 = "5232535-ATR-POD-ZZZ-3DM-ARC-000010.rvt";
    private const string Arc20 = "5232535-ATR-TWR-ZZZ-3DM-ARC-000020.rvt";
    private const string Ind = "5232535-ATR-TWR-ZZZ-3DM-IND-000030.rvt";
    private const string Plu = "5232535-ATR-POD-ZZZ-3DM-PLU-000040.rvt";

    private const string Wc = "M_Water Closet - Wall Mounted";
    private const string Urinal = "M_Urinal - Wall Hung";
    private const string Lavatory = "M_Lavatory - Vanity";
    private const string Sink = "Sink - Kitchen";
    private const string IndWc = "IND_WC_Pack";
    private const string IndBasin = "IND_Basin";
    private const string PluWc = "PLU_Water Closet";
    private const string PluDrain = "PLU_Floor Drain";

    // Placements, as the window hands them in
    private static readonly List<VENTFIXTUREDIALOG.Source> Sources = new List<VENTFIXTUREDIALOG.Source>
    {
        new VENTFIXTUREDIALOG.Source { Id = 0, Label = "This model" },
        new VENTFIXTUREDIALOG.Source { Id = 11, Label = Arc10 },
        new VENTFIXTUREDIALOG.Source { Id = 12, Label = Arc20 + " : 1 : Tower A" },
        new VENTFIXTUREDIALOG.Source { Id = 13, Label = Arc20 + " : 2 : Tower B" },
        new VENTFIXTUREDIALOG.Source { Id = 21, Label = Ind + " : 1" },
        new VENTFIXTUREDIALOG.Source { Id = 22, Label = Ind + " : 2" },
        new VENTFIXTUREDIALOG.Source { Id = 23, Label = Ind + " : 3" },
        new VENTFIXTUREDIALOG.Source { Id = 24, Label = Ind + " : 4" },
        new VENTFIXTUREDIALOG.Source { Id = 31, Label = Plu }
    };

    // The rooms come from ARC-000010 and Tower A
    private static readonly List<long> RoomLinks = new List<long> { 11, 12 };

    // What VENTREAD reads: each placement's families
    private static readonly Dictionary<long, Dictionary<string, int>> Read = Build();

    private static Dictionary<long, Dictionary<string, int>> Build()
    {
        var read = new Dictionary<long, Dictionary<string, int>>
        {
            [0] = new Dictionary<string, int>(),
            [11] = new Dictionary<string, int> { [Wc] = 10, [Lavatory] = 8, [Sink] = 2 },
            [31] = new Dictionary<string, int> { [PluWc] = 18, [PluDrain] = 30 }
        };

        foreach (long tower in new long[] { 12, 13 })
            read[tower] = new Dictionary<string, int> { [Wc] = 5, [Urinal] = 3 };

        foreach (long floor in new long[] { 21, 22, 23, 24 })
            read[floor] = new Dictionary<string, int> { [IndWc] = 2, [IndBasin] = 2 };

        return read;
    }

    private static void Reset()
    {
        VENTSETTINGS.FixtureSources.Clear();
        VENTSETTINGS.FixtureFamilies.Clear();
    }

    // ---- driving the dialog ------------------------------------------

    // Shows the dialog off-screen, runs the body once it is laid out, then
    // presses OK (the real Keep) or closes it
    private static void Run(Action<VENTFIXTUREDIALOG> body, bool ok)
    {
        var dialog = new VENTFIXTUREDIALOG(Sources, RoomLinks, ids => VENTSETTINGS.FamiliesFrom(Read, ids))
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = -4000,
            ShowActivated = false
        };

        dialog.Loaded += (s, e) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                body(dialog);
            }
            catch (Exception ex)
            {
                Check("no exception: " + ex, false);
            }

            if (ok)
                typeof(VENTFIXTUREDIALOG).GetMethod("Keep", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dialog, null);
            else
                dialog.Close();
        }), DispatcherPriority.ContextIdle);

        dialog.ShowDialog();
    }

    private static void Settle(Window window)
    {
        window.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.ContextIdle);
    }

    // Every row, the search's hidden ones too
    private static List<ListBoxItem> Rows(ListBox list)
    {
        return ((IEnumerable<ListBoxItem>)list.ItemsSource).ToList();
    }

    // The rows the list shows
    private static List<ListBoxItem> Showing(ListBox list)
    {
        return list.Items.Cast<ListBoxItem>().ToList();
    }

    private static TextBlock Part(ListBoxItem row, int column)
    {
        return ((Grid)row.Content).Children.OfType<TextBlock>().First(t => Grid.GetColumn(t) == column);
    }

    private static string Name(ListBoxItem row)
    {
        return Part(row, 1).Text;
    }

    private static string Aside(ListBoxItem row)
    {
        return Part(row, 2).Text;
    }

    private static CheckBox Box(ListBoxItem row)
    {
        return ((Grid)row.Content).Children.OfType<CheckBox>().First();
    }

    private static ListBoxItem Row(ListBox list, string name)
    {
        return Rows(list).FirstOrDefault(r => Name(r) == name);
    }

    private static bool On(ListBox list, string name)
    {
        ListBoxItem row = Row(list, name);

        return row != null && Box(row).IsChecked == true;
    }

    private static void Click(ListBox list, string name)
    {
        CheckBox box = Box(Row(list, name));

        typeof(ToggleButton)
            .GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(box, null);
    }

    private static void Pick(ListBox list, params string[] names)
    {
        list.SelectedItems.Clear();

        foreach (string name in names)
            list.SelectedItems.Add(Row(list, name));
    }

    private static void Space(ListBox list)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(list), 0, Key.Space)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };

        list.RaiseEvent(args);
    }

    private static bool Same(IEnumerable<long> a, params long[] b)
    {
        return new HashSet<long>(a).SetEquals(b);
    }

    // ---- the checks --------------------------------------------------

    [STAThread]
    private static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        Console.WriteLine();
        Console.WriteLine("AUTOVENT - the Fixtures dialog: models, picks, search");
        Console.WriteLine();

        // ---- placements into models --------------------------------
        Check("a placement's model is the title before \" : \"",
            VENTFIXTUREDIALOG.ModelOf(Arc20 + " : 2 : Tower B") == Arc20);
        Check("a model placed once is its label", VENTFIXTUREDIALOG.ModelOf(Arc10) == Arc10);
        Check("no label, no model", VENTFIXTUREDIALOG.ModelOf(null) == string.Empty);

        List<KeyValuePair<string, List<long>>> models = VENTFIXTUREDIALOG.Models(Sources);

        Check("nine placements are five models",
            models.Select(m => m.Key).SequenceEqual(new[] { "This model", Arc10, Arc20, Ind, Plu }));
        Check("a model holds all its placements", Same(models[3].Value, 21, 22, 23, 24));
        Check("four placements of one model and one of another are 2 models",
            VENTFIXTUREDIALOG.ModelCount(Sources, new long[] { 21, 22, 23, 24, 31 }) == 2);
        Check("this model counts as one", VENTFIXTUREDIALOG.ModelCount(Sources, new long[] { 0 }) == 1);
        Check("nothing chosen, no models", VENTFIXTUREDIALOG.ModelCount(Sources, new long[0]) == 0);

        // ---- opened over a model nobody set up ---------------------
        Reset();

        Run(d =>
        {
            Check("one row per model", Rows(d.ModelList).Select(Name).SequenceEqual(new[] { "This model", Arc10, Arc20, Ind, Plu }));
            Check("a model placed twice says so", Aside(Row(d.ModelList, Arc20)) == "2 placements");
            Check("a model placed four times says so", Aside(Row(d.ModelList, Ind)) == "4 placements");
            Check("a model placed once says nothing", Aside(Row(d.ModelList, Arc10)) == string.Empty);

            Check("the room links' models arrive ticked", On(d.ModelList, Arc10) && On(d.ModelList, Arc20));
            Check("the others do not", !On(d.ModelList, "This model") && !On(d.ModelList, Ind) && !On(d.ModelList, Plu));

            Check("families from EVERY placement of the ticked models (WC 10 + 5 + 5)", Aside(Row(d.FamilyList, Wc)) == "20");
            Check("both towers' urinals", Aside(Row(d.FamilyList, Urinal)) == "6");
            Check("nothing from unticked models", Row(d.FamilyList, IndWc) == null && Row(d.FamilyList, PluWc) == null);
            Check("families ticked by the name rule",
                On(d.FamilyList, Wc) && On(d.FamilyList, Urinal) && !On(d.FamilyList, Lavatory) && !On(d.FamilyList, Sink));
        }, true);

        Check("OK with nothing changed still follows the room links", VENTSETTINGS.FixtureSources.Count == 0);
        Check("... and the name rule", VENTSETTINGS.FixtureFamilies.Count == 0);

        // ---- several models ticked at once -------------------------
        Reset();

        Run(d =>
        {
            Pick(d.ModelList, Ind, Plu);
            Click(d.ModelList, Plu);

            Check("ticking a picked model ticks every picked model", On(d.ModelList, Ind) && On(d.ModelList, Plu));
            Check("an unpicked model is left alone", On(d.ModelList, Arc10) && !On(d.ModelList, "This model"));

            Check("their families arrive (IND 2 x 4 placements)", Aside(Row(d.FamilyList, IndWc)) == "8");
            Check("a new family follows the name rule",
                On(d.FamilyList, IndWc) && On(d.FamilyList, PluWc) && !On(d.FamilyList, IndBasin) && !On(d.FamilyList, PluDrain));
        }, true);

        Check("a ticked model stores ALL its placements",
            Same(VENTSETTINGS.FixtureSources, 11, 12, 13, 21, 22, 23, 24, 31));
        Check("family ticks still the name rule", VENTSETTINGS.FixtureFamilies.Count == 0);

        // ---- a box on a row NOT picked -----------------------------
        Reset();

        Run(d =>
        {
            Pick(d.ModelList, Ind, Plu);
            Click(d.ModelList, "This model");

            Check("only the clicked row changes", On(d.ModelList, "This model"));
            Check("the picked rows are not touched", !On(d.ModelList, Ind) && !On(d.ModelList, Plu));
        }, false);

        Check("Cancel changes nothing", VENTSETTINGS.FixtureSources.Count == 0);

        // ---- families keep their ticks while models come and go ----
        Reset();

        Run(d =>
        {
            Click(d.FamilyList, Urinal);
            Check("a family unticked", !On(d.FamilyList, Urinal));

            Click(d.ModelList, Arc20);
            Check("its model unticked: the family goes", Row(d.FamilyList, Urinal) == null);
            Check("... and the counts drop (WC 10)", Aside(Row(d.FamilyList, Wc)) == "10");

            Click(d.ModelList, Arc20);
            Check("ticked again: the family is back, still unticked", Row(d.FamilyList, Urinal) != null && !On(d.FamilyList, Urinal));
        }, true);

        Check("OK keeps the families as ticked", VENTSETTINGS.FixtureFamilies.SetEquals(new[] { Wc }));
        Check("the models were left as the defaults", VENTSETTINGS.FixtureSources.Count == 0);

        // ---- the search ---------------------------------------------
        Reset();

        Run(d =>
        {
            Click(d.ModelList, Plu);

            d.Search = "water closet";
            Settle(d);

            Check("the search shows what matches",
                Showing(d.FamilyList).Select(Name).OrderBy(n => n).SequenceEqual(new[] { Wc, PluWc }.OrderBy(n => n)));
            Check("... and hides the rest, ticks kept", Rows(d.FamilyList).Count == 6 && On(d.FamilyList, Urinal));

            d.FamilyList.SelectAll();
            Check("Ctrl+A picks the rows showing", d.FamilyList.SelectedItems.Count == 2);

            Click(d.FamilyList, PluWc);
            Check("a tick on a picked row ticks the rows showing", !On(d.FamilyList, PluWc) && !On(d.FamilyList, Wc));
            Check("a hidden row is not changed", On(d.FamilyList, Urinal) && !On(d.FamilyList, Lavatory));

            d.Search = string.Empty;
            Settle(d);
            Check("cleared: every row shows again", Showing(d.FamilyList).Count == 6);

            Pick(d.FamilyList, Sink);
            d.Search = "urinal";
            Settle(d);
            Check("a picked row the search hides is no longer picked",
                !d.FamilyList.SelectedItems.Contains(Row(d.FamilyList, Sink)));

            d.Search = string.Empty;
            Settle(d);
            Click(d.FamilyList, Lavatory);
            Check("so a tick then leaves it alone", On(d.FamilyList, Lavatory) && !On(d.FamilyList, Sink));

            d.Search = "WATER plu";
            Settle(d);
            Check("every word, any order, any case", Showing(d.FamilyList).Select(Name).SequenceEqual(new[] { PluWc }));
            d.Search = string.Empty;
        }, true);

        Check("ticks made under a search are what OK stores",
            VENTSETTINGS.FixtureFamilies.SetEquals(new[] { Urinal, Lavatory }));
        Check("... with the model ticked there", Same(VENTSETTINGS.FixtureSources, 11, 12, 13, 31));

        // ---- Space ---------------------------------------------------
        Reset();

        Run(d =>
        {
            Pick(d.FamilyList, Lavatory, Sink);
            Space(d.FamilyList);
            Check("Space ticks the picked rows", On(d.FamilyList, Lavatory) && On(d.FamilyList, Sink));

            Space(d.FamilyList);
            Check("Space again unticks them", !On(d.FamilyList, Lavatory) && !On(d.FamilyList, Sink));

            Pick(d.FamilyList, Wc, Sink);
            Space(d.FamilyList);
            Check("some ticked: Space ticks them all", On(d.FamilyList, Wc) && On(d.FamilyList, Sink));
            Check("rows not picked are left alone", !On(d.FamilyList, Lavatory) && On(d.FamilyList, Urinal));

            Pick(d.ModelList, Ind);
            Space(d.ModelList);
            Check("Space works on the models too, and reads their families",
                On(d.ModelList, Ind) && Row(d.FamilyList, IndWc) != null);
        }, true);

        Check("families ticked beyond the rule are stored", VENTSETTINGS.FixtureFamilies.SetEquals(new[] { Wc, Urinal, Sink, IndWc }));

        // ---- a choice saved by the old dialog: one placement --------
        Reset();
        VENTSETTINGS.FixtureSources.Add(21);

        Run(d =>
        {
            Check("one placement chosen: its model arrives ticked", On(d.ModelList, Ind));
            Check("... and only it - a choice, not the room links",
                !On(d.ModelList, Arc10) && !On(d.ModelList, Arc20));
        }, true);

        Check("OK stores every placement of the model", Same(VENTSETTINGS.FixtureSources, 21, 22, 23, 24));

        // ---- a choice that says what the defaults say ---------------
        Reset();
        VENTSETTINGS.FixtureSources.UnionWith(new long[] { 11, 12 });

        Run(d => { }, true);

        Check("ticks equal to the room links' models go back to following them", VENTSETTINGS.FixtureSources.Count == 0);

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        app.Shutdown();

        return _failed == 0 ? 0 : 1;
    }
}
