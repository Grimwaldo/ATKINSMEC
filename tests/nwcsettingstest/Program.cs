using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using ATKINSMEC;

// Round-trips the REAL NWCSETTINGS - the file itself is compiled in, not
// mirrored - because a settings round trip is where things break
// quietly. A number written with a comma for a decimal point, a tab
// inside a name, a value the reader does not recognise: each of those
// loses a setting without anybody noticing until the export is wrong.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static NWCSETTINGS Loaded()
    {
        return new NWCSETTINGS
        {
            OutputFolder = @"C:\Projects\1234\Navisworks\Issue",
            ViewName = "Navisworks",
            Prefix = "ISSUE-",
            Suffix = "-A",

            SharedCoordinates = false,
            ExportLinks = true,
            DivideIntoLevels = false,
            ExportElementIds = false,
            ExportRoomAsAttribute = false,
            ExportParts = true,
            ConvertElementProperties = false,
            SkipUnchanged = true,

            Parameters = 2,
            Worksets = 1,
            FacetingFactor = 3.5
        };
    }

    private static int Main()
    {
        Console.WriteLine("NWC settings round-trip harness");
        Console.WriteLine();

        string folder = Path.Combine(
            Path.GetTempPath(), "nwcsettingstest");

        Directory.CreateDirectory(folder);

        string file = Path.Combine(folder, "settings.nwcset");

        // --- everything survives being written and read back ---------
        {
            NWCSETTINGS wrote = Loaded();

            Check("it writes a settings file", wrote.SaveTo(file));

            var read = new NWCSETTINGS();

            Check("and reads it back", read.LoadFrom(file));

            Check("the folder survives",
                read.OutputFolder == wrote.OutputFolder);

            Check("the view name survives",
                read.ViewName == wrote.ViewName);

            Check("the prefix and suffix survive",
                read.Prefix == wrote.Prefix &&
                read.Suffix == wrote.Suffix);

            Check("every tick survives, including the FALSE ones",
                read.SharedCoordinates == false &&
                read.ExportLinks == true &&
                read.DivideIntoLevels == false &&
                read.ExportElementIds == false &&
                read.ExportRoomAsAttribute == false &&
                read.ExportParts == true &&
                read.ConvertElementProperties == false &&
                read.SkipUnchanged == true);

            Check("the parameter and workset choices survive",
                read.Parameters == 2 && read.Worksets == 1);

            Check("the faceting factor survives as 3.5",
                Math.Abs(read.FacetingFactor - 3.5) < 1e-9);
        }

        // --- a shared file carries no model list ----------------------
        {
            NWCSETTINGS wrote = Loaded();

            wrote.Jobs.Add(new NWCJOB
            {
                What = NWCJOB.Kind.Local,
                Name = "somebody-elses-model",
                File = @"D:\NotYourMachine\model.rvt"
            });

            wrote.SaveTo(file);

            string text = File.ReadAllText(file);

            Check("the exported settings carry NO model list",
                !text.Contains("somebody-elses-model") &&
                !text.Contains("\njob\t"));

            // Importing must not empty, or fill, somebody's own list
            var mine = new NWCSETTINGS();

            mine.Jobs.Add(new NWCJOB
            {
                What = NWCJOB.Kind.Local,
                Name = "my-model",
                File = @"C:\Mine\my-model.rvt"
            });

            mine.LoadFrom(file);

            Check("importing leaves my own list exactly as it was",
                mine.Jobs.Count == 1 && mine.Jobs[0].Name == "my-model");

            Check("and it did take the settings",
                mine.Parameters == 2 && mine.ExportLinks);
        }

        // --- a file with job lines in it is still only settings -------
        {
            File.WriteAllText(file,
                "view\tCoordination\r\n" +
                "job\tlocal\tyes\tsneaky\tD:\\sneaky.rvt\r\n" +
                "parameters\t1\r\n");

            var mine = new NWCSETTINGS();

            mine.Jobs.Add(new NWCJOB { Name = "my-model" });

            mine.LoadFrom(file);

            Check("job lines in a settings file are ignored, not trusted",
                mine.Jobs.Count == 1 && mine.Jobs[0].Name == "my-model");

            Check("while the real settings on either side of them read",
                mine.ViewName == "Coordination" && mine.Parameters == 1);
        }

        // --- the decimal point is not the user's to choose ------------
        {
            // A machine set to German writes 3,5 for three and a half.
            // Written and read through the same culture-invariant path,
            // that must still come back as 3.5 rather than 35 or 0.
            CultureInfo was = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture =
                    new CultureInfo("de-DE");

                NWCSETTINGS wrote = Loaded();

                wrote.SaveTo(file);

                var read = new NWCSETTINGS();

                read.LoadFrom(file);

                Check("faceting survives on a machine that writes 3,5",
                    Math.Abs(read.FacetingFactor - 3.5) < 1e-9);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = was;
            }
        }

        // --- what a missing or broken file does -----------------------
        {
            var read = new NWCSETTINGS();

            double before = read.FacetingFactor;

            Check("a file that is not there is refused, not thrown on",
                !read.LoadFrom(Path.Combine(folder, "nothing-here.nwcset")));

            Check("and nothing was changed by trying",
                Math.Abs(read.FacetingFactor - before) < 1e-9);

            File.WriteAllText(file, "this is not a settings file at all");

            var second = new NWCSETTINGS();

            Check("nonsense reads as nothing rather than failing",
                second.LoadFrom(file) &&
                second.ViewName == "Navisworks");
        }

        // --- a tab in a name cannot break the format ------------------
        {
            var wrote = new NWCSETTINGS
            {
                ViewName = "Navis\tworks",
                Prefix = "a\tb"
            };

            wrote.SaveTo(file);

            var read = new NWCSETTINGS();

            read.LoadFrom(file);

            Check("a tab inside a value cannot split the line",
                read.ViewName == "Navis works" && read.Prefix == "a b");
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
