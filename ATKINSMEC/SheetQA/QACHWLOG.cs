using System;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // What the CHW resize actually did, written to the Desktop.
    //
    // This exists because the resize has now been wrong twice in a real
    // model while every harness passed - the model knows things the
    // arithmetic does not, and guessing from a description costs a
    // round trip each time. AUTOVENT has had a log like this for the
    // same reason and it is the fastest way to diagnose it.
    //
    // One file per run of the fix, overwritten each time: a log that
    // grows all day is a liability, and only the last run is ever
    // interesting.
    public class QACHWLOG
    {
        private const string FileName = "ATKINSMEC_chw_log.txt";

        private static readonly StringBuilder Lines = new StringBuilder();

        public static void Start(string title)
        {
            Lines.Length = 0;

            Say("ATKINSMEC - chilled water resize");
            Say(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say(title);
            Say(string.Empty);
        }

        public static void Say(string text)
        {
            Lines.AppendLine(text ?? string.Empty);
        }

        public static void Head(string text)
        {
            Say(string.Empty);
            Say("=== " + text + " ===");
        }

        // Never allowed to break the fix it is reporting on
        public static void Save()
        {
            try
            {
                string where = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.DesktopDirectory),
                    FileName);

                File.WriteAllText(where, Lines.ToString());
            }
            catch
            {
                // a log that cannot be written is not worth an error
            }
        }

        public static string Where
        {
            get
            {
                try
                {
                    return Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.DesktopDirectory),
                        FileName);
                }
                catch
                {
                    return FileName;
                }
            }
        }
    }
}
