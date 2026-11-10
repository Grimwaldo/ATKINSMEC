using ATKINSMEC;
using System;
using System.Collections.Generic;

// SHEET QA's flowrate check reports a tag on an element with no flow only
// when the tag SHOWS a flow (user, 2026-11-10: a riser tag reading
// "500x400 EAD FA/TB" was listed as reading zero). QAFLOWTEXT decides it
// from what the tag reads and from the other tags of its type.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    // Revit's internal flow (ft³/s) from L/s
    private static double Ls(double litres) => litres / 28.316846592;

    private static QAFLOWTEXT.Evidence Ev(int seen, int showed) =>
        new QAFLOWTEXT.Evidence { Seen = seen, Showed = showed };

    // A type's record, worked out as the check does: each tag's text
    // against its own element's flow
    private static QAFLOWTEXT.Evidence Learn(params (string text, double ls)[] tags)
    {
        var ev = new QAFLOWTEXT.Evidence();

        foreach (var t in tags)
        {
            ev.Seen++;

            if (QAFLOWTEXT.Shows(t.text, Ls(t.ls), t.ls.ToString("0") + " L/s"))
                ev.Showed++;
        }

        return ev;
    }

    private static int Main()
    {
        Console.WriteLine();
        Console.WriteLine("SHEET QA - does a tag on an element with no flow show that flow?");
        Console.WriteLine();

        Console.WriteLine("What the tag reads");

        void Reads(string text, bool expected) =>
            Check((expected ? "a flow:     " : "not a flow: ") + "\"" + text + "\"",
                QAFLOWTEXT.ReadsZero(text) == expected);

        // The user's tag: size, system, type - no flow
        Reads("500x400 EAD FA/TB", false);
        Reads("500 x 400 SAD", false);
        Reads("1000x500", false);
        Reads("Ø50 CHWS", false);
        Reads("Ø50 CHWS +0.00", false);
        Reads("L00 SAD", false);
        Reads("400x0", false);
        Reads("0x0", false);
        Reads("0 mm", false);
        Reads("0mm", false);
        Reads("0 m", false);
        Reads("0 m²", false);
        Reads("0 m/s", false);
        Reads("0 Pa", false);
        Reads("0°", false);
        Reads("0.5", false);
        Reads("10", false);
        Reads("CLPS", false);
        Reads("", false);
        Reads(null, false);

        Reads("0", true);
        Reads("0 L/s", true);
        Reads("0.0 l/s", true);
        Reads("0,00 L/s", true);
        Reads("Q=0", true);
        Reads("0L/s", true);
        Reads("0 m³/h", true);
        Reads("0 m3/h", true);
        Reads("0 CFM", true);
        Reads("500x400 0 L/s", true);
        Reads("500x400 EAD 0", true);
        Reads("0 INLET", true);

        // TagText runs labels together: a zero glued to the size is still
        // found by its unit
        Reads("500x4000 L/s", true);

        Console.WriteLine();
        Console.WriteLine("A tag on an element WITH a flow: does it show it?");

        void Shows(string text, double ls, string shown, bool expected) =>
            Check((expected ? "shows   " : "doesn't ") + ls + " L/s: \"" + text + "\"",
                QAFLOWTEXT.Shows(text, Ls(ls), shown) == expected);

        Shows("250 L/s", 250, "250 L/s", true);
        Shows("250", 250, "250 L/s", true);
        Shows("250.0 L/s", 250, "250 L/s", true);
        Shows("250,0 L/s", 250, "250 L/s", true);
        Shows("Q=250", 250, "250 L/s", true);
        Shows("500x400250 L/s", 250, "250 L/s", true);
        Shows("530 CFM", 250, "250 L/s", true);
        Shows("900 m³/h", 250, "250 L/s", true);
        Shows("1,250 L/s", 1250, "1,250 L/s", true);
        Shows("17.6", 17.6, "17.6 L/s", true);

        Shows("500x400 EAD FA/TB", 250, "250 L/s", false);
        Shows("500x400 EAD FA/TB", 50, "50 L/s", false);
        Shows("500x400 EAD FA/TB", 5, "5 L/s", false);
        Shows("1.250", 250, "250 L/s", false);
        Shows("", 250, "250 L/s", false);

        // 1 L/s is 0.001 m³/s: "0", "0.0" and "0.00" must not be looked
        // for, or every size ending in 0 would show it
        Shows("500x400 EAD FA/TB", 1, "1 L/s", false);

        Console.WriteLine();
        Console.WriteLine("A type shows flow when most of its tags read theirs");

        Check("none seen: no", !Ev(0, 0).ShowsFlow);
        Check("1 of 1: yes", Ev(1, 1).ShowsFlow);
        Check("1 of 2: no", !Ev(2, 1).ShowsFlow);
        Check("2 of 3: yes", Ev(3, 2).ShowsFlow);
        Check("1 of 10 (a size that happens to equal the flow): no", !Ev(10, 1).ShowsFlow);

        // The user's riser tags on ducts with flow - one 400 L/s duct's
        // "400" is a coincidence, not a flow
        QAFLOWTEXT.Evidence risers = Learn(
            ("500x400 EAD FA/TB", 400),
            ("500x400 EAD FA/TB", 310),
            ("600x400 SAD FA/TB", 275),
            ("400x300 EAD FA/TB", 120),
            ("800x500 FAD FA/TB", 1150));

        Check("FA/TB risers: " + risers.Showed + " of " + risers.Seen + " read a flow - type shows none",
            risers.Seen == 5 && risers.Showed == 1 && !risers.ShowsFlow);

        // A size + flow tag whose labels TagText ran together
        QAFLOWTEXT.Evidence sizeFlow = Learn(
            ("500x400250 L/s", 250),
            ("600x400310 L/s", 310),
            ("300x200", 0.4),
            ("400x300120 L/s", 120));

        Check("size + flow: " + sizeFlow.Showed + " of " + sizeFlow.Seen + " - type shows flow",
            sizeFlow.ShowsFlow);

        Console.WriteLine();
        Console.WriteLine("Reported?");

        void Report(string what, string text, QAFLOWTEXT.Evidence ev, bool expected) =>
            Check((expected ? "reported:  " : "left out:  ") + what,
                QAFLOWTEXT.Report(text, ev) == expected);

        Report("the user's FA/TB tag, nothing known of its type", "500x400 EAD FA/TB", null, false);
        Report("the FA/TB tag, its type shows no flow", "500x400 EAD FA/TB", risers, false);
        Report("a flow tag reading 0 L/s", "0 L/s", null, true);
        Report("a flow tag reading 0, its type unread", "0", Ev(5, 0), true);
        Report("size + flow, flow not set, prints only the size", "500x400", sizeFlow, true);
        Report("blank, its type shows flow", "", Ev(3, 3), true);
        Report("blank, nothing known", "", null, false);
        Report("unreadable, nothing known (as before)", null, null, true);
        Report("unreadable, its type never read", null, Ev(0, 0), true);
        Report("unreadable, its type shows flow", null, Ev(5, 5), true);
        Report("unreadable, its type shows none", null, Ev(5, 0), false);

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "ALL PASSED" : _failed + " FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
