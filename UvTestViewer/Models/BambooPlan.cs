using System;
using System.Collections.Generic;

namespace UvTestViewer.Models
{
    public class BambooPlan
    {
        public String Name { get; set; }
        public String PlanKey { get; set; }
        public IEnumerable<BambooBranch> Branches { get; set; }
    }
}