using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // THE REVIT-FREE HALF OF VENTAPARTMENTS: what is decided once the
    // model has been asked where everything stands. VENTAPARTMENTS.cs
    // does the asking (GetRoomAtPoint, the transforms, the floors); this
    // file decides which apartment a room joins and keeps the record the
    // window, the export, the airflow write and the fixture count read.
    // Split out so scratchpad/ventleveltest can run the decision itself
    // rather than a copy of it.
    public static partial class VENTAPARTMENTS
    {
        public class Apartment
        {
            public string Key = string.Empty;    // RVTLINKSELECTION.RoomKey of the apartment room
            public string Name = string.Empty;
            public string Number = string.Empty;
        }

        // One apartments model's answer for one interior room: the
        // apartment it stands in there, and the storey of THIS model that
        // apartment's floor is on
        public class Candidate
        {
            public long Source;
            public Apartment Apartment;
            public int Storey;
        }

        public class Map
        {
            // The apartment links that could be read, in the order asked
            public List<long> Sources = new List<long>();

            // Room key -> the apartment it stands in
            public Dictionary<string, Apartment> ByRoom = new Dictionary<string, Apartment>();

            // APARTMENTS WITH A ROOM INSIDE. These are not rooms of their
            // own any more - no row in the sheet, no terminal or WC put in
            // them - because what is inside them is. An apartment with
            // nothing inside stays an ordinary room.
            public HashSet<string> Holding = new HashSet<string>();

            // The placements a room inside an apartment was found in -
            // ticked or not
            public HashSet<long> FoundIn = new HashSet<long>();

            // Placement -> apartment link -> the storeys (indexes into
            // Storeys) where its rooms were found inside those apartments
            public Dictionary<long, Dictionary<long, SortedSet<int>>> Pairs =
                new Dictionary<long, Dictionary<long, SortedSet<int>>>();

            // Rooms that stood inside an apartment in space but on another
            // storey of this model - not grouped, and counted once each
            public int OtherStorey;

            // ROOMS OF AN INTERIOR MODEL THAT STOOD IN NO APARTMENT AT ALL -
            // searched on their storey, and no apartments model had a room
            // there. They are in no run (an interior placement gives only
            // the rooms inside apartments), so they are COUNTED rather than
            // dropped in silence: user-reported as "the apartments rooms
            // from IND model are not exported", with nothing on screen to
            // say why. By storey (index into Storeys) as well as in all.
            public int Outside;
            public SortedDictionary<int, int> OutsideOn = new SortedDictionary<int, int>();

            public List<VENTLEVELMAP.Storey> Storeys = new List<VENTLEVELMAP.Storey>();
            public Dictionary<long, string> Labels = new Dictionary<long, string>();

            public bool On
            {
                get { return Sources.Count > 0; }
            }

            public string Label
            {
                get { return string.Join(", ", Sources.Select(LabelOf)); }
            }

            public string LabelOf(long linkId)
            {
                string label;
                return Labels.TryGetValue(linkId, out label) ? label : "link " + linkId;
            }

            public Apartment Of(string roomKey)
            {
                Apartment found;
                return ByRoom.TryGetValue(roomKey, out found) ? found : null;
            }

            // WHICH APARTMENT A ROOM JOINS, from what each apartments model
            // said about it (in the order the models were asked).
            //
            // VERIFIED ON THIS MODEL'S LEVELS: only an apartment whose
            // floor is on the SAME storey of the main model as the room's
            // own floor - by the band rule everything else uses
            // (VENTLEVELMAP.StoreyOf). Standing inside it in space is not
            // enough: a duplex's upper bedroom stands inside the apartment
            // drawn on the floor below, and an interior placement dropped
            // onto the wrong floor stands inside the apartments of the
            // floor it was dropped on. Either would put a room under an
            // apartment on another level, and the sheet groups by level.
            //
            // The first model with an apartment on the right storey wins.
            // A room that stood inside apartments only on other storeys is
            // counted ONCE, however many models said so; one that stood in
            // none at all is counted as Outside.
            public Candidate Settle(long placement, string roomKey, int roomStorey, IEnumerable<Candidate> candidates)
            {
                if (roomStorey < 0)
                    return null;

                bool elsewhere = false;

                foreach (Candidate candidate in candidates ?? Enumerable.Empty<Candidate>())
                {
                    if (candidate == null || candidate.Apartment == null)
                        continue;

                    if (candidate.Storey != roomStorey)
                    {
                        elsewhere = true;
                        continue;
                    }

                    ByRoom[roomKey] = candidate.Apartment;
                    Holding.Add(candidate.Apartment.Key);
                    FoundIn.Add(placement);

                    Dictionary<long, SortedSet<int>> bySource;

                    if (!Pairs.TryGetValue(placement, out bySource))
                    {
                        bySource = new Dictionary<long, SortedSet<int>>();
                        Pairs.Add(placement, bySource);
                    }

                    SortedSet<int> storeys;

                    if (!bySource.TryGetValue(candidate.Source, out storeys))
                    {
                        storeys = new SortedSet<int>();
                        bySource.Add(candidate.Source, storeys);
                    }

                    storeys.Add(roomStorey);

                    return candidate;
                }

                if (elsewhere)
                {
                    OtherStorey++;
                }
                else
                {
                    Outside++;

                    int n;
                    OutsideOn.TryGetValue(roomStorey, out n);
                    OutsideOn[roomStorey] = n + 1;
                }

                return null;
            }

            // THE LINKS A RUN READS ROOMS FROM: the ticked ones, and every
            // placement a room inside an apartment was found in
            public List<long> RoomLinks(IEnumerable<long> ticked)
            {
                var links = new List<long>();

                foreach (long id in (ticked ?? new List<long>()).Concat(FoundIn.OrderBy(i => i)))
                {
                    if (!links.Contains(id))
                        links.Add(id);
                }

                return links;
            }

            // A room is read when its link is ticked, or when it stands
            // inside an apartment - an interior placement nobody ticked
            // gives only the rooms that belong in an apartment
            public bool Takes(long linkId, string roomKey, ICollection<long> ticked)
            {
                return (ticked != null && ticked.Contains(linkId)) || ByRoom.ContainsKey(roomKey);
            }

            // "IND-000030.rvt : 3  ->  ARC-000020.rvt : 1 apartments on
            // L04_LEVEL 04 to L10_LEVEL 10", one line per pairing
            public List<string> Describe()
            {
                var lines = new List<string>();

                foreach (KeyValuePair<long, Dictionary<long, SortedSet<int>>> placement in
                         Pairs.OrderBy(p => LabelOf(p.Key), StringComparer.OrdinalIgnoreCase))
                {
                    foreach (KeyValuePair<long, SortedSet<int>> source in placement.Value.OrderBy(s => LabelOf(s.Key), StringComparer.OrdinalIgnoreCase))
                    {
                        lines.Add(LabelOf(placement.Key) + "  ->  " + LabelOf(source.Key) +
                                  " apartments on " + VENTLEVELMAP.Floors(source.Value, Storeys));
                    }
                }

                return lines;
            }
        }
    }
}
