using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Xml.Serialization;
using UvTestViewer.Models;

namespace UvTestViewer.Services
{
    /// <summary>
    /// Represents a service which retrieves and processes rendering test data.
    /// </summary>
    public class RenderingTestService
    {
        /// <summary>
        /// Gets an overview of the most recent rendering test run.
        /// </summary>
        /// <param name="vendor">The vendor for which to retrieve a rendering test run.</param>
        /// <param name="planKey">The Bamboo plan key for which to retrieve results.</param>
        /// <param name="branchKey">The Bamboo branch key for which to retrieve results.</param>
        /// <param name="page">The index of the selected page of results.</param>
        /// <param name="pageSize">The number of test results on each page of the overview.</param>
        /// <returns>A <see cref="RenderingTestOverview"/> instance which represents the msot recent test run.</returns>
        public RenderingTestOverview GetMostRecentRenderingTestOverview(GpuVendor vendor, String planKey, String branchKey, Int32 page, Int32 pageSize)
        {
            var id        = 0L;
            var directory = GetMostRecentTestResultsDirectory(vendor, planKey, branchKey, out id);
            if (directory == null)
                return null;

            var cachedTestInfos = RetrieveCachedTestInfo(directory);

            var images       = directory.GetFiles("*.png");
            var imagesByTest = from file in images
                               let filename = Path.GetFileName(file.FullName)
                               let testname = GetTestNameFromImageFileName(file.FullName)
                               where
                                !String.IsNullOrEmpty(testname)
                               group filename by testname into g
                               select g;

            var workingDirectoryPattern = ConfigurationManager.AppSettings["BambooWorkingDirectoryPattern"];
            var workingDirectory = String.Format(workingDirectoryPattern, branchKey ?? planKey);

            var outputDir = VirtualPathUtility.ToAbsolute(String.Format("~/TestResults/{0}/{1}/{2}", vendor, workingDirectory, id));
            
            var tests = new List<RenderingTest>();
            foreach (var cachedTestInfo in cachedTestInfos)
            {
                var imageName = cachedTestInfo.Name.Split(' ').First();

                var testExpected = String.Format("{0}_Expected.png", imageName);
                var testActual = String.Format("{0}_Actual.png", imageName);
                var testDiff =  String.Format("{0}_Diff.png", imageName);

                var test = new RenderingTest(cachedTestInfo.Name, cachedTestInfo.Description,
                    GetRelativeUrlOfImage(outputDir, testExpected),
                    GetRelativeUrlOfImage(outputDir, testActual),
                    GetRelativeUrlOfImage(outputDir, testDiff));

                test.Failed = (cachedTestInfo.Status == CachedTestInfoStatus.Failed);

                tests.Add(test);
            }

            var resultTests = tests.OrderBy(test => test.Name).Select((test, index) => new { Page = index / pageSize, Test = test }).GroupBy(item => item.Page)
                .Select(group => group.Select(item => item.Test)).ToList();
            var resultPages = resultTests.Select(x => new RenderingTestPage() { Failed = x.Any(y => y.Failed) });

            if (page < 0 || page >= resultTests.Count)
                page = 0;
            
            return new RenderingTestOverview()
            {
                TestRunID = id,
                PassedTestCount = tests.Where(x => !x.Failed).Count(),
                FailedTestCount = tests.Where(x => x.Failed).Count(),
                SelectedPage = page,
                Tests = resultTests.Any() ? resultTests[page].ToList() : new List<RenderingTest>(),
                Pages = resultPages.ToList(),
                TimeProcessed = directory.CreationTime,
                Vendor = vendor
            };
        }

        /// <summary>
        /// Gets the test run identifier associated with the specified directory.
        /// </summary>
        /// <param name="name">The name of the directory to evaluate.</param>
        /// <returns>The test run identifier associated with the specified directory, or <c>null</c> if the
        /// specified directory is not associated with a test run.</returns>
        private static Int64? GetDirectoryID(String name)
        {
            Int64 id;

            if (!Int64.TryParse(name, out id))
                return null;

            return id;
        }

        /// <summary>
        /// Gets a <see cref="DirectoryInfo"/> which represents the most recent rendering test run.
        /// </summary>
        /// <param name="vendor">The vendor for which to retrieve a rendering test run.</param>
        /// <param name="planKey">The Bamboo plan key for which to retrieve results.</param>
        /// <param name="branchKey">The Bamboo branch key for which to retrieve results.</param>
        /// <param name="id">The identifier of the test run associated with the retrieved directory.</param>
        /// <returns>A <see cref="DirectoryInfo"/> which represents the most recent rendering test run.</returns>
        private static DirectoryInfo GetMostRecentTestResultsDirectory(GpuVendor vendor, String planKey, String branchKey, out Int64 id)
        {
            var root = ConfigurationManager.AppSettings["TestResultRootDirectory"];
            var rootMapped = Path.IsPathRooted(root) ? root : HttpContext.Current.Server.MapPath(root);

            switch (vendor)
            {
                case GpuVendor.Intel:
                    rootMapped = Path.Combine(rootMapped, "Intel"); 
                    break;
                
                case GpuVendor.Nvidia:
                    rootMapped = Path.Combine(rootMapped, "Nvidia"); 
                    break;
                
                case GpuVendor.Amd:
                    rootMapped = Path.Combine(rootMapped, "Amd"); 
                    break;

                default: 
                    throw new ArgumentException("Unrecognized GPU hardware vendor.");
            }

            var workingDirectoryPatterns = ConfigurationManager.AppSettings["BambooWorkingDirectoryPattern"].Split(';');
            foreach (var workingDirectoryPattern in workingDirectoryPatterns)
            {
                var workingDirectory = String.Format(workingDirectoryPattern, branchKey ?? planKey);
                var workingDirectoryWithRoot = Path.Combine(rootMapped, workingDirectory);

                if (!Directory.Exists(workingDirectoryWithRoot))
                {
                    id = 0;
                    continue;
                }

                var rootSubdirs = Directory.GetDirectories(workingDirectoryWithRoot);
                var rootSubdirsByID = from subdir in rootSubdirs
                                      let dirInfo = new DirectoryInfo(subdir)
                                      let dirID = GetDirectoryID(dirInfo.Name)
                                      where dirID.HasValue
                                      orderby dirInfo.CreationTime descending
                                      select new { ID = dirID, DirectoryInfo = dirInfo };

                var directory = rootSubdirsByID.FirstOrDefault();
                if (directory == null)
                {
                    id = 0;
                    continue;
                }

                id = directory.ID.Value;
                return directory.DirectoryInfo;
            }

            id = 0;
            return null;
        }

        /// <summary>
        /// Gets the relative URL used to display the specified unit test image.
        /// </summary>
        /// <param name="rootdir">The root directory of unit test images for the current run.</param>
        /// <param name="image">The path to the image for which to retrieve a URL.</param>
        /// <returns>The relative URL used to display the specified unit test image.</returns>
        private static String GetRelativeUrlOfImage(String rootdir, String image)
        {
            return String.IsNullOrEmpty(image) ? null : String.Format("{0}/{1}", rootdir, image);
        }

        /// <summary>
        /// Gets the name of the test associated with the specified output image.
        /// </summary>
        /// <param name="filename">The filename of the output image to evaluate.</param>
        /// <returns>The name of the test associated with the specified output image.</returns>
        private static String GetTestNameFromImageFileName(String filename)
        {
            var filenameNoExt = Path.GetFileNameWithoutExtension(filename);

            if (filenameNoExt.EndsWith("_Actual"))
                return filenameNoExt.Substring(0, filenameNoExt.Length - "_Actual".Length);

            if (filenameNoExt.EndsWith("_Expected"))
                return filenameNoExt.Substring(0, filenameNoExt.Length - "_Expected".Length);

            if (filenameNoExt.EndsWith("_Diff"))
                return filenameNoExt.Substring(0, filenameNoExt.Length - "_Diff".Length);

            return null;
        }
        
        /// <summary>
        /// Retrieves the collection of <see cref="CachedTestInfo"/> objects which represent the tests in the specified directory.
        /// </summary>
        private static IEnumerable<CachedTestInfo> RetrieveCachedTestInfo(DirectoryInfo dir)
        {
            var serializer = new XmlSerializer(typeof(List<CachedTestInfo>));

            var cacheFilename = Path.Combine(dir.FullName, "ResultCache.xml");
            try
            {
                if (File.Exists(cacheFilename))
                {
                    using (var stream = File.OpenRead(cacheFilename))
                        return (List<CachedTestInfo>)serializer.Deserialize(stream);
                }
            }
            catch (IOException) { }

            var testFramework = (ConfigurationManager.AppSettings["TestFramework"] ?? "mstest").ToLowerInvariant();
            switch (testFramework)
            {
                case "nunit3":
                    return RetrieveCachedTestInfo_NUnit3(dir, cacheFilename, serializer);

                case "mstest":
                    return RetrieveCachedTestInfo_MSTest(dir, cacheFilename, serializer);

                default:
                    throw new NotSupportedException("Unsupported test framework");
            }            
        }

        /// <summary>
        /// Retrieves cached test info for the NUnit3 framework.
        /// </summary>
        private static IEnumerable<CachedTestInfo> RetrieveCachedTestInfo_NUnit3(DirectoryInfo dir, String cacheFilename, XmlSerializer serializer)
        {
            try
            {
                var testResultFilename = dir.GetFiles("*.xml").SingleOrDefault()?.FullName;
                var testResultXml = XDocument.Load(testResultFilename);
                var testResultNamespace = testResultXml.Root.GetDefaultNamespace();

                var unitTests = testResultXml.Root.Descendants(testResultNamespace + "test-case")
                    .Where(x => x.Descendants(testResultNamespace + "category").Any(y => (String)y.Attribute("name") == "Rendering"));

                var failedTestNames = new HashSet<String>
                    (from r in unitTests
                     let testName = (String)r.Attribute("name")
                     let testResult = (String)r.Attribute("result")
                     where
                         testResult == "Failed"
                     select testName);

                var cachedTestInfos =
                    (from node in unitTests
                     let name = (String)node.Attribute("name")
                     let desc = (String)node.Descendants(testResultNamespace + "property").Where(x => (String)x.Attribute("name") == "Description").SingleOrDefault()?.Attribute("value")
                     let status = failedTestNames.Contains(name) ? CachedTestInfoStatus.Failed : CachedTestInfoStatus.Succeeded
                     select new CachedTestInfo
                     {
                         Name = GetNUnitTestName(name),
                         Description = desc,
                         Status = status,
                     }).ToList();

                using (var stream = File.OpenWrite(cacheFilename))
                {
                    serializer.Serialize(stream, cachedTestInfos);
                }

                return cachedTestInfos;
            }
            catch (IOException)
            {
                return Enumerable.Empty<CachedTestInfo>();
            }
        }

        /// <summary>
        /// Retrieves cached test info for the MSTest framework.
        /// </summary>
        private static IEnumerable<CachedTestInfo> RetrieveCachedTestInfo_MSTest(DirectoryInfo dir, String cacheFilename, XmlSerializer serializer)
        {
            try
            {
                var testResultFilename = Path.Combine(dir.FullName, "Result.trx");
                var testResultXml = XDocument.Load(testResultFilename);
                var testResultNamespace = testResultXml.Root.GetDefaultNamespace();

                var unitTests = testResultXml.Root.Descendants(testResultNamespace + "UnitTest")
                    .Where(x => x.Descendants(testResultNamespace + "TestCategoryItem").Any(y => (String)y.Attribute("TestCategory") == "Rendering"));
                var unitTestResults = testResultXml.Root.Descendants(testResultNamespace + "UnitTestResult");

                var failedTestNames = new HashSet<String>
                    (from r in unitTestResults
                     let testName = (String)r.Attribute("testName")
                     let outcome = (String)r.Attribute("outcome")
                     where
                         outcome == "Failed"
                     select testName);

                var cachedTestInfos =
                    (from node in unitTests
                     let name = (String)node.Attribute("name")
                     let desc = (String)node.Element(testResultNamespace + "Description")
                     let status = failedTestNames.Contains(name) ? CachedTestInfoStatus.Failed : CachedTestInfoStatus.Succeeded
                     select new CachedTestInfo
                     {
                         Name = name,
                         Description = desc,
                         Status = status,
                     }).ToList();

                using (var stream = File.OpenWrite(cacheFilename))
                {
                    serializer.Serialize(stream, cachedTestInfos);
                }

                return cachedTestInfos;
            }
            catch (IOException)
            {
                return Enumerable.Empty<CachedTestInfo>();
            }
        }

        /// <summary>
        /// Gets the simple name of an NUnit test.
        /// </summary>
        private static String GetNUnitTestName(String name)
        {
            var ix = name.LastIndexOf('.');
            if (ix < 0)
                return name;

            return name.Substring(ix + 1);
        }
    }
}
