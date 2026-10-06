using System;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // What the equipment change actually did, written to the Desktop.
    //
    // The same reason QACHWLOG and NWCLOG exist. This one changes
    // ductwork - it resizes ducts, drives fittings and takes joints
    // apart and puts them back - and every one of those can be refused
    // by a family, a lookup table or Revit itself. A window can show
    // one line per element; the log says which of the five steps it was
    // and what Revit said about it.
    //
    // One file per run, overwritten each time.
    public class EQUIPLOG
    {
        private const string FileName = "ATKINSMEC_equip_log.txt";

        private static readonly StringBuilder Lines = new StringBuilder();

        public static void Start(string title)
        {
            Lines.Length = 0;

            Say("ATKINSMEC - equipment changer");
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

        // The TYPE as well as the message: Revit uses a different
        // exception for "not writable", "would delete an element" and
        // "not in an API context", and they need different answers
        public static void Blew(string what, Exception ex)
        {
            if (ex == null)
                return;

            Say("   " + what + " THREW " + ex.GetType().Name);
            Say("      " + Flatten(ex.Message));
        }

        private static string Flatten(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        // Never allowed to break the change it is reporting on
        public static void Save()
        {
            try
            {
                File.WriteAllText(Where, Lines.ToString());
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
