using Newtonsoft.Json;

namespace BambooClient.Models
{
    public class PlanBranchesResponse
    {
        [JsonProperty]
        public Hyperlink Link { get; private set; }
        [JsonProperty("branches")]
        public PlanBranchesCollection PlanBranches { get; private set; }
    }
}
