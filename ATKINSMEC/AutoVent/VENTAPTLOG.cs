using System;
using System.IO;
using System.Text;

namespace ATKINSMEC
{
    // WHAT THE APARTMENTS MAP FOUND, AND WHY, written to %AppData%\ATKINSMEC as
    // ATKINSMEC_apartments_log.txt every time the map is built - by the
    // levels list, the export or the airflow write; whichever ran last
    // owns the file.
    //
    // The same reason QACHWLOG, EQUIPLOG and NWCLOG exist. User-reported:
    // "the apartments rooms from IND model are not exported in the excel
    // sheet under the apartment from the arch model" - and nothing on
    // screen said whether the interior rooms were searched at all, stood
    // in no apartment, or stood in one on another level. Each of those
    // has a different answer, and only the model knows which it was. The
    // log writes down, per placement and per level of this model, what
    // each room was found in - and for a room found in nothing, whether
    // it was inside an apartment IN PLAN but outside its height, which is
    // the difference between "wrong model ticked" and "the two models'
    // floors do not line up".
    //
    // Revit-free; VENTAPARTMENTS fills it. Never allowed to break the map.
    public static class VENTAPTLOG
    {
        private const string FileName = "ATKINSMEC_apartments_log.txt";

        private static readonly StringBuilder Lines = new StringBuilder();

        public static void Start(string purpose, string title)
        {
            Lines.Length = 0;

            Say("Atkins MEP Toolkit - AUTOVENT redundant rooms");
            Say(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  -  built for " + (purpose ?? "the window"));
            Say(title ?? string.Empty);
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

        // Metres, to the millimetre, from Revit's feet
        public static string M(double feet)
        {
            return (feet * 0.3048).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

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
                return LOGS.PathOf(FileName);
            }
        }
    }
}
