using System;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // What reading a model's worksets actually did, written to the
    // Desktop.
    //
    // This exists for the same reason QACHWLOG does. Reading the
    // worksets of an ACC model has now come back empty twice in a real
    // model while every harness passed, and each round of guessing from
    // a screenshot has cost a rebuild: the picker can only ever show
    // ONE message, and by then it is too late to know which of the
    // several things that had to work did not.
    //
    // So every step is written down: which path was built, which region
    // was tried, what each call threw and the TYPE it threw, and what
    // came back. Read it before theorising.
    //
    // One file per press of the Worksets button, overwritten each time.
    public class NWCLOG
    {
        private const string FileName = "ATKINSMEC_nwc_log.txt";

        private static readonly StringBuilder Lines = new StringBuilder();

        private static bool _on;

        public static void Start(string title)
        {
            Lines.Length = 0;

            _on = true;

            Say("ATKINSMEC - NWC batch exporter, reading worksets");
            Say(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Say(title);
            Say(string.Empty);
        }

        public static void Say(string text)
        {
            if (!_on)
                return;

            Lines.AppendLine(text ?? string.Empty);
        }

        public static void Head(string text)
        {
            Say(string.Empty);
            Say("=== " + text + " ===");
        }

        // The exception TYPE matters as much as its message here: the
        // API documents a different one for "not workshared", "central
        // missing", "access denied" and "cannot reach the server", and
        // they call for completely different answers
        public static void Blew(string what, Exception ex)
        {
            if (ex == null)
            {
                Say("   " + what + ": threw nothing");

                return;
            }

            Say("   " + what + " THREW " + ex.GetType().FullName);
            Say("      " + Flatten(ex.Message));

            if (ex.InnerException != null)
            {
                Say("      inner: " + ex.InnerException.GetType().Name +
                    " - " + Flatten(ex.InnerException.Message));
            }
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        // Never allowed to break the thing it is reporting on
        public static void Save()
        {
            if (!_on)
                return;

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
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.DesktopDirectory),
                    FileName);
            }
        }
    }
}
