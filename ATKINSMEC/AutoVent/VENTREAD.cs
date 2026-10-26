using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ATKINSMEC
{
    // EVERYTHING AUTOVENT'S WINDOW READS FROM THE MODEL, read in ONE place
    // and only where the API allows it: the window's constructor (inside
    // the command) or the external event (ExtEventHan). The window holds
    // the answers as plain data and works from them.
    //
    // It used to read the links - every room of every ticked link, and
    // with apartments every room of EVERY loaded link and a GetRoomAtPoint
    // for each - straight from its own tick and button handlers. Autodesk
    // does not support API calls outside an API context; read-only ones
    // mostly work, which is how it went unnoticed, but nothing promises
    // they will. It was also SLOW: one tick re-read everything.
    //
    // Now a tick asks only for what the window has not read yet (a link
    // ticked for the first time, or the apartments when their ticks
    // change), and Refresh clears what it holds so everything is read
    // afresh. What the window asks for is an Ask; what comes back, an
    // Answer.
    public static class VENTREAD
    {
        public class Ask
        {
            // The links list: every placement, its label and its span of
            // this model's levels (the slow part - ElementLevelFilter)
            public bool Links;

            // Rooms of these links (RVTLINKSELECTION.ReadRooms)
            public HashSet<long> Rooms = new HashSet<long>();

            // The apartments map for the Apartments ticks as they are when
            // this runs - and, with it, the rooms of every placement found
            // inside an apartment that the window has not got (Have)
            public bool Apartments;
            public HashSet<long> Have = new HashSet<long>();

            // Each model's fixture families, for the Fixtures dialog
            public bool Fixtures;

            // What every room's terminals carry now, for Arrange rooms...
            // (REVITROOMS, Mode.Survey - the ticked links, every level)
            public bool Terminals;

            public bool Any
            {
                get { return Links || Rooms.Count > 0 || Apartments || Fixtures || Terminals; }
            }

            // Two asks raised before the event ran become one
            public void Add(Ask other)
            {
                if (other == null)
                    return;

                Links |= other.Links;
                Apartments |= other.Apartments;
                Fixtures |= other.Fixtures;
                Terminals |= other.Terminals;

                Rooms.UnionWith(other.Rooms);
                Have.UnionWith(other.Have);
            }
        }

        public class LinkRow
        {
            public long Id;
            public string Label = string.Empty;
            public string Span = string.Empty;
        }

        public class Answer
        {
            // Always - cheap, and what every room is placed against
            public List<VENTLEVELMAP.Storey> Storeys = new List<VENTLEVELMAP.Storey>();

            public List<LinkRow> Links;      // null when not asked

            public Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>> Rooms =
                new Dictionary<long, List<RVTLINKSELECTION.LinkedRoom>>();

            // With each link's rooms, its rooms that have no area
            // (VENTUNENCLOSED) - read in the same trip
            public Dictionary<long, List<VENTUNENCLOSED.Room>> Unenclosed =
                new Dictionary<long, List<VENTUNENCLOSED.Room>>();

            // When asked: the map, and the Apartments ticks it was built
            // for (ApartmentsKey) - the window compares that with its own
            // ticks, so a change made while this ran is asked for again
            public VENTAPARTMENTS.Map Apartments;
            public string ApartmentsFor;

            // Source (0 = this model) -> family -> how many
            public Dictionary<long, Dictionary<string, int>> Fixtures;

            // Room key -> what its terminals carry now; null when not asked
            public Dictionary<string, VENTFLOWS.Air> Terminals;

            // With it: each unit's rooms and fresh air connections, and each
            // room's supply terminals whose ducts reach no unit
            public List<VENTFLOWS.Unit> Units;
            public Dictionary<string, List<long>> Unreached;

            // What could not be read, for the status bar
            public List<string> Trouble = new List<string>();
        }

        // What a map is built for, as one string, to tell it from one built
        // for something else: the Apartments ticks, AND which placements are
        // interior models (VENTINTERIOR) - a new keyword or a changed pick
        // builds the map again
        public static string ApartmentsKey()
        {
            return VENTINTERIOR.Key(
                VENTSETTINGS.ApartmentLinkIds,
                VENTSETTINGS.InteriorKeyword,
                VENTSETTINGS.InteriorChosen,
                VENTSETTINGS.InteriorPicks);
        }

        public static Answer Do(Document doc, Ask ask)
        {
            var answer = new Answer();

            ask = ask ?? new Ask();

            // EVERYTHING ASKED FOR IS ANSWERED, if only with nothing. The
            // window asks again for whatever is still missing, so an ask
            // left unanswered would be asked forever.
            if (doc == null || !doc.IsValidObject)
            {
                answer.Trouble.Add("the model this window was opened on is closed");

                if (ask.Links)
                    answer.Links = new List<LinkRow>();

                foreach (long linkId in ask.Rooms)
                    answer.Rooms[linkId] = new List<RVTLINKSELECTION.LinkedRoom>();

                if (ask.Apartments)
                {
                    answer.Apartments = new VENTAPARTMENTS.Map();
                    answer.ApartmentsFor = ApartmentsKey();
                }

                if (ask.Fixtures)
                    answer.Fixtures = new Dictionary<long, Dictionary<string, int>>();

                if (ask.Terminals)
                    answer.Terminals = new Dictionary<string, VENTFLOWS.Air>();

                return answer;
            }

            try
            {
                answer.Storeys = RVTLINKSELECTION.GetHostLevels(doc);
            }
            catch (Exception ex)
            {
                answer.Trouble.Add("this model's levels: " + ex.Message);
            }

            Dictionary<long, string> labels = null;

            try
            {
                labels = RVTLINKSELECTION.LinkLabels(doc);
            }
            catch
            {
                labels = new Dictionary<long, string>();
            }

            if (ask.Links)
            {
                answer.Links = new List<LinkRow>();

                try
                {
                    foreach (RVTLINKSELECTION.LinkItem link in RVTLINKSELECTION.GetLinks(doc))
                    {
                        string span;

                        try
                        {
                            span = RVTLINKSELECTION.LevelSpan(doc, link.Id, answer.Storeys);
                        }
                        catch
                        {
                            span = string.Empty;
                        }

                        answer.Links.Add(new LinkRow { Id = link.Id, Label = link.ToString(), Span = span });
                    }
                }
                catch (Exception ex)
                {
                    answer.Trouble.Add("the links: " + ex.Message);
                }
            }

            var rooms = new HashSet<long>(ask.Rooms);

            if (ask.Apartments)
            {
                answer.ApartmentsFor = ApartmentsKey();

                try
                {
                    answer.Apartments = VENTAPARTMENTS.Build(doc, "Levels and rooms");
                }
                catch (Exception ex)
                {
                    // An empty map rather than none: the window would
                    // otherwise ask again on every tick
                    answer.Apartments = new VENTAPARTMENTS.Map();
                    answer.Trouble.Add("the redundant rooms: " + ex.Message);
                }

                // The interior placements found are read in the same trip
                foreach (long found in answer.Apartments.FoundIn)
                {
                    if (!ask.Have.Contains(found))
                        rooms.Add(found);
                }
            }

            foreach (long linkId in rooms)
            {
                try
                {
                    answer.Rooms[linkId] = RVTLINKSELECTION.ReadRooms(doc, linkId, labels);

                    try
                    {
                        answer.Unenclosed[linkId] = RVTLINKSELECTION.ReadUnenclosed(doc, linkId, labels);
                    }
                    catch
                    {
                        // the rooms stand without the list of those with no area
                        answer.Unenclosed[linkId] = new List<VENTUNENCLOSED.Room>();
                    }
                }
                catch (Exception ex)
                {
                    answer.Rooms[linkId] = new List<RVTLINKSELECTION.LinkedRoom>();
                    answer.Trouble.Add("rooms of " + (labels.ContainsKey(linkId) ? labels[linkId] : "link " + linkId) +
                                       ": " + ex.Message);
                }
            }

            if (ask.Fixtures)
            {
                answer.Fixtures = new Dictionary<long, Dictionary<string, int>>();

                var sources = new List<long> { 0 };

                try
                {
                    sources.AddRange(RVTLINKSELECTION.GetLinks(doc).Select(l => l.Id));
                }
                catch
                {
                }

                foreach (long source in sources)
                {
                    try
                    {
                        answer.Fixtures[source] = VENTFIXTURES.Families(doc, new[] { source })
                            .ToDictionary(f => f.Name, f => f.Count, StringComparer.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        answer.Fixtures[source] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        answer.Trouble.Add("fixtures of " + (source == 0 ? "this model" : "link " + source) +
                                           ": " + ex.Message);
                    }
                }
            }

            // The airflow run's own walk, writing nothing (REVITROOMS
            // catches its own trouble and says it in Trouble)
            if (ask.Terminals)
            {
                var survey = new REVITROOMS { Doing = REVITROOMS.Mode.Survey };

                survey.Rooms(doc);

                answer.Terminals = survey.Carrying ?? new Dictionary<string, VENTFLOWS.Air>();
                answer.Units = survey.Units ?? new List<VENTFLOWS.Unit>();
                answer.Unreached = survey.Unreached ?? new Dictionary<string, List<long>>();

                if (!string.IsNullOrEmpty(survey.Trouble))
                    answer.Trouble.Add("the terminals: " + survey.Trouble);
            }

            return answer;
        }
    }
}
