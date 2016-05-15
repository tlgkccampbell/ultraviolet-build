using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class ResultCollection
    {
        [JsonProperty]
        public Int32 Size { get; private set; }
        [JsonProperty("start-index")]
        public Int32 StartIndex { get; private set; }
        [JsonProperty("max-result")]
        public Int32 MaxResult { get; private set; }
        [JsonProperty]
        public IEnumerable<Result> Result { get; private set; }
    }
}
