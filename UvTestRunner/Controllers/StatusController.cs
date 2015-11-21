using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using BambooClient;
using UvTestRunner.Services;

namespace UvTestRunner.Controllers
{
    public class StatusController : ApiController
    {
        [Route("api/status")]
        public async Task<HttpResponseMessage> Get()
        {
            var excludedPlans = (Settings.Default.BambooExcludedPlans ?? String.Empty).Split(';');

            var workingDirectories = new List<String>();
            using (var bamboo = new BambooHttpClient(new Uri(Settings.Default.BambooServerUri)))
            {
                var plans = await bamboo.EnumeratePlans();
                foreach (var plan in plans)
                {
                    if (excludedPlans.Contains(plan.Key))
                        continue;

                    var planWorkingDirectory = String.Format(Settings.Default.BambooWorkingDirectoryPattern, plan.Key);
                    workingDirectories.Add(planWorkingDirectory);

                    var branches = await bamboo.EnumeratePlanBranches(plan.Key);
                    foreach (var branch in branches)
                    {
                        var branchWorkingDirectory = String.Format(Settings.Default.BambooWorkingDirectoryPattern, branch.Key);
                        workingDirectories.Add(branchWorkingDirectory);
                    }
                }
            }

            var testRunService = new TestRunService();
            var status = testRunService.GetMostRecentStatusByWorkingDirectories(workingDirectories);

            if (status == Models.TestRunStatus.Failed)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ff0000") };

            if (status == Models.TestRunStatus.Pending || status == Models.TestRunStatus.Running)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ffff00") };

            if (status == Models.TestRunStatus.Succeeded)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#00ff00") };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ffffff") };
        }        
    }
}
