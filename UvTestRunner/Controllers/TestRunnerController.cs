using System;
using System.Web.Http;
using UvTestRunner.Models;
using UvTestRunner.Services;

namespace UvTestRunner.Controllers
{
    public class TestRunnerController : ApiController
    {
        [Route("api/uvtest")]
        public IHttpActionResult Post([FromBody] String metadata)
        {
            var metadataParts = metadata?.Split(',');
            if (metadataParts?.Length < 2 || metadataParts?.Length > 3)
                return BadRequest("Invalid request metadata.");

            var testAssembly = metadataParts[0];
            var workingDirectory = metadataParts[1];
            var testFramework = metadataParts.Length > 2 ? metadataParts[2] : null;

            var testRunID = TestRunQueueService.Instance.Create(workingDirectory, testAssembly, testFramework);
            return Ok(new TestRunCreationResponse() { TestRunID = testRunID });
        }

        [Route("api/uvtest/{id}")]
        public IHttpActionResult Get(Int64 id)
        {
            var run = testRunService.GetByID(id);
            if (run == null)
                return NotFound();

            return Ok(new TestRunStatusResponse() { TestRunID = id, TestRunStatus = run.Status });
        }

        private readonly TestRunService testRunService = new TestRunService();
    }
}
