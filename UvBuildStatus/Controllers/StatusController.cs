using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using BambooClient;
using BambooClient.Models;

namespace UvBuildStatus.Controllers
{
    [Route("status")]
    public class StatusController : ApiController
    {
        public async Task<HttpResponseMessage> Get(String planKeys, Boolean dim = false)
        {
            var splitPlanKeys = String.IsNullOrEmpty(planKeys) ? null : planKeys.Split(';');
            if (splitPlanKeys == null)
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            var failed = false;

            using (var bamboo = CreateBambooClient())
            {
                foreach (var planKey in splitPlanKeys)
                {
                    var masterResults = await bamboo.EnumerateLatestResults(planKey);
                    var masterState = masterResults.OrderByDescending(x => x.BuildNumber).Select(x => x.BuildState).FirstOrDefault();
                    if (masterState == State.Failed)
                    {
                        failed = true;
                        break;
                    }

                    var branches = await bamboo.EnumeratePlanBranches(planKey);
                    foreach (var branch in branches)
                    {
                        var branchResults = await bamboo.EnumerateLatestResults(planKey, branch.ShortName);
                        var branchState = branchResults.OrderByDescending(x => x.BuildNumber).Select(x => x.BuildState).FirstOrDefault();
                        if (branchState == State.Failed)
                        {
                            failed = true;
                            break;
                        }
                    }
                }
            }
            
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(dim ? 
                    failed ? "#400000" : "#004000" :
                    failed ? "#ff0000" : "#00ff00")
            };
        }

        private static BambooHttpClient CreateBambooClient()
        {
            var uri = new Uri(ConfigurationManager.AppSettings["BambooServerUri"]);
            return new BambooHttpClient(uri);
        }    
    }
}
