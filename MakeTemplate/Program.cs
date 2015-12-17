using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MakeTemplate
{
    class Program
    {
        public static void Main(String[] args)
        {
            String version, srcDirectory, dstDirectory, outputFile;
            if (!RetrieveArguments(args, out version, out srcDirectory, out dstDirectory, out outputFile))
                return;

            // Copy the contents of the source directory to the destination directory.
            CopyDirectory(srcDirectory, dstDirectory);

            // Remove the bin/obj directories.
            DeleteDirectory(Path.Combine(dstDirectory, "bin"));
            DeleteDirectory(Path.Combine(dstDirectory, "obj"));

            // Scan the directory for C# files and perform template parameter replacements.
            ReplaceTemplateParametersInDirectory(dstDirectory);
            ReplaceProjectReferencesInDirectory(dstDirectory, version);

            // Zip everything up.
            CreateTemplateArchive(dstDirectory, outputFile);
        }

        /// <summary>
        /// Validates and retrieves the program's command line arguments.
        /// </summary>
        private static Boolean RetrieveArguments(String[] args, out String version, out String srcDirectory, out String dstDirectory, out String outputFile)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Converts Ultraviolet's project template projects into Visual Studio project template archives.");
                Console.WriteLine();
                Console.WriteLine(
                    "MAKETEMPLATE version srcDirectory dstDirectory output\n" +
                    "\n" +
                    "  version      The Ultraviolet Framework version number (ex. 'v1.3')\n" +
                    "  srcDirectory The directory that contains the project to convert.\n" +
                    "  dstDirectory The directory that will be used to build the archive.\n" +
                    "  outputFile   The name of the archive file which is created.");

                version = null;
                srcDirectory = null;
                dstDirectory = null;
                outputFile = null;

                return false;
            }

            version = args[0];
            srcDirectory = args[1];
            dstDirectory = args[2];
            outputFile = args[3];

            return true;
        }

        /// <summary>
        /// Recursively deletes the specified directory, if it exists.
        /// </summary>
        /// <param name="dir">The path to the directory to delete.</param>
        private static void DeleteDirectory(String dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (DirectoryNotFoundException) { }
        }

        /// <summary>
        /// Recursively copies one directory into another directory. If the destination directory already
        /// exists, it will be deleted before copying.
        /// </summary>
        private static void CopyDirectory(String srcDirectory, String dstDirectory)
        {
            var srcDirectoryInfo = new DirectoryInfo(srcDirectory);
            if (!srcDirectoryInfo.Exists)
                throw new DirectoryNotFoundException();

            DeleteDirectory(dstDirectory);
            Directory.CreateDirectory(dstDirectory);

            var files = srcDirectoryInfo.EnumerateFiles();
            foreach (var file in files)
            {
                var dst = Path.Combine(dstDirectory, file.Name);
                file.CopyTo(dst, true);
            }

            var subdirs = srcDirectoryInfo.GetDirectories();
            foreach (var subdir in subdirs)
            {
                var dst = Path.Combine(dstDirectory, subdir.Name);
                CopyDirectory(subdir.FullName, dst);
            }
        }

        /// <summary>
        /// Scans the specified directory for files which match the given filter and performs
        /// the given action against those files.
        /// </summary>
        private static void ScanDirectory(String dir, Predicate<FileInfo> filter, Action<FileInfo> action)
        {
            var dirInfo = new DirectoryInfo(dir);
            if (!dirInfo.Exists)
                throw new DirectoryNotFoundException();

            var files = dirInfo.EnumerateFiles();
            foreach (var file in files)
            {
                if (filter(file))
                {
                    action(file);
                }
            }
            
            var subdirs = dirInfo.GetDirectories();
            foreach (var subdir in subdirs)
            {
                ScanDirectory(subdir.FullName, filter, action);
            }
        }

        /// <summary>
        /// Replaces template parameters in all C# files in the specified directory and its subdirectories.
        /// </summary>
        /// <remarks>
        /// The replaced parameters are:
        ///     SAFE_PROJECT_NAME - becomes $safeprojectname$
        ///          PROJECT_NAME - becomes $projectname$
        /// </remarks>
        private static void ReplaceTemplateParametersInDirectory(String directory)
        {
            var supportedExtensions = new String[] { ".cs", ".csproj" };
            ScanDirectory(directory, fi => supportedExtensions.Contains(fi.Extension), fi =>
            {
                ReplaceTemplateParametersInFile(fi.FullName);
            });
        }

        /// <summary>
        /// Replaces template parameters in the specified file.
        /// </summary>
        private static void ReplaceTemplateParametersInFile(String file)
        {
            var regex = new Regex("SAFE_PROJECT_NAME|PROJECT_NAME", RegexOptions.Singleline);

            var replaced = false;
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = regex.Replace(lines[i], new MatchEvaluator(match =>
                {
                    replaced = true;
                    switch (match.Value.ToUpperInvariant())
                    {
                        case "SAFE_PROJECT_NAME":
                            return "$safeprojectname$";

                        case "PROJECT_NAME":
                            return "$projectname$";
                    }
                    return match.Value;
                }));
            }

            if (replaced)
                File.WriteAllLines(file, lines);
        }

        /// <summary>
        /// Replaces project references in any .csproj files in the specified directory with generic assembly references.
        /// </summary>
        private static void ReplaceProjectReferencesInDirectory(String directory, String version)
        {
            var files = Directory.EnumerateFiles(directory, "*.csproj");
            foreach (var file in files)
            {
                ReplaceProjectReferencesInFile(file, version);
            }
        }

        /// <summary>
        /// Replaces project references in the specified file with generic assembly references.
        /// </summary>
        private static void ReplaceProjectReferencesInFile(String file, String version)
        {
            var xml = XDocument.Load(file);
            var xmlNs = xml.Root.GetDefaultNamespace();

            var projectReferences = xml.Descendants().Where(
                x => x.Name == xmlNs + "ProjectReference").ToList();

            var platformSpecifiers = new String[] { " (Desktop)", " %28Desktop%29", " (Android)", " %28Android%29" };

            foreach (var projectReference in projectReferences)
            {
                var asmName = projectReference.Element(xml.Root.GetDefaultNamespace() + "Name").Value;

                foreach (var platformSpecifier in platformSpecifiers)
                {
                    if (asmName.EndsWith(platformSpecifier))
                        asmName = asmName.Substring(0, asmName.Length - platformSpecifier.Length);
                }

                var asmReference = new XElement(xml.Root.GetDefaultNamespace() + "Reference",
                    new XAttribute("Include", asmName));

                projectReference.ReplaceWith(asmReference);
            }

            var nativeLibRegex = new Regex(@"\.\.\\(\w+)\\(?<lib>\w+)\\(?<arch>\w+)\\(?<plat>\w+)\\(?<dll>\w+\.(dll|so|dylib))", RegexOptions.Singleline);
            var nativeLibReferences = (from x in xml.Descendants(xmlNs + "None")
                                       let incl = (String)x.Attribute("Include")
                                       let link = x.Element(xmlNs + "Link")
                                       where
                                         link != null &&
                                         incl != null && incl.StartsWith("..")
                                       select x).ToList();

            foreach (var nativeLibReference in nativeLibReferences)
            {
                var path = nativeLibReference.Attribute("Include").Value;

                var pathMatch = nativeLibRegex.Match(path);
                if (pathMatch.Success)
                {
                    var nativeLibPath = String.Format("$(MSBuildProgramFiles32)\\Ultraviolet Framework\\{0}\\Dependencies\\{1}\\{2}\\{3}", version, 
                        pathMatch.Groups["arch"].Value, pathMatch.Groups["plat"].Value, pathMatch.Groups["dll"].Value);
                    var nativeLibLink = nativeLibReference.Element(xmlNs + "Link");

                    nativeLibReference.ReplaceWith(new XElement(xmlNs + "None", new XAttribute("Include", nativeLibPath),
                        new XElement(xmlNs + "Link", nativeLibLink.Value),
                        new XElement(xmlNs + "CopyToOutputDirectory", "PreserveNewest")));
                }
            }

            xml.Save(file);
        }

        /// <summary>
        /// Zips up the specified directory in order to create a Visual Studio project template archive.
        /// </summary>
        private static void CreateTemplateArchive(String dir, String file)
        {
            File.Delete(file);
            ZipFile.CreateFromDirectory(dir, file);
        }
    }
}
