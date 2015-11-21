using System;
using System.Web.Http;
using UvTestRunner.Models;
using UvTestRunner.Services;

namespace UvTestRunner.Controllers
{
    public class TestRunnerController : ApiController
    {
        [Route("api/uvtest")]
        public IHttpActionResult Post([FromBody] String workingDirectory)
        {
            var testRunID = TestRunnerQueueService.Instance.Create(workingDirectory);
            return Ok(new TestRunCreationResponse() { TestRunID = testRunID });
        }

        [Route("api/uvtest/{id}")]
        public IHttpActionResult Get(Int64 id)
        {
            var run = TestRunnerQueueService.Instance.RunnerService.GetByID(id);
            if (run == null)
                return NotFound();

            return Ok(new TestRunStatusResponse() { TestRunID = id, TestRunStatus = run.Status });
        }
    }
}
