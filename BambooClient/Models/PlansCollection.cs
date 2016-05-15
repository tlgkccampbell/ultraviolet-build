using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class PlansCollection
    {
        [JsonProperty]
        public Int32 Size { get; set; }
        [JsonProperty("start-index")]
        public Int32 StartIndex { get; set; }
        [JsonProperty("max-result")]
        public Int32 MaxResult { get; set; }
        [JsonProperty]
        public IEnumerable<Plan> Plan { get; set; }
    }
}
