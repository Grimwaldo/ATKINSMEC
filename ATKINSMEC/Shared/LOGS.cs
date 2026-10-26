using System;
using System.IO;

namespace ATKINSMEC
{
    // WHERE EVERY ADD-IN'S LOG GOES: %AppData%\ATKINSMEC, beside the saved
    // settings and the occupancy rules - at the user's request
    // (2026-10-16); they were written to the Desktop. The file names are
    // unchanged (ATKINSMEC_debug_log.txt and the rest).
    public static class LOGS
    {
        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ATKINSMEC");
            }
        }

        // The full path of a log, the folder made if it is not there yet. A
        // folder that cannot be made is not a reason to stop: the write
        // that follows fails quietly, as every log's write does.
        public static string PathOf(string fileName)
        {
            try
            {
                Directory.CreateDirectory(Folder);
            }
            catch
            {
                // the write will say nothing, as before
            }

            return Path.Combine(Folder, fileName);
        }
    }
}
