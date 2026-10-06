using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // One workset somebody picked, remembered by BOTH of the things
    // that can identify it.
    //
    // Neither on its own is enough:
    //
    //   Name      is what a person reads and types, and is the only
    //             thing that means anything in a settings file - but it
    //             changes the moment somebody renames the workset.
    //
    //   UniqueId  survives a rename, and Autodesk's own words are that
    //             it is a "Stable identifier for a workset, UniqueId
    //             will not change while synchronizing with central" -
    //             but it is opaque, and it is not there at all in a
    //             settings file written before this existed.
    //
    // So both are stored and the id is tried first. Note what is NOT
    // stored: WorksetId. The API is explicit that the id "may be
    // changed while synchronizing with central", so a persisted one
    // rots. It is resolved from the model immediately before use.
    //
    // No Revit in this file, so the settings and the picker window can
    // both hold it.
    public class WORKSETPICK
    {
        public string Name { get; set; }

        // Empty for a pick read back from an older settings file, and
        // for anything matched by name alone
        public string UniqueId { get; set; }

        public WORKSETPICK()
        {
            Name = string.Empty;
            UniqueId = string.Empty;
        }

        public WORKSETPICK(string name, string uniqueId)
        {
            Name = name ?? string.Empty;
            UniqueId = uniqueId ?? string.Empty;
        }

        public bool HasId
        {
            get { return !string.IsNullOrEmpty(UniqueId); }
        }

        public override string ToString()
        {
            return Name;
        }

        // Is this the same workset as that one? The id wins where both
        // have one; otherwise the name, which is how an old settings
        // file and a model still find each other.
        public bool Same(WORKSETPICK other)
        {
            if (other == null)
                return false;

            if (HasId && other.HasId)
            {
                return string.Equals(UniqueId, other.UniqueId,
                    StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(Name, other.Name,
                StringComparison.OrdinalIgnoreCase);
        }

        public static WORKSETPICK Find(
            List<WORKSETPICK> among, WORKSETPICK wanted)
        {
            if (among == null || wanted == null)
                return null;

            // The id first, across the whole list, BEFORE falling back
            // to names - otherwise a renamed workset would be matched
            // by name to whatever now carries its old name, which is
            // the one wrong answer worse than no answer
            if (wanted.HasId)
            {
                foreach (WORKSETPICK had in among)
                {
                    if (had.HasId &&
                        string.Equals(had.UniqueId, wanted.UniqueId,
                            StringComparison.OrdinalIgnoreCase))
                        return had;
                }
            }

            foreach (WORKSETPICK had in among)
            {
                if (string.Equals(had.Name, wanted.Name,
                        StringComparison.OrdinalIgnoreCase))
                    return had;
            }

            return null;
        }

        public static List<WORKSETPICK> Copy(List<WORKSETPICK> these)
        {
            var copy = new List<WORKSETPICK>();

            if (these == null)
                return copy;

            foreach (WORKSETPICK one in these)
                copy.Add(new WORKSETPICK(one.Name, one.UniqueId));

            return copy;
        }
    }
}
