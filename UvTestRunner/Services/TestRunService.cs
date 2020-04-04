using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UvTestRunner.Data;
using UvTestRunner.Models;

namespace UvTestRunner.Services
{
    /// <summary>
    /// Represents a service which can run the test suite and retrieve data about
    /// previous test suite runs.
    /// </summary>
    public class TestRunService
    {
        /// <summary>
        /// Gets the test run with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the test run within the database.</param>
        /// <returns>The test run with the specified identifier, or <c>null</c> if no such test run exists.</returns>
        public TestRun GetByID(Int64 id)
        {
            using (var testRunContext = new TestRunContext())
            {
                return testRunContext.TestRuns.Where(x => x.ID == id).SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets the status of the most recent test run in the specified working directory.
        /// </summary>
        /// <param name="workingDirectory">The working directory of the test runs to evaluate.</param>
        /// <returns>A <see cref="TestRunStatus"/> value that specifies the status of the most recent test run in the specified working directory.</returns>
        public TestRunStatus GetMostRecentStatusByWorkingDirectory(String workingDirectory)
        {
            if (String.IsNullOrEmpty(workingDirectory))
                return TestRunStatus.Failed;

            using (var testRunContext = new TestRunContext())
            {
                return testRunContext.TestRuns.Where(x => x.WorkingDirectory == workingDirectory).OrderByDescending(x => x.ID).Take(1).Select(x => x.Status).SingleOrDefault();
            }
        }

        /// <summary>
        /// Gets a value which represents the overall status of the most recent test runs in all of the specified working directories.
        /// </summary>
        /// <param name="workingDirectories">A collection of working directories representing the test runs to evaluate.</param>
        /// <returns>A <see cref="TestRunStatus"/> value that specifies the overall status of the most recent test runs in the specified working directories.</returns>
        public TestRunStatus GetMostRecentStatusByWorkingDirectories(IEnumerable<String> workingDirectories)
        {
            if (workingDirectories == null)
                return TestRunStatus.Failed;

            using (var testRunContext = new TestRunContext())
            {
                var statuses = testRunContext.TestRuns.GroupBy(x => x.WorkingDirectory).Where(x => workingDirectories.Contains(x.Key))
                    .Select(x => x.OrderByDescending(y => y.ID).Select(y => y.Status).FirstOrDefault()).ToList();

                if (statuses.Any(x => x == TestRunStatus.Failed))
                    return TestRunStatus.Failed;

                if (statuses.Any(x => x == TestRunStatus.Running))
                    return TestRunStatus.Running;

                if (statuses.Any(x => x == TestRunStatus.Pending))
                    return TestRunStatus.Pending;

                return TestRunStatus.Succeeded;
            }
        }

        /// <summary>
        /// Executes the specified test run.
        /// </summary>
        /// <param name="testRun">The test run to execute.</param>
        /// <returns>The identifier of the test run within the database.</returns>
        public Int64 Run(TestRun testRun)
        {
            if (testRun == null)
                throw new ArgumentNullException("testRun");

            var id = testRun.ID;

            var testAssemblies = testRun.TestAssembly.Split(';');
            var testSuffixes = (testRun.Suffix ?? String.Empty).Split(';');
            if (testSuffixes.Length != testAssemblies.Length)
            {
                UpdateTestRunStatus(id, TestRunStatus.Failed);
                ProgramUI.QueueMessage("ERROR: Assembly/suffix mismatch.");
                return id;
            }

            UpdateTestRunStatus(id, TestRunStatus.Running);
            for (int i = 0; i < testAssemblies.Length; i++)
            {
                var workingDirectory = testRun.WorkingDirectory;
                var testAssembly = testAssemblies[i];
                var testFramework = (testRun.TestFramework ?? Settings.Default.TestFramework ?? "mstest").ToLowerInvariant();
                var testSuffix = testSuffixes[i];

                // Start by spawning the test runner process and running the unit test suite.
                var proc = default(Process);
                var previousWorkingDirectory = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = Path.Combine(Settings.Default.TestRootDirectory, workingDirectory).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                    switch (testFramework)
                    {
                        case "mstest":
                        case "nunit3":
                            RunTests_Legacy(testAssembly, out proc);
                            break;

                        case "nunit3core":
                            RunTests_NUnit3Core(testAssembly, testSuffix, out proc);
                            break;
                    }
                }
                catch (IOException e)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    ProgramUI.QueueMessage("ERROR: Unable to spawn unit test process!");
                    ProgramUI.QueueMessage("ERROR: " + e.Message + Environment.NewLine);
                    return id;
                }
                finally
                {
                    Environment.CurrentDirectory = previousWorkingDirectory;
                }

                // If the test runner exited with an error, log it to the database and bail out.
                var testFrameworkFailed = (testFramework == "mstest" || testFramework == "nunit3core") ?
                    (proc.ExitCode != 0 && proc.ExitCode != 1) : (proc.ExitCode < 0);

                if (testFrameworkFailed)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    return id;
                }

                // Determine the location of the test result file...
                var testResultsRoot = Path.Combine(Settings.Default.TestRootDirectory, workingDirectory, Settings.Default.TestOutputDirectory);
                var testResultPath = String.Empty;
                var testResultImagesPath = String.Empty;

                /* If the tests ran successfully, find the folder that contains the test results.
                 * TODO: The way we do this currently introduces a race condition if the test suite is being run simultaneously
                 * in multiple threads, which shouldn't realistically happen, but this case probably
                 * ought to be handled anyway for robustness. */
                try
                {
                    switch (testFramework)
                    {
                        case "mstest":
                            if (!GetTestResults_MSTest(id, testResultsRoot, out testResultPath, out testResultImagesPath))
                                return id;
                            break;

                        case "nunit3":
                            if (!GetTestResults_NUnit3(id, testResultsRoot, out testResultPath, out testResultImagesPath))
                                return id;
                            break;

                        case "nunit3core":
                            if (!GetTestResults_NUnit3Core(id, testResultsRoot, testSuffix, out testResultPath, out testResultImagesPath))
                                return id;
                            break;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    ProgramUI.QueueMessage("ERROR: Unable to retrieve test artifacts. Directory not found.");
                    return id;
                }

                // Optionally rewrite test names.
                try
                {
                    if (!String.IsNullOrEmpty(Settings.Default.TestNameRewriteRule))
                    {
                        switch (testFramework)
                        {
                            case "mstest":
                                RewriteTestNames_MSTest(testResultPath);
                                break;

                            case "nunit3":
                            case "nunit3core":
                                RewriteTestNames_NUnit3(testResultPath);
                                break;
                        }
                    }
                }
                catch (IOException e)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    ProgramUI.QueueMessage("ERROR: Failed to rewrite test names. " + e.Message);
                    return id;
                }

                // Create a directory to hold this test's artifacts.
                try
                {
                    var outputDirectory = Path.Combine(Settings.Default.TestResultDirectory, workingDirectory, id.ToString());
                    Directory.CreateDirectory(outputDirectory);

                    // Move the result file and any outputted PNG files to the artifact directory.
                    var resultStatus = GetStatusFromTestResult(testResultPath);
                    var resultFileSrc = testResultPath;
                    var resultFileDst = Path.Combine(outputDirectory, Path.GetFileName(testResultPath));
                    CopyFile(resultFileSrc, resultFileDst, false);

                    var pngFiles = Directory.GetFiles(testResultImagesPath, "*.png");
                    foreach (var pngFile in pngFiles)
                    {
                        var pngFileSrc = pngFile;
                        var pngFileDst = Path.Combine(outputDirectory, Path.GetFileName(pngFileSrc));
                        CopyFile(pngFileSrc, pngFileDst, true);
                    }

                    // Update test status if the test failed.
                    if (resultStatus != TestRunStatus.Succeeded)
                    {
                        UpdateTestRunStatus(id, resultStatus);
                        return id;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    ProgramUI.QueueMessage("ERROR: Unable to store test artifacts. Directory not found.");
                    return id;
                }
            }

            UpdateTestRunStatus(id, TestRunStatus.Succeeded);
            return id;
        }

        /// <summary>
        /// Creates a new test run and places it into pending status.
        /// </summary>
        /// <param name="workingDirectory">The current working directory for the build agent.</param>
        /// <param name="testAssembly">The name of the assembly that contains the tests.</param>
        /// <param name="testFramework">The name of the test framework with which to execute the tests.</param>
        /// <param name="suffix">An optional suffix to use when outputting test results.</param>
        /// <returns>The identifier of the test run within the database.</returns>
        public Int64 CreateTestRun(String workingDirectory, String testAssembly, String testFramework, String suffix)
        {
            using (var testRunContext = new TestRunContext())
            {
                var run = new TestRun() { Status = TestRunStatus.Pending, WorkingDirectory = workingDirectory, TestAssembly = testAssembly, TestFramework = testFramework, Suffix = suffix };

                testRunContext.TestRuns.Add(run);
                testRunContext.SaveChanges();

                return run.ID;
            }
        }

        /// <summary>
        /// Gets the status of the specified test run.
        /// </summary>
        /// <param name="id">The identifier of the test run within the database.</param>
        /// <returns>A <see cref="TestRunStatus"/> value specifying the test run's current status.</returns>
        public TestRunStatus GetTestRunStatus(Int64 id)
        {
            using (var testRunContext = new TestRunContext())
            {
                var run = testRunContext.TestRuns.Where(x => x.ID == id).Single();
                return run.Status;
            }
        }

        /// <summary>
        /// Updates the status of the specified test run.
        /// </summary>
        /// <param name="id">The identifier of the test run within the database.</param>
        /// <param name="status">The status to set for the specified test run.</param>
        /// <returns>A <see cref="TestRunStatus"/> value specifying the test run's previous status.</returns>
        public TestRunStatus UpdateTestRunStatus(Int64 id, TestRunStatus status)
        {
            using (var testRunContext = new TestRunContext())
            {
                var run = testRunContext.TestRuns.Where(x => x.ID == id).Single();
                var previousStatus = run.Status;
                run.Status = status;
                testRunContext.SaveChanges();

                return previousStatus;
            }
        }
        
        /// <summary>
        /// Copies a file and does not return until copying is complete.
        /// </summary>
        /// <param name="src">The source file.</param>
        /// <param name="dst">The destination file.</param>
        /// <param name="delete">A value indicating whether to delete the source file after copying.</param>
        private void CopyFile(String src, String dst, Boolean delete)
        {
            using (var srcStream = File.Open(src, FileMode.Open))
            {
                using (var dstStream = File.Create(dst))
                {
                    srcStream.CopyTo(dstStream);
                }
            }

            if (delete)
                File.Delete(src);
        }

        /// <summary>
        /// Gets the test run status from the specified test result file.
        /// </summary>
        /// <param name="file">The path to the test result file to read.</param>
        /// <returns>A <see cref="TestRunStatus"/> value that represents the status of the specified test.</returns>
        private TestRunStatus GetStatusFromTestResult(String file)
        {
            var testResultXml = XDocument.Load(file);
            var testResultNamespace = testResultXml.Root.GetDefaultNamespace();

            var testResults = testResultXml.Descendants(testResultNamespace + "UnitTestResult");
            var testOutcomes = testResults.Select(x => (String)x.Attribute("outcome"));
            if (testOutcomes.Where(x => String.Equals("Failed", x)).Any())
            {
                return TestRunStatus.Failed;
            }

            return TestRunStatus.Succeeded;
        }
        
        /// <summary>
        /// Gets the current machine's name, with any path-invalid characters removed.
        /// </summary>
        private String GetSanitizedMachineName()
        {
            var invalid = Path.GetInvalidPathChars();
            var name = new StringBuilder(Environment.MachineName);
            for (int i = 0; i < name.Length; i++)
            {
                if (invalid.Contains(name[i]))
                {
                    name[i] = '_';
                }
            }
            return name.ToString();
        }

        /// <summary>
        /// Retrieves the results of the specified test run.
        /// </summary>
        private Boolean GetTestResults_MSTest(Int64 id, String testResultsRoot, out String testResultPath, out String testResultImagesPath)
        {
            var testResultsDirs = Directory.GetDirectories(testResultsRoot)
                .Where(x => x.Contains("_" + Environment.MachineName.ToUpper() + " "))
                .Select(x => new DirectoryInfo(x));

            var relevantTestResult = testResultsDirs.OrderByDescending(x => x.CreationTimeUtc).FirstOrDefault();

            if (relevantTestResult == null)
            {
                UpdateTestRunStatus(id, TestRunStatus.Failed);
                testResultPath = null;
                testResultImagesPath = null;
                return false;
            }

            testResultPath = Path.ChangeExtension(Path.Combine(relevantTestResult.Parent.FullName, relevantTestResult.Name), "trx");
            testResultImagesPath = Path.Combine(relevantTestResult.FullName, "Out");
            return true;
        }

        /// <summary>
        /// Retrieves the results of the specified test run.
        /// </summary>
        private Boolean GetTestResults_NUnit3(Int64 id, String testResultsRoot, out String testResultPath, out String testResultImagesPath)
        {
            testResultPath = Path.Combine(testResultsRoot, Settings.Default.TestResultFile);
            testResultImagesPath = Path.Combine(testResultsRoot, GetSanitizedMachineName());

            return true;
        }

        /// <summary>
        /// Retrieves the results of the specified test run.
        /// </summary>
        private Boolean GetTestResults_NUnit3Core(Int64 id, String testResultsRoot, String testSuffix, out String testResultPath, out String testResultImagesPath)
        {
            testResultPath = Path.Combine(testResultsRoot, String.Format(Settings.Default.NetCoreTestResultFile, testSuffix));
            testResultImagesPath = Path.Combine(testResultsRoot, GetSanitizedMachineName());

            return true;
        }

        /// <summary>
        /// Rewrites the names found in the specified results file based on the current rewrite rule.
        /// </summary>
        private void RewriteTestNames_MSTest(String path)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Rewrites the names found in the specified results file based on the current rewrite rule.
        /// </summary>
        private void RewriteTestNames_NUnit3(String path)
        {
            var testResultXml = XDocument.Load(path);
            var testResultNamespace = testResultXml.Root.GetDefaultNamespace();

            var tests = testResultXml.Root.Descendants(testResultNamespace + "test-case");

            foreach (var test in tests)
            {
                var name = (String)test.Attribute("name");
                name = String.Format(Settings.Default.TestNameRewriteRule, name);
                test.SetAttributeValue("name", name);
            }

            testResultXml.Save(path);
        }

        /// <summary>
        /// Runs tests using a legacy framework.
        /// </summary>
        private void RunTests_Legacy(String testAssembly, out Process proc)
        {
            var psi = new ProcessStartInfo(Settings.Default.TestHostExecutable, String.Format(Settings.Default.TestHostArgs, testAssembly ?? "Ultraviolet.Tests.dll"))
            {
                WorkingDirectory = Environment.CurrentDirectory
            };
            proc = Process.Start(psi);
            proc.PriorityClass = ProcessPriorityClass.High;
            proc.WaitForExit();
        }

        /// <summary>
        /// Runs NUnit3 runs for a .NET Core project.
        /// </summary>
        private void RunTests_NUnit3Core(String testAssembly, String testSuffix, out Process proc)
        {
            var psi = new ProcessStartInfo(Settings.Default.NetCoreHostExecutable, $"test {String.Format(Settings.Default.NetCoreHostArgs, testAssembly ?? "Ultraviolet.Tests.dll", testSuffix)}")
            {
                WorkingDirectory = Environment.CurrentDirectory
            };
            proc = Process.Start(psi);
            proc.PriorityClass = ProcessPriorityClass.High;
            proc.WaitForExit();
        }
    }
}
