using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            var workingDirectory = testRun.WorkingDirectory;

            // Start by spawning the MSTest process and running the unit test suite.
            UpdateTestRunStatus(id, TestRunStatus.Running);
            var psi = new ProcessStartInfo(Settings.Default.TestHostExecutable, Settings.Default.TestHostArgs)
            {
                WorkingDirectory = Path.Combine(Settings.Default.TestRootDirectory, workingDirectory).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            };
            var proc = Process.Start(psi);
            proc.WaitForExit();

            // If MSTest exited with an error, log it to the database and bail out.
            if (proc.ExitCode != 0 && proc.ExitCode != 1)
            {
                UpdateTestRunStatus(id, TestRunStatus.Failed);
                return id;
            }

            /* If the tests ran successfully, find the folder that contains the test results.
             * TODO: The way we do this currently introduces a race condition if the test suite is being run simultaneously
             * in multiple threads, which shouldn't realistically happen, but this case probably
             * ought to be handled anyway for robustness. */
            DirectoryInfo relevantTestResult;
            try
            {
                var testResultsRoot = Path.Combine(Settings.Default.TestRootDirectory, workingDirectory, "TestResults");
                var testResultsDirs = Directory.GetDirectories(testResultsRoot)
                    .Where(x => x.Contains("_" + Environment.MachineName.ToUpper() + " "))
                    .Select(x => new DirectoryInfo(x));

                relevantTestResult = testResultsDirs.OrderByDescending(x => x.CreationTimeUtc).FirstOrDefault();

                if (relevantTestResult == null)
                {
                    UpdateTestRunStatus(id, TestRunStatus.Failed);
                    return id;
                }
            }
            catch (DirectoryNotFoundException)
            {
                UpdateTestRunStatus(id, TestRunStatus.Failed);
                return id;
            }

            // Create a directory to hold this test's artifacts.
            var outputDirectory = Path.Combine(Settings.Default.TestResultDirectory, workingDirectory, id.ToString());
            Directory.CreateDirectory(outputDirectory);

            // Copy the TRX file and any outputted PNG files to the artifact directory.
            var trxFileSrc = Path.ChangeExtension(Path.Combine(relevantTestResult.Parent.FullName, relevantTestResult.Name), "trx");
            var trxFileDst = Path.Combine(outputDirectory, "Result.trx");
            CopyFile(trxFileSrc, trxFileDst);

            var pngFiles = Directory.GetFiles(Path.Combine(relevantTestResult.FullName, "Out"), "*.png");
            foreach (var pngFile in pngFiles)
            {
                var pngFileSrc = pngFile;
                var pngFileDst = Path.Combine(outputDirectory, Path.GetFileName(pngFileSrc));
                CopyFile(pngFileSrc, pngFileDst);
            }

            var resultStatus = GetStatusFromTestResult(trxFileSrc);
            UpdateTestRunStatus(id, resultStatus);

            return id;
        }

        /// <summary>
        /// Creates a new test run and places it into pending status.
        /// </summary>
        /// <param name="workingDirectory">The current working directory for the build agent.</param>
        /// <returns>The identifier of the test run within the database.</returns>
        public Int64 CreateTestRun(String workingDirectory)
        {
            using (var testRunContext = new TestRunContext())
            {
                var run = new TestRun() { Status = TestRunStatus.Pending, WorkingDirectory = workingDirectory };

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
        /// <returns>A <see cref="Task"/> which represents the copy operation.</returns>
        private void CopyFile(String src, String dst)
        {
            using (var srcStream = File.Open(src, FileMode.Open))
            {
                using (var dstStream = File.Create(dst))
                {
                    srcStream.CopyTo(dstStream);
                }
            }
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
    }
}
