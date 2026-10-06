using System.Collections.Generic;

namespace ATKINSMEC
{
    // What the ESP window is handed and hands back. Revit-free, so the
    // window renders in scratchpad/esppreview with no model - the same
    // reason EQUIPROW exists.

    // A linked model that can be ticked into the calculation
    public class ESPLINKROW
    {
        public long Id;
        public string Label = string.Empty;

        public override string ToString()
        {
            return Label;
        }
    }

    // A unit that can be calculated: mechanical equipment with duct
    // connectors, in this model or a linked one
    public class ESPUNITROW
    {
        public string Key = string.Empty;    // "link id:element id"
        public long LinkId;
        public long ElementId;
        public string Name = string.Empty;
        public string Mark = string.Empty;
        public string Model = string.Empty;
        public string Ports = string.Empty;  // "Supply, Return"
        public bool Picked;
    }

    // One press of Calculate
    public class ESPRUN
    {
        public List<ESPNET.Result> Results = new List<ESPNET.Result>();

        // The ESP parameter on each unit as it stands now, in Pa, by key;
        // null where the unit has no such parameter
        public Dictionary<string, double?> InModel = new Dictionary<string, double?>();

        public List<ESPNET.Bridge> Bridges = new List<ESPNET.Bridge>();
        public List<ESPNET.Bridge> NearMisses = new List<ESPNET.Bridge>();

        // One line per model read: what was in it
        public List<string> Models = new List<string>();

        public int Parts;
        public List<string> Missing = new List<string>();

        public bool TableBuiltIn;
        public string LogPath = string.Empty;
    }
}
