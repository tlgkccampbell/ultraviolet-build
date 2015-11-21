using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Xml.Linq;

namespace UvBuildStatus.Controllers
{
    [Route("status")]
    public class StatusController : ApiController
    {
        public async Task<HttpResponseMessage> Get(String planKeys)
        {
            var failed = false;

            foreach (var planKey in planKeys.Split(';'))
            {
                var planKeySucceeded = await QueryPlan(planKey);
                if (!planKeySucceeded)
                {
                    failed = true;
                    break;
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(failed ? "#ff0000" : "#00ff00")
            };
        }

        private static Uri GetBambooRelativeUri(String uri)
        {
            var uriRoot = new Uri(ConfigurationManager.AppSettings["BambooServerUri"]);
            var uriPath = new Uri(uri, UriKind.Relative);
            return new Uri(uriRoot, uriPath);
        }

        private async Task<Boolean> QueryPlan(String planKey)
        {
            var branches = await EnumerateBranches(planKey);

            var masterResult = await QueryBranch(planKey, null);
            if (!masterResult)
                return false;

            foreach (var branch in branches)
            {
                var branchResult = await QueryBranch(planKey, branch);
                if (!branchResult)
                    return false;
            }

            return true;
        }

        private async Task<Boolean> QueryBranch(String planKey, String branch)
        {
            using (var client = new HttpClient())
            {
                var uri = GetBambooRelativeUri(String.Format("rest/api/latest/result/{0}/", planKey));

                if (!String.IsNullOrEmpty(branch))
                    uri = new Uri(uri, new Uri(String.Format("branch/{0}", branch), UriKind.Relative));

                using (var stream = await client.GetStreamAsync(uri))
                using (var reader = new StreamReader(stream))
                {
                    var resultString = await reader.ReadToEndAsync();
                    var resultXml = XDocument.Parse(resultString);

                    var buildStateString = (String)resultXml.Descendants("result").OrderByDescending(x => (Int64)x.Attribute("number"))
                        .Select(x => x.Attribute("state")).FirstOrDefault();

                    if (String.Equals("Failed", buildStateString, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            return true;
        }

        private async Task<IEnumerable<String>> EnumerateBranches(String planKey)
        {
            using (var client = new HttpClient())
            {
                var uri = GetBambooRelativeUri(String.Format("rest/api/latest/plan/{0}/branch", planKey));

                using (var stream = await client.GetStreamAsync(uri))
                using (var reader = new StreamReader(stream))
                {
                    var resultString = await reader.ReadToEndAsync();
                    var resultXml = XDocument.Parse(resultString);

                    return resultXml.Descendants("branch").Select(x => (String)x.Attribute("shortName"));
                }
            }
        }
    }
}
