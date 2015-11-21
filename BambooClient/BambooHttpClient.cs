using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using BambooClient.Models;
using Newtonsoft.Json;

namespace BambooClient
{
    /// <summary>
    /// Represents a wrapper around <see cref="HttpClient"/> for accessing a Bamboo instance.
    /// </summary>
    public class BambooHttpClient : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BambooHttpClient"/> class.
        /// </summary>
        /// <param name="baseUri">The base URI of the Bamboo server.</param>
        public BambooHttpClient(Uri baseUri)
        {
            if (baseUri == null)
                throw new ArgumentNullException("baseUri");
            
            this.bambooUri = baseUri;
            this.bambooClient = new HttpClient();
            this.bambooClient.DefaultRequestHeaders.Accept.Clear();
            this.bambooClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Retrieves a collection containing the set of build plans defined on the Bamboo server.
        /// </summary>
        /// <returns>A collection of <see cref="Plan"/> objects which represent the build plans defined on the Bamboo server.</returns>
        public async Task<IEnumerable<Plan>> EnumeratePlans()
        {
            var response = await HttpGet<PlansResponse>(GetBambooRelativeUri("rest/api/latest/plan"));
            return response.Plans.Plan;
        }

        /// <summary>
        /// Retrieves a collection containing the set of plan branches defined for the specified build plan.
        /// </summary>
        /// <param name="planKey">The plan key that identifies the plan for which to retrieve branches.</param>
        /// <returns>A collection of <see cref="PlanBranch"/> objects which represent the plan branches defined for the specified build plan.</returns>
        public async Task<IEnumerable<PlanBranch>> EnumeratePlanBranches(String planKey)
        {
            if (String.IsNullOrEmpty(planKey))
                throw new ArgumentException("planKey");
            
            var response = await HttpGet<PlanBranchesResponse>(GetBambooRelativeUri("rest/api/latest/plan/{0}/branch", planKey));
            return response.PlanBranches.PlanBranch;
        }

        /// <summary>
        /// Retrieves a collection containing the latest results of builds for the specified plan.
        /// </summary>
        /// <param name="planKey">The plan key that identifies the plan for which to retrieve results.</param>
        /// <param name="branch">The branch for which to retrieve results, or <c>null</c> to retrieve the overall plan results.</param>
        /// <returns>A collection of <see cref="Result"/> objects which represents the latest results of the specified plan.</returns>
        public async Task<IEnumerable<Result>> EnumerateLatestResults(String planKey, String branch = null)
        {
            if (String.IsNullOrEmpty(planKey))
                throw new ArgumentException("planKey");

            if (branch == null)
            {
                var response = await HttpGet<ResultsResponse>(GetBambooRelativeUri("rest/api/latest/result/{0}", planKey));
                return response.Results.Result;
            }
            else
            {
                var response = await HttpGet<ResultsResponse>(GetBambooRelativeUri("rest/api/latest/result/{0}/branch/{1}", planKey, branch));
                return response.Results.Result;
            }
        }

        /// <summary>
        /// Releases resources associated with this object.
        /// </summary>
        /// <param name="disposing"><c>true</c> if the object is being disposed; <c>false</c> if the object is being finalized.</param>
        protected virtual void Dispose(Boolean disposing)
        {
            if (disposing)
            {
                if (bambooClient != null)
                    bambooClient.Dispose();
            }
        }

        /// <summary>
        /// Gets a URI relative to the Bamboo base URI.
        /// </summary>
        private Uri GetBambooRelativeUri(String uri, params Object[] args)
        {
            return new Uri(bambooUri, new Uri(String.Format(uri, args), UriKind.Relative));
        }

        /// <summary>
        /// Gets a URI relative to the Bamboo base URI.
        /// </summary>
        private Uri GetBambooRelativeUri(Uri uri)
        {
            return new Uri(bambooUri, uri);
        }
        
        /// <summary>
        /// Performs an HTTP GET operation.
        /// </summary>
        private async Task<TResponse> HttpGet<TResponse>(Uri uri)
        {
            using (var stream = await bambooClient.GetStreamAsync(uri))
            using (var reader = new StreamReader(stream))
            {
                var resultString = await reader.ReadToEndAsync();
                var resultObject = JsonConvert.DeserializeObject<TResponse>(resultString);

                return resultObject;
            }
        }

        /// <summary>
        /// Performs an HTTP POST operation.
        /// </summary>
        private async Task<TResponse> HttpPost<TResponse>(Uri uri, Object body)
        {
            var contentString = JsonConvert.SerializeObject(body);
            var content = new StringContent(contentString, Encoding.UTF8, "application/json");

            using (var response = await bambooClient.PostAsync(uri, content))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(String.Format("HTTP POST failed: {0} {1}", response.ReasonPhrase, response.StatusCode));

                var resultString = await response.Content.ReadAsStringAsync();
                var resultObject = JsonConvert.DeserializeObject<TResponse>(resultString);

                return resultObject;
            }
        }

        // State values.
        private readonly HttpClient bambooClient;
        private readonly Uri bambooUri;
    }
}
