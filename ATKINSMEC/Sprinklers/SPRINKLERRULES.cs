using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // NFPA 13 occupancy hazard classifications and the spacing they
    // allow for standard-coverage pendent sprinklers.
    public class SPRINKLERRULES
    {
        public enum Hazard
        {
            Light,
            OrdinaryI,
            OrdinaryII,
            ExtraI,
            ExtraII
        }

        public class HazardRule
        {
            public Hazard Hazard { get; set; }
            public string Name { get; set; }

            // Maximum protection area per sprinkler, m2
            public double MaxCoverageM2 { get; set; }

            // Maximum distance between sprinklers, m
            public double MaxSpacingM { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        // NFPA 13 standard spray, unobstructed construction:
        //   Light          225 sq ft / 15 ft
        //   Ordinary I+II  130 sq ft / 15 ft
        //   Extra I+II     100 sq ft / 12 ft
        public static readonly List<HazardRule> Rules = new List<HazardRule>
        {
            new HazardRule { Hazard = Hazard.Light,      Name = "Light",       MaxCoverageM2 = 20.9, MaxSpacingM = 4.6 },
            new HazardRule { Hazard = Hazard.OrdinaryI,  Name = "Ordinary I",  MaxCoverageM2 = 12.1, MaxSpacingM = 4.6 },
            new HazardRule { Hazard = Hazard.OrdinaryII, Name = "Ordinary II", MaxCoverageM2 = 12.1, MaxSpacingM = 4.6 },
            new HazardRule { Hazard = Hazard.ExtraI,     Name = "Extra I",     MaxCoverageM2 = 9.3,  MaxSpacingM = 3.7 },
            new HazardRule { Hazard = Hazard.ExtraII,    Name = "Extra II",    MaxCoverageM2 = 9.3,  MaxSpacingM = 3.7 }
        };

        // Minimum distance between two sprinklers, m (NFPA 13: 6 ft)
        public const double MinSpacingM = 1.8;

        // Minimum distance from a wall, m (NFPA 13: 4 in)
        public const double MinWallClearanceM = 0.1;

        // What the sprinkler itself needs to clear a wall. NFPA's 100 mm
        // is measured to the centre of the head, which says nothing about
        // the cover plate around it, so the working clearance is set
        // here and never allowed below the standard's minimum.
        public static double WallClearanceMm = 150;

        public static double WallClearanceM
        {
            get
            {
                double metres = WallClearanceMm / 1000.0;

                return metres < MinWallClearanceM
                    ? MinWallClearanceM
                    : metres;
            }
        }

        public static HazardRule RuleFor(Hazard hazard)
        {
            return Rules.First(r => r.Hazard == hazard);
        }

        // ==================================================
        // Guessing the hazard from the room name
        //
        // English and German keywords, since architectural models are
        // often named in the local language. The guess is only a
        // starting point - every room can be overridden in the table.
        // ==================================================

        private static readonly Dictionary<Hazard, string[]> Keywords =
            new Dictionary<Hazard, string[]>
            {
                {
                    Hazard.Light, new[]
                    {
                        "office", "bureau", "buro", "büro",
                        "bedroom", "zimmer", "schlaf", "room",
                        "living", "wohn", "dining", "ess",
                        "class", "school", "lecture", "seminar",
                        "hotel", "ward", "patient", "clinic",
                        "toilet", "wc", "bath", "bad", "dusche", "shower",
                        "corridor", "flur", "gang", "hall", "lobby", "foyer",
                        "reception", "empfang", "meeting", "conference",
                        "besprech", "lounge", "seating", "church"
                    }
                },
                {
                    Hazard.OrdinaryI, new[]
                    {
                        "parking", "garage", "park",
                        "laundry", "wasch", "waschraum",
                        "electrical", "elektro", "mechanical", "technik",
                        "plant", "mep", "pump", "riser",
                        "restaurant", "cafeteria", "canteen", "kantine",
                        "bakery", "baecker", "bäcker", "dairy"
                    }
                },
                {
                    Hazard.OrdinaryII, new[]
                    {
                        "kitchen", "kuche", "küche", "kueche",
                        "storage", "lager", "abstell", "store",
                        "workshop", "werkstatt", "repair", "machine",
                        "library", "biblio", "stack",
                        "retail", "shop", "verkauf", "laundry room",
                        "post", "mail", "printing", "druck"
                    }
                },
                {
                    Hazard.ExtraI, new[]
                    {
                        "woodwork", "holz", "saw", "saege", "säge",
                        "plywood", "textile", "foundry", "metal work"
                    }
                },
                {
                    Hazard.ExtraII, new[]
                    {
                        "paint", "lack", "spray", "spritz",
                        "flammable", "brennbar", "solvent", "loesemittel",
                        "plastic", "kunststoff", "asphalt", "varnish"
                    }
                }
            };

        // Unknown names fall back to Ordinary I: tighter than Light, so
        // an unrecognised room is not under-protected by accident.
        public const Hazard DefaultHazard = Hazard.OrdinaryI;

        public static Hazard Guess(string roomName, out bool recognised)
        {
            recognised = false;

            if (string.IsNullOrEmpty(roomName))
                return DefaultHazard;

            string name = roomName.ToLowerInvariant();

            // Most demanding match wins, so a "paint store" is treated as
            // Extra rather than Ordinary
            foreach (Hazard hazard in new[]
                     {
                         Hazard.ExtraII,
                         Hazard.ExtraI,
                         Hazard.OrdinaryII,
                         Hazard.OrdinaryI,
                         Hazard.Light
                     })
            {
                foreach (string keyword in Keywords[hazard])
                {
                    if (name.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                    {
                        recognised = true;
                        return hazard;
                    }
                }
            }

            return DefaultHazard;
        }
    }
}
