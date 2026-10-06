using System.Collections.Generic;

namespace ATKINSMEC
{
    // One line of the window, with no Revit in it.
    //
    // EQUIPForm is MODELESS, so it may not touch the API at all - it is
    // handed plain data by EQUIPEVENT and hands plain data back. This is
    // the shape of that data.
    public class EQUIPROW
    {
        public long Id { get; set; }

        public string Name { get; set; }
        public string Category { get; set; }
        public string TypeNow { get; set; }

        public int Connectors { get; set; }

        // Filled in by Look; empty until then
        public string Follows { get; set; }

        public string Detail { get; set; }

        public EQUIPROW()
        {
            Name = string.Empty;
            Category = string.Empty;
            TypeNow = string.Empty;
            Follows = string.Empty;
            Detail = string.Empty;
        }
    }

    // One type on offer
    public class EQUIPTYPEROW
    {
        public long Id { get; set; }

        public string Label { get; set; }

        // True when this is what the selection already is, so the
        // window can say so rather than letting somebody "change" a
        // unit to the type it already has and wonder why nothing
        // happened - which is exactly what it looks like from outside
        public bool Current { get; set; }

        public EQUIPTYPEROW()
        {
            Label = string.Empty;
        }
    }

    // Everything the window needs after a read of the selection
    public class EQUIPPICTURE
    {
        public List<EQUIPROW> Rows = new List<EQUIPROW>();

        public List<EQUIPTYPEROW> Types = new List<EQUIPTYPEROW>();

        public string Said = string.Empty;
    }
}
