using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UvTestRunner.Models;

namespace UvTestRunnerClient
{
    public class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
                ExitWithError(1, "Unable to execute tests. Missing value for ${bamboo.agentWorkingDirectory}.");
            if (args.Length < 2)
                ExitWithError(2, "Unable to execute tests. Missing value for ${bamboo.build.working.directory}.");
            if (args.Length < 3)
                ExitWithError(3, "Unable to execute tests. Missing test assembly.");

            var agentWorkingDirectory = args[0];
            var buildWorkingDirectory = args[1];
            var testAssembly = args[2];
            var testFramework = args.Length > 3 ? args[3] : "nunit3";
            var testSuffix = args.Length > 4 ? args[4] : String.Empty;

            var isNetCore = String.Equals(testFramework, "nunit3core", StringComparison.Ordinal);

            var inputNameIntel = isNetCore ? Settings.Default.NetCoreInputNameIntel : Settings.Default.InputNameIntel;
            var inputNameNvidia = isNetCore ? Settings.Default.NetCoreInputNameNvidia : Settings.Default.InputNameNvidia;
            var inputNameAmd = isNetCore ? Settings.Default.NetCoreInputNameAmd : Settings.Default.InputNameAmd;

            var outputNameIntel = isNetCore ? Settings.Default.NetCoreOutputNameIntel : Settings.Default.OutputNameIntel;
            var outputNameNvidia = isNetCore ? Settings.Default.NetCoreOutputNameNvidia : Settings.Default.OutputNameNvidia;
            var outputNameAmd = isNetCore ? Settings.Default.NetCoreOutputNameAmd : Settings.Default.OutputNameAmd;

            Console.WriteLine("Spawning test runs.");

            var succeeded = Task.Run(() =>
            {
                var taskSpawnIntel = Task.Run(() => SpawnNewTestRun(Settings.Default.UvTestRunnerUrlIntel, agentWorkingDirectory, buildWorkingDirectory, testAssembly, testFramework, testSuffix));
                var taskSpawnNvidia = Task.Run(() => SpawnNewTestRun(Settings.Default.UvTestRunnerUrlNvidia, agentWorkingDirectory, buildWorkingDirectory, testAssembly, testFramework, testSuffix));
                var taskSpawnAmd = Task.Run(() => SpawnNewTestRun(Settings.Default.UvTestRunnerUrlAmd, agentWorkingDirectory, buildWorkingDirectory, testAssembly, testFramework, testSuffix));

                Task.WaitAll(taskSpawnIntel, taskSpawnNvidia, taskSpawnAmd);

                Console.WriteLine($"Spawned #{taskSpawnIntel.Result} for Intel HD Graphics.");
                Console.WriteLine($"Spawned #{taskSpawnNvidia.Result} for NVIDIA.");
                Console.WriteLine($"Spawned #{taskSpawnAmd.Result} for AMD.");

                var taskIntel = Task.Run(() => WaitForTestRunToComplete(taskSpawnIntel.Result, "Intel", Settings.Default.UvTestRunnerUrlIntel,
                    agentWorkingDirectory, buildWorkingDirectory, inputNameIntel, outputNameIntel, testSuffix));
                var taskNvidia = Task.Run(() => WaitForTestRunToComplete(taskSpawnNvidia.Result, "Nvidia", Settings.Default.UvTestRunnerUrlNvidia,
                    agentWorkingDirectory, buildWorkingDirectory, inputNameNvidia, outputNameNvidia, testSuffix));
                var taskAmd = Task.Run(() => WaitForTestRunToComplete(taskSpawnAmd.Result, "Amd", Settings.Default.UvTestRunnerUrlAmd,
                    agentWorkingDirectory, buildWorkingDirectory, inputNameAmd, outputNameAmd, testSuffix));

                Task.WaitAll(taskIntel, taskNvidia, taskAmd);

                return true;
            });
            Console.WriteLine(succeeded.Result);
        }

        /// <summary>
        /// Terminates the program and displays the specified error message.
        /// </summary>
        /// <param name="exitCode">The application's exit code.</param>
        /// <param name="message">The error message to display to the console.</param>
        private static void ExitWithError(Int32 exitCode, String message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);

            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Waits for the specified test run to complete, then copies the results to the configured output directory.
        /// </summary>
        /// <param name="id">The identifier of the test run for which to retrieve a result.</param>
        /// <param name="vendor">The name of the GPU vendor for which to run rendering tests.</param>
        /// <param name="testRunnerUrl">The URL of the test runner server.</param>
        /// <param name="agentWorkingDirectory">The working directory for the current build agent.</param>
        /// <param name="buildWorkingDirectory">The working directory for the current build.</param>
        /// <param name="inputName">The name of the input result file.</param>
        /// <param name="outputName">The name to give to the result file.</param>
        /// <param name="suffixes">A semicolon-delimited list of optional suffixes to append to the test results.</param>
        /// <returns>The list of paths to the output result files.</returns>
        private static async Task<List<String>> WaitForTestRunToComplete(Int64? id, 
            String vendor, String testRunnerUrl, String agentWorkingDirectory, String buildWorkingDirectory, String inputName, String outputName, String suffixes)
        {
            if (id == null)
                return null;

            var idValue = id.Value;

            try
            {
                // Poll until the test run is complete.
                var status = TestRunStatus.Pending;
                while (status != TestRunStatus.Succeeded && status != TestRunStatus.Failed)
                {
                    await Task.Delay(1000);
                    status = await QueryTestRunStatus(testRunnerUrl, idValue);
                }

                // Spit out the result files.
                var resultPaths = new List<String>();
                var splitSuffixes = (suffixes ?? String.Empty).Split(';');
                for (int i = 0; i < splitSuffixes.Length; i++)
                {
                    var suffix = splitSuffixes[i];

                    var outputNameExtension = Path.GetExtension(outputName);
                    var outputNameWithoutExtension = Path.GetFileNameWithoutExtension(outputName);
                    outputName = $"{outputNameWithoutExtension}{suffix}{outputNameExtension}";

                    var resultData = await RetrieveTestResult(vendor, agentWorkingDirectory, buildWorkingDirectory, inputName, idValue, suffix);
                    var resultPath = Path.Combine(buildWorkingDirectory, outputName);
                    File.WriteAllBytes(resultPath, resultData);
                }
                return resultPaths;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// Posts a request to the server to spawn a new test run.
        /// </summary>
        /// <param name="testRunnerUrl">The URL of the test runner server.</param>
        /// <param name="agentWorkingDirectory">The working directory for the current build agent.</param>
        /// <param name="buildWorkingDirectory">The working directory for the current build.</param>
        /// <param name="testAssembly">The name of the assembly which contains the tests.</param>
        /// <param name="testFramework">The name of the test framework with which to run the tests.</param>
        /// <param name="testSuffix">An optional suffix to append to the test output.</param>
        /// <returns>The identifier of the test run within the server's database.</returns>
        private static async Task<Int64?> SpawnNewTestRun(String testRunnerUrl, String agentWorkingDirectory, String buildWorkingDirectory, String testAssembly, String testFramework, String testSuffix)
        {
            if (String.IsNullOrEmpty(testRunnerUrl))
                return null;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(testRunnerUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                var dirRelative =
                    new Uri(AddTrailingSlashToPath(agentWorkingDirectory)).MakeRelativeUri(
                    new Uri(AddTrailingSlashToPath(buildWorkingDirectory)));
                
                var response = await client.PostAsync("Api/UvTest", new StringContent($"\"{testAssembly},{dirRelative},{testFramework},{testSuffix}\"", Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to POST to test server at {0}: {1} {2}.", testRunnerUrl, (Int32)response.StatusCode, response.ReasonPhrase);
                    Environment.Exit(1);
                }

                var responseObject = await response.Content.ReadAsAsync<TestRunCreationResponse>();

                return responseObject.TestRunID;
            }
        }

        /// <summary>
        /// Retrieves the status of the specified test run from the server.
        /// </summary>
        /// <param name="testRunnerUrl">The URL of the test runner server.</param>
        /// <param name="id">The identifier of the test run within the server's database.</param>
        /// <returns>The current status of the specified test run.</returns>
        private static async Task<TestRunStatus> QueryTestRunStatus(String testRunnerUrl, Int64 id)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(testRunnerUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync("Api/UvTest/" + id.ToString());
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to GET from test server at {0}: {1} {2}.", testRunnerUrl, (Int32)response.StatusCode, response.ReasonPhrase);
                    Environment.Exit(1);
                }

                var responseObject = await response.Content.ReadAsAsync<TestRunStatusResponse>();
                return responseObject.TestRunStatus;
            }
        }

        /// <summary>
        /// Retrieves the test result file associated with the specified test run.
        /// </summary>
        /// <param name="vendor">The vendor for which to retrieve test results.</param>
        /// <param name="agentWorkingDirectory">The working directory for the current build agent.</param>
        /// <param name="buildWorkingDirectory">The working directory for the current build.</param>
        /// <param name="inputName">The name of the input result file.</param>
        /// <param name="id">The identifier of the test run within the server's database.</param>
        /// <param name="suffix">A suffix to append to the test results.</param>
        /// <returns>The contents of the test result file associated with the specified test run.</returns>
        private static async Task<Byte[]> RetrieveTestResult(String vendor, String agentWorkingDirectory, String buildWorkingDirectory, String inputName, Int64 id, String suffix)
        {
            Console.WriteLine("Retreiving test result for {0}...", vendor);

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(15);
                client.BaseAddress = new Uri(Settings.Default.UvTestViewerUrl);

                var dirRelative =
                    new Uri(AddTrailingSlashToPath(agentWorkingDirectory)).MakeRelativeUri(
                    new Uri(AddTrailingSlashToPath(buildWorkingDirectory)));

                var inputNameExtension = Path.GetExtension(inputName);
                var inputNameWithoutExtension = Path.GetFileNameWithoutExtension(inputName);
                inputName = $"{inputNameWithoutExtension}{suffix}{inputNameExtension}";

                var request = Path.Combine("TestResults", vendor, dirRelative.ToString(), id.ToString(), inputName).Replace('\\', '/');
                var response = await client.GetAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to retrieve {0} test results ({1}): {2} {3}.", vendor, inputName, (Int32)response.StatusCode, response.ReasonPhrase);

                    var error = await response.Content.ReadAsStringAsync();
                    File.WriteAllText(String.Format("Error_{0}_{1:yyyy_MM_dd_HH_mm_ss}.html", vendor, DateTime.Now), error);

                    Environment.Exit(1);
                }

                var data = await response.Content.ReadAsByteArrayAsync();
                Console.WriteLine("Received {0} bytes from {1} ({2})", data.Length, vendor, inputName);

                return data;
            }
        }

        /// <summary>
        /// Adds a trailing slash to the specified path, if it doesn't already have one.
        /// </summary>
        /// <param name="path">The path to which a trailing slash will be added.</param>
        /// <returns>The specified path with a trailing slash added, if it didn't already have one.</returns>
        private static String AddTrailingSlashToPath(String path)
        {
            var separator1 = Path.DirectorySeparatorChar.ToString();
            var separator2 = Path.AltDirectorySeparatorChar.ToString();

            if (path.EndsWith(separator1) || path.EndsWith(separator2))
                return path;

            if (path.Contains(separator2))
                return path + separator2;

            return path + separator1;
        }
    }
}
