using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class PlanBranchesCollection
    {
        [JsonProperty]
        public Int32 Size { get; set; }
        [JsonProperty("start-index")]
        public Int32 StartIndex { get; set; }
        [JsonProperty("max-result")]
        public Int32 MaxResult { get; set; }
        [JsonProperty("branch")]
        public IEnumerable<PlanBranch> PlanBranch { get; set; }
    }
}
