using System;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class Result
    {
        [JsonProperty]
        public Hyperlink Link { get; private set; }
        [JsonProperty]
        public Int64 ID { get; private set; }
        [JsonProperty]
        public String BuildResultKey { get; private set; }
        [JsonProperty]
        public String LifeCycleState { get; private set; }
        [JsonProperty]
        public String Key { get; private set; }
        [JsonProperty]
        public State State { get; private set; }
        [JsonProperty]
        public State BuildState { get; private set; }
        [JsonProperty]
        public Int64 Number { get; private set; }
        [JsonProperty]
        public Int64 BuildNumber { get; private set; }
    }
}
