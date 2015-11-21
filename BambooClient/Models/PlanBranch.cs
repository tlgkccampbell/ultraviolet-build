using System;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class PlanBranch
    {
        [JsonProperty]
        public Hyperlink Link { get; private set; }
        [JsonProperty]
        public String Description { get; private set; }
        [JsonProperty]
        public String ShortName { get; private set; }
        [JsonProperty]
        public String ShortKey { get; private set; }
        [JsonProperty]
        public String Key { get; private set; }
        [JsonProperty]
        public String Name { get; private set; }
        [JsonProperty]
        public Boolean Enabled { get; private set; }
    }
}
