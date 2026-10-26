using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Do the ribbon icons actually LOAD out of the built assembly?
//
// The DLL byte scan proves the resource NAMES are right. It does not
// prove a single one of them decodes - and a picture that fails to
// decode shows up as a blank button, never as an error, because UI.cs
// swallows it on purpose rather than taking the whole ribbon down.
//
// So this walks the real DLL and runs the same three steps UI.Icon
// does: open the manifest stream, decode it with CacheOption.OnLoad,
// and Freeze it. Reading resources needs no Revit, so this runs with no
// model and no Revit installed.
//
// The trap it exists for: WITHOUT CacheOption.OnLoad a BitmapImage
// reads its StreamSource lazily, the using block disposes the stream
// first, and every button comes out empty.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private static readonly string[] Names =
    {
        "autovent", "chwvolume", "ductinsulation", "sprinklers",
        "alignterminals", "sheetqa", "autosync", "nwcbatch",
        "equipchanger", "ductesp", "roomtags"
    };

    // Exactly what UI.Icon does, word for word
    private static ImageSource Icon(Assembly dll, string name, int size)
    {
        string resource = "ATKINSMEC.Icons." + name + size + ".png";

        using (Stream stream = dll.GetManifestResourceStream(resource))
        {
            if (stream == null)
                return null;

            var image = new BitmapImage();

            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string path = args.Length > 0
            ? args[0]
            : RepoPath.Find(@"ATKINSMEC\bin\Release\2025\ATKINSMEC.dll");

        Console.WriteLine();
        Console.WriteLine("ATKINSMEC - the ribbon icons, out of the real DLL");
        Console.WriteLine(path);
        Console.WriteLine();

        if (!File.Exists(path))
        {
            Console.WriteLine("  FAIL  that DLL is not there");

            return 1;
        }

        Assembly dll = Assembly.LoadFrom(path);

        foreach (string name in Names)
        {
            foreach (int size in new[] { 32, 16 })
            {
                ImageSource image = null;
                string trouble = string.Empty;

                try
                {
                    image = Icon(dll, name, size);
                }
                catch (Exception ex)
                {
                    trouble = ex.GetType().Name + " - " + ex.Message;
                }

                if (image == null)
                {
                    Check(name + size + " loads",
                        false);

                    if (trouble.Length > 0)
                        Console.WriteLine("        " + trouble);

                    continue;
                }

                var bitmap = (BitmapSource)image;

                // The RIGHT size, not just some image: a 16 saved over a
                // 32 would go unnoticed on the ribbon and look wrong
                // only at high DPI
                Check(name + size + " loads, is " + size + "x" + size +
                      " and is frozen",
                    bitmap.PixelWidth == size
                    && bitmap.PixelHeight == size
                    && bitmap.IsFrozen);
            }
        }

        // And the one that would have caught the lazy-stream trap
        {
            bool blank = false;

            try
            {
                using (Stream stream = dll.GetManifestResourceStream(
                    "ATKINSMEC.Icons.autovent32.png"))
                {
                    var lazy = new BitmapImage();

                    lazy.BeginInit();
                    lazy.StreamSource = stream;

                    // NO CacheOption here, on purpose
                    lazy.EndInit();
                }
            }
            catch
            {
                blank = true;
            }

            Console.WriteLine();
            Console.WriteLine(
                "  note   without CacheOption.OnLoad the stream is read " +
                "lazily" + (blank ? " and it threw" : ""));
        }

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
