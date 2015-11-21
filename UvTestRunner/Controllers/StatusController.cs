using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UvTestRunner.Services;

namespace UvTestRunner.Controllers
{
    public class StatusController : ApiController
    {
        [Route("api/status")]
        public HttpResponseMessage Get(String workingDirectories)
        {
            var statuses = workingDirectories.Split(';').Select(x => testRunService.GetMostRecentStatusByWorkingDirectory(x)).ToList();

            if (statuses.Any(x => x == Models.TestRunStatus.Failed))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ff0000") };

            if (statuses.Any(x => x == Models.TestRunStatus.Pending || x == Models.TestRunStatus.Running))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ffff00") };

            if (statuses.Any(x => x == Models.TestRunStatus.Succeeded))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#00ff00") };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#ffffff") };
        }

        private readonly TestRunService testRunService = new TestRunService();
    }
}
