using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Just enough of CHWCLOUD for NWCSETTINGS to compile with no Revit
    // present. NWCJOB.ToCloudModel is the only thing that reaches for
    // it, and none of these tests calls that.
    public class CHWCLOUD
    {
        public enum WorksetMode
        {
            All = 0,
            Editable = 1
        }

        public class CloudModel
        {
            public string Name { get; set; }
            public string Region { get; set; }
            public Guid ProjectGuid { get; set; }
            public Guid ModelGuid { get; set; }

            public WorksetMode Worksets { get; set; }

            public List<string> SystemTypes { get; set; }
                = new List<string>();
        }
    }
}
