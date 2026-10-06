using ATKINSMEC;
using System;
using System.Diagnostics;
using System.IO;

// Exercises the two AutoSync files that have no Revit in them.
//
// The property that matters most is the TIMEOUT. These probes run on
// Revit's own thread, and an unreachable UNC path can take the best
// part of a minute to admit defeat - which would be a frozen Revit
// every five minutes. Every check below is timed as well as asserted.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static int Main()
    {
        Console.WriteLine("AutoSync harness");
        Console.WriteLine();

        // ==================================================
        // The five minute floor
        // ==================================================

        Check("1 minute is refused and becomes 5",
            SYNCSETTINGS.Floor(1) == 5);

        Check("0 and negatives become 5",
            SYNCSETTINGS.Floor(0) == 5 && SYNCSETTINGS.Floor(-10) == 5);

        Check("5 stays 5", SYNCSETTINGS.Floor(5) == 5);

        Check("anything above is left alone",
            SYNCSETTINGS.Floor(6) == 6 && SYNCSETTINGS.Floor(480) == 480);

        // ==================================================
        // Settings survive a round trip
        // ==================================================

        string path = SYNCSETTINGS.SettingsPath;
        string saved = File.Exists(path) ? File.ReadAllText(path) : null;

        try
        {
            SYNCSETTINGS.Load();

            SYNCSETTINGS.Minutes = 25;
            SYNCSETTINGS.Relinquish = true;
            SYNCSETTINGS.Comment = "Auto sync from the harness";
            SYNCSETTINGS.Host = "example.org";
            SYNCSETTINGS.LastAnswer = true;
            SYNCSETTINGS.Save();

            string written = File.ReadAllText(path);

            Check("the file records the interval",
                written.Contains("minutes=25"));

            Check("the file records the rest",
                written.Contains("relinquish=true")
                && written.Contains("host=example.org")
                && written.Contains("lastanswer=true"));

            // A file that somehow says 2 must still come back as 5
            File.WriteAllText(path,
                "minutes=2\nrelinquish=false\nhost=a.b\nlastanswer=false\n");

            Reload();

            Check("a settings file asking for 2 minutes still loads as 5",
                SYNCSETTINGS.Minutes == 5);

            File.WriteAllText(path, "this is not a settings file at all");

            Reload();

            Check("rubbish in the file falls back to the defaults",
                SYNCSETTINGS.Minutes == 15
                && SYNCSETTINGS.Host == SYNCNET.DefaultHost);
        }
        finally
        {
            if (saved == null)
                File.Delete(path);
            else
                File.WriteAllText(path, saved);
        }

        Console.WriteLine();

        // ==================================================
        // Reaching a share - the timeout is the point
        // ==================================================

        var clock = Stopwatch.StartNew();

        SYNCNET.Answer here = SYNCNET.Share(
            System.Reflection.Assembly.GetExecutingAssembly().Location);

        Console.WriteLine("  local file      " + clock.ElapsedMilliseconds
            + " ms   " + here.Why);

        Check("a file that is there is reported reachable", here.Reachable);

        // An address reserved to go nowhere. Without the bounded probe
        // this is the call that would freeze Revit.
        clock.Restart();

        SYNCNET.Answer nowhere = SYNCNET.Share(
            @"\\10.255.255.1\share\model_central.rvt");

        long tookShare = clock.ElapsedMilliseconds;

        Console.WriteLine("  dead UNC path   " + tookShare
            + " ms   " + nowhere.Why);

        Check("an unreachable share is reported unreachable",
            !nowhere.Reachable);

        Check("and it gives up in under 6 seconds  (took " + tookShare
              + " ms)", tookShare < 6000);

        Console.WriteLine();

        // ==================================================
        // Reaching the internet
        // ==================================================

        clock.Restart();

        SYNCNET.Answer online = SYNCNET.Internet(
            SYNCNET.DefaultHost, SYNCNET.TimeoutMs);

        long tookOnline = clock.ElapsedMilliseconds;

        Console.WriteLine("  " + SYNCNET.DefaultHost + "        "
            + tookOnline + " ms   " + online.Why);

        Console.WriteLine("     (reachable: " + online.Reachable
            + " - this machine's own connection decides this one)");

        Check("the internet test answers in under 6 seconds  (took "
              + tookOnline + " ms)", tookOnline < 6000);

        clock.Restart();

        SYNCNET.Answer bad = SYNCNET.Internet(
            "no-such-host-anywhere.invalid", 2000);

        long tookBad = clock.ElapsedMilliseconds;

        Console.WriteLine("  bad host        " + tookBad
            + " ms   " + bad.Why);

        Check("a host that does not exist is reported unreachable",
            !bad.Reachable);

        Check("and it gives up in under 4 seconds  (took " + tookBad
              + " ms)", tookBad < 4000);

        Check("an empty central path is unreachable, not a crash",
            !SYNCNET.Share(null).Reachable
            && !SYNCNET.Share("   ").Reachable);

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }

    // SYNCSETTINGS loads once; the harness needs it to look again
    private static void Reload()
    {
        typeof(SYNCSETTINGS)
            .GetField("_loaded",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static)
            .SetValue(null, false);

        SYNCSETTINGS.Load();
    }
}
