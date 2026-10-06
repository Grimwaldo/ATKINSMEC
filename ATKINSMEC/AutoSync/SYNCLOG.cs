using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // What AutoSync has done, so the window can show it and a person can
    // tell a quiet add-in from a broken one.
    //
    // Written from the timer thread and read from the window, so every
    // touch is locked. Kept in memory only: a log file that grows all
    // day unattended is a liability, and the window is where anybody
    // would look.
    public class SYNCLOG
    {
        public enum What
        {
            Synced,
            Saved,
            Skipped,
            Failed,
            Note
        }

        public class Entry
        {
            public DateTime When;
            public What Did;
            public string Model = string.Empty;
            public string Said = string.Empty;

            public string Label
            {
                get
                {
                    switch (Did)
                    {
                        case What.Synced: return "Synced";
                        case What.Saved: return "Saved";
                        case What.Skipped: return "Skipped";
                        case What.Failed: return "Failed";
                        default: return string.Empty;
                    }
                }
            }
        }

        private const int Keep = 200;

        private static readonly object Gate = new object();

        private static readonly List<Entry> Entries = new List<Entry>();

        public static DateTime LastRun { get; private set; }

        public static void Add(What did, string model, string said)
        {
            lock (Gate)
            {
                Entries.Add(new Entry
                {
                    When = DateTime.Now,
                    Did = did,
                    Model = model ?? string.Empty,
                    Said = said ?? string.Empty
                });

                // Oldest first out, so a long day does not eat memory
                while (Entries.Count > Keep)
                    Entries.RemoveAt(0);
            }
        }

        public static void Ran()
        {
            lock (Gate)
            {
                LastRun = DateTime.Now;
            }
        }

        // Newest first, which is the order anybody reads a log in
        public static List<Entry> Recent()
        {
            lock (Gate)
            {
                var copy = new List<Entry>(Entries);

                copy.Reverse();

                return copy;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Entries.Clear();
            }
        }
    }
}
