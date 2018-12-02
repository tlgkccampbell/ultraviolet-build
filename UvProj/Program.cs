using System;
using System.IO;
using System.Xml.Linq;
using CommandLine;
using UvProj.Options;

namespace UvProj
{
    class Program
    {
        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<NewOptions>(args)
                .MapResult(
                    (NewOptions opts) => New(opts),
                    errs => 1);
        }

        private static Int32 Command(String cmdName, Func<String> cmd)
        {
            try
            {
                var result = cmd();
                WriteColoredLine($"{cmdName} succeeded. {result}", ConsoleColor.Green);
                return 0;
            }
            catch (Exception e)
            {
                WriteColoredLine($"{cmdName} failed.", ConsoleColor.Red);
                WriteColoredLine(e, ConsoleColor.DarkRed);
                return 1;
            }
        }

        private static Int32 New(NewOptions options) => Command("New project creation", () =>
        {
            // Look for Ultraviolet.proj and fail if we can't find it
            if (!File.Exists("Ultraviolet.proj"))
                throw new InvalidOperationException("Could not locate Ultraviolet.proj in the current directory.");

            // Create projects.
            var path = Path.Combine("Source", options.Name);
            CreateSharedProject(options.Name, 
                Path.Combine(path, "Shared"));
            CreateDesktopProject(options.Name, 
                Path.Combine(path, "Desktop"));
            CreateMacOSModernProject(options.Name, 
                Path.Combine(path, "macOSModern"));
            CreateNETStandardProject(options.Name, 
                Path.Combine(path, "NETStandard"));
            CreateAndroidProject(options.Name, 
                Path.Combine(path, "Android"));
            CreateiOSProject(options.Name, 
                Path.Combine(path, "iOS"));

            return null;
        });

        private static void CreateSharedProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating Shared project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateSharedProject_ProjectFile(projectName, projectPath, out var projectGuid);
            CreateSharedProject_ProjectItems(projectName, projectPath, projectGuid);
        }

        private static void CreateSharedProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XAttribute("ToolsVersion", "4.0"),
                    new XAttribute("DefaultTargets", "Build"),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Label", "Globals"),
                        new XElement(xmlns + "ProjectGuid", projectGuid),
                        new XElement(xmlns + "MinimumVisualStudioVersion", "12.0")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"),
                        new XAttribute("Condition", @"Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.Default.props"),
                        new XAttribute("Condition", @"Exists('$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.Default.props')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.props"),
                        new XAttribute("Condition", @"Exists('$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.props')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.CSharp.targets"),
                        new XAttribute("Condition", @"Exists('$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.CSharp.targets')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", $"{projectName}.projitems"),
                        new XAttribute("Label", "Shared")
                    )
                )
            );

            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.shproj"));
        }

        private static void CreateSharedProject_ProjectItems(String projectName, String projectPath, Guid projectGuid)
        {
            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "MSBuildAllProjects", "$(MSBuildAllProjects);$(MSBuildThisFileFullPath)"),
                        new XElement(xmlns + "HasSharedItems", "true"),
                        new XElement(xmlns + "SharedGUID", projectGuid)
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Label", "Configuration"),
                        new XElement(xmlns + "Import_RootNamespace", projectName)
                    ),
                    new XElement(xmlns + "ItemGroup")
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.projitems"));
        }

        private static void CreateDesktopProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating Desktop project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateDesktopProject_ProjectFile(projectName, projectPath, out var projectGuid);
            CreateDesktopProject_AssemblyInfo(projectName, projectPath);
            CreateDesktopProject_StringsDatabase(projectName, projectPath);
        }

        private static void CreateDesktopProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XAttribute("ToolsVersion", "12.0"),
                    new XAttribute("DefaultTargets", "Build"),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "Configuration",
                            new XAttribute("Condition", @" '$(Configuration)' == '' "), new XText("Debug")
                        ),
                        new XElement(xmlns + "Platform",
                            new XAttribute("Condition", @" '$(Platform)' == '' "), new XText("AnyCPU")
                        ),
                        new XElement(xmlns + "ProjectGuid", $"{{{projectGuid}}}"),
                        new XElement(xmlns + "OutputType", "Library"),
                        new XElement(xmlns + "AppDesignerFolder", "Properties"),
                        new XElement(xmlns + "RootNamespace", projectName),
                        new XElement(xmlns + "AssemblyName", projectName),
                        new XElement(xmlns + "TargetFrameworkVersion", "v4.6.1"),
                        new XElement(xmlns + "TargetFrameworkProfile", String.Empty),
                        new XElement(xmlns + "FileAlignment", "512")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "full"),
                        new XElement(xmlns + "Optimize", "false"),
                        new XElement(xmlns + "OutputPath", @"bin\Debug\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;DEBUG;DESKTOP"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Debug\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "Prefer32Bit", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Release\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;DESKTOP"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Release\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "Prefer32Bit", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Signed|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Signed\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;SIGNED;DESKTOP"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Signed\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "true"),
                        new XElement(xmlns + "DelaySign", "true"),
                        new XElement(xmlns + "Prefer32Bit", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "AssemblyOriginatorKeyFile", @"..\..\Ultraviolet.Public.snk")
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System.Core")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "Microsoft.CSharp")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Core.Desktop.csproj"),
                            new XElement(xmlns + "Project", "{7DA6158B-A0B9-4100-904E-22FD86949608}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Core.Desktop")
                        ),
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Desktop.csproj"),
                            new XElement(xmlns + "Project", "{F071ABE3-05E5-4DF9-A929-CF71B01EC50A}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Desktop")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "None",
                            new XAttribute("Include", @"..\..\Ultraviolet.Public.snk"),
                            new XElement(xmlns + "Link", "Ultraviolet.Public.snk")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Content",
                            new XAttribute("Include", @"..\..\Version.tt"),
                            new XElement(xmlns + "Link", @"Properties\Version.tt"),
                            new XElement(xmlns + "Generator", "TextTemplatingFileGenerator"),
                            new XElement(xmlns + "LastGenOutput", "Version.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "EmbeddedResource",
                            new XAttribute("Include", @"Resources\Strings.xml")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\..\Version.cs"),
                            new XElement(xmlns + "Link", @"Properties\Version.cs"),
                            new XElement(xmlns + "AutoGen", "True"),
                            new XElement(xmlns + "DesignTime", "True"),
                            new XElement(xmlns + "DependentUpon", "Version.tt")
                        ),
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"Properties\AssemblyInfo.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Service",
                            new XAttribute("Include", "{508349B6-6B84-4DF5-91F0-309BEEBAD82D}")
                        )
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"),
                        new XAttribute("Condition", @"Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildToolsPath)\Microsoft.CSharp.targets")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", $@"..\Shared\{projectName}.projitems"),
                        new XAttribute("Label", "Shared"),
                        new XAttribute("Condition", @"Exists('..\Shared\Ultraviolet.BASS.projitems')")
                    )
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.Desktop.csproj"));
        }

        private static void CreateDesktopProject_AssemblyInfo(String projectName, String projectPath)
        {
            var pathProperties = Path.Combine(projectPath, "Properties");
            Directory.CreateDirectory(pathProperties);

            using (var stream = File.Open(Path.Combine(pathProperties, "AssemblyInfo.cs"), FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("using System;");
                writer.WriteLine("using System.Reflection;");
                writer.WriteLine("using System.Runtime.InteropServices;");
                writer.WriteLine();
                writer.WriteLine("[assembly: CLSCompliant(false)]");
                writer.WriteLine();
                writer.WriteLine("// General Information about an assembly is controlled through the following ");
                writer.WriteLine("// set of attributes. Change these attribute values to modify the information");
                writer.WriteLine("// associated with an assembly.");
                writer.WriteLine("[assembly: AssemblyTitle(@\"Assembly name goes here.\")]");
                writer.WriteLine("[assembly: AssemblyDescription(");
                writer.WriteLine("    @\"Assembly description goes here.\")]");
                writer.WriteLine();
                writer.WriteLine("// Setting ComVisible to false makes the types in this assembly not visible ");
                writer.WriteLine("// to COM components.  If you need to access a type in this assembly from ");
                writer.WriteLine("// COM, set the ComVisible attribute to true on that type. Only Windows");
                writer.WriteLine("// assemblies support COM.");
                writer.WriteLine("[assembly: ComVisible(false)]");
                writer.WriteLine();
                writer.WriteLine("// On Windows, the following GUID is for the ID of the typelib if this");
                writer.WriteLine("// project is exposed to COM. On other platforms, it unique identifies the");
                writer.WriteLine("// title storage container when deploying this assembly to the device.");
                writer.WriteLine("[assembly: Guid(\"" + Guid.NewGuid() + "\")]");
            }
        }

        private static void CreateDesktopProject_StringsDatabase(String projectName, String projectPath)
        {
            var pathResources = Path.Combine(projectPath, "Resources");
            Directory.CreateDirectory(pathResources);

            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("LocalizedStrings")
            );
            xmldoc.Save(Path.Combine(pathResources, "Strings.xml"));
        }

        private static void CreateMacOSModernProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating macOSModern project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateMacOSModernProject_ProjectFile(projectName, projectPath, out var projectGuid);
        }

        private static void CreateMacOSModernProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XAttribute("ToolsVersion", "4.0"),
                    new XAttribute("DefaultTargets", "Build"),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "Configuration",
                            new XAttribute("Condition", @" '$(Configuration)' == '' "), new XText("Debug")
                        ),
                        new XElement(xmlns + "Platform",
                            new XAttribute("Condition", @" '$(Platform)' == '' "), new XText("AnyCPU")
                        ),
                        new XElement(xmlns + "ProjectGuid", $"{{{projectGuid}}}"),
                        new XElement(xmlns + "ProjectTypeGuids", "{A3F8F2AB-B479-4A4A-A458-A89E7DC349F1};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"),
                        new XElement(xmlns + "OutputType", "Library"),
                        new XElement(xmlns + "RootNamespace", projectName),
                        new XElement(xmlns + "AssemblyName", projectName),
                        new XElement(xmlns + "TargetFrameworkVersion", "v2.0"),
                        new XElement(xmlns + "TargetFrameworkIdentifier", "Xamarin.Mac"),
                        new XElement(xmlns + "MonoMacResourcePrefix", "Resources")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "full"),
                        new XElement(xmlns + "Optimize", "false"),
                        new XElement(xmlns + "OutputPath", @"bin\Debug\"),
                        new XElement(xmlns + "DefineConstants", "__UNIFIED__;__MACOS__;MACOS;MACOS_MODERN;DEBUG"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Debug\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "IncludeMonoRuntime", "false"),
                        new XElement(xmlns + "EnableCodeSigning", "false"),
                        new XElement(xmlns + "EnablePackageSigning", "false"),
                        new XElement(xmlns + "CreatePackage", "false"),
                        new XElement(xmlns + "UseSGen", "false"),
                        new XElement(xmlns + "XamMacArch", String.Empty),
                        new XElement(xmlns + "LinkMode", String.Empty),
                        new XElement(xmlns + "AOTMode", "None"),
                        new XElement(xmlns + "HttpClientHandler", String.Empty)
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Release\"),
                        new XElement(xmlns + "DefineConstants", "__UNIFIED__;__MACOS__;MACOS;MACOS_MODERN"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Release\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "IncludeMonoRuntime", "false"),
                        new XElement(xmlns + "EnableCodeSigning", "false"),
                        new XElement(xmlns + "EnablePackageSigning", "false"),
                        new XElement(xmlns + "CreatePackage", "false"),
                        new XElement(xmlns + "UseSGen", "false"),
                        new XElement(xmlns + "XamMacArch", String.Empty),
                        new XElement(xmlns + "LinkMode", String.Empty),
                        new XElement(xmlns + "AOTMode", "None"),
                        new XElement(xmlns + "HttpClientHandler", String.Empty)
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Signed|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "full"),
                        new XElement(xmlns + "Optimize", "false"),
                        new XElement(xmlns + "OutputPath", @"bin\Signed\"),
                        new XElement(xmlns + "DefineConstants", "__UNIFIED__;__MACOS__;MACOS;MACOS_MODERN;SIGNED"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Signed\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "true"),
                        new XElement(xmlns + "DelaySign", "true"),
                        new XElement(xmlns + "IncludeMonoRuntime", "false"),
                        new XElement(xmlns + "EnableCodeSigning", "false"),
                        new XElement(xmlns + "EnablePackageSigning", "false"),
                        new XElement(xmlns + "CreatePackage", "false"),
                        new XElement(xmlns + "UseSGen", "false"),
                        new XElement(xmlns + "XamMacArch", String.Empty),
                        new XElement(xmlns + "LinkMode", String.Empty),
                        new XElement(xmlns + "AOTMode", "None"),
                        new XElement(xmlns + "HttpClientHandler", String.Empty)
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "AssemblyOriginatorKeyFile", @"..\..\Ultraviolet.Public.snk")
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System.Core")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "Xamarin.Mac")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Core.macOSModern.csproj"),
                            new XElement(xmlns + "Project", "{2B22B67F-2B54-4973-A579-1714EE1297A9}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Core.macOSModern")
                        ),
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.macOSModern.csproj"),
                            new XElement(xmlns + "Project", "{1005CE63-D332-465D-8AC0-579EF73ADA8B}"),
                            new XElement(xmlns + "Name", "Ultraviolet.macOSModern")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "None",
                            new XAttribute("Include", @"..\..\Ultraviolet.Public.snk"),
                            new XElement(xmlns + "Link", "Ultraviolet.Public.snk")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Content",
                            new XAttribute("Include", @"..\..\Version.tt"),
                            new XElement(xmlns + "Link", @"Properties\Version.tt"),
                            new XElement(xmlns + "Generator", "TextTemplatingFileGenerator"),
                            new XElement(xmlns + "LastGenOutput", "Version.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "EmbeddedResource",
                            new XAttribute("Include", @"..\Desktop\Resources\Strings.xml"),
                            new XElement(xmlns + "Link", @"Resources\Strings.xml")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\..\Version.cs"),
                            new XElement(xmlns + "Link", @"Properties\Version.cs"),
                            new XElement(xmlns + "AutoGen", "True"),
                            new XElement(xmlns + "DesignTime", "True"),
                            new XElement(xmlns + "DependentUpon", "Version.tt")
                        ),
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\Desktop\Properties\AssemblyInfo.cs"),
                            new XElement(xmlns + "Link", @"Properties\AssemblyInfo.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Service",
                            new XAttribute("Include", "{508349B6-6B84-4DF5-91F0-309BEEBAD82D}")
                        )
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", $@"..\Shared\{projectName}.projitems"),
                        new XAttribute("Label", "Shared"),
                        new XAttribute("Condition", @"Exists('..\Shared\Ultraviolet.BASS.projitems')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath)\Xamarin\Mac\Xamarin.Mac.CSharp.targets")
                    )
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.macOSModern.csproj"));
        }

        private static void CreateNETStandardProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating NETStandard project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateNETStandardProject_ProjectFile(projectName, projectPath, out var projectGuid);
        }

        private static void CreateNETStandardProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmldoc = new XDocument(
                new XElement("Project",
                    new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                    new XElement("PropertyGroup",
                        new XElement("GenerateAssemblyInfo", "false"),
                        new XElement("Configurations", "Debug;Release;Signed")
                    ),
                    new XElement("PropertyGroup",
                        new XElement("TargetFramework", "netstandard2.0"),
                        new XElement("AssemblyName", projectName),
                        new XElement("RootNamespace", projectName),
                        new XElement("SignAssembly", "false"),
                        new XElement("DelaySign", "false"),
                        new XElement("AllowUnsafeBlocks", "true")
                    ),
                    new XElement("PropertyGroup",
                        new XAttribute("Condition", "'$(Configuration)|$(Platform)'=='Debug|AnyCPU'"),
                        new XElement("DefineConstants", "TRACE;NETSTANDARD;DEBUG"),
                        new XElement("DocumentationFile", $@"bin\Debug\netstandard2.0\{projectName}.xml")
                    ),
                    new XElement("PropertyGroup",
                        new XAttribute("Condition", "'$(Configuration)|$(Platform)'=='Release|AnyCPU'"),
                        new XElement("DefineConstants", "TRACE;NETSTANDARD"),
                        new XElement("DocumentationFile", $@"bin\Debug\netstandard2.0\{projectName}.xml")
                    ),
                    new XElement("PropertyGroup",
                        new XAttribute("Condition", "'$(Configuration)|$(Platform)'=='Signed|AnyCPU'"),
                        new XElement("DocumentationFile", $@"bin\Debug\netstandard2.0\{projectName}.xml"),
                        new XElement("DefineConstants", "TRACE;NETSTANDARD;SIGNED"),
                        new XElement("SignAssembly", "true"),
                        new XElement("DelaySign", "true")
                    ),
                    new XElement("ItemGroup",
                        new XElement("Compile",
                            new XAttribute("Include", @"..\..\Version.cs"),
                            new XAttribute("Link", @"Properties\Version.cs"),
                            new XElement("DependentUpon", "Version.tt")
                        ),
                        new XElement("Compile",
                            new XAttribute("Include", @"..\Desktop\Properties\AssemblyInfo.cs"),
                            new XAttribute("Link", @"Properties\AssemblyInfo.cs")
                        )
                    ),
                    new XElement("ItemGroup",
                        new XElement("EmbeddedResource",
                            new XAttribute("Include", @"..\Desktop\Resources\Strings.xml"),
                            new XAttribute("Link", @"Resources\Strings.xml")
                        )
                    ),
                    new XElement("ItemGroup",
                        new XElement("None",
                            new XAttribute("Include", @"..\..\Version.tt"),
                            new XAttribute("Link", @"Properties\Version.tt"),
                            new XElement("Generator", "TextTemplatingFileGenerator")
                        )
                    ),
                    new XElement("ItemGroup",
                        new XElement("Folder",
                            new XAttribute("Include", @"Properties\")
                        ),
                        new XElement("Folder",
                            new XAttribute("Include", @"Resources\")
                        )
                    ),
                    new XElement("ItemGroup",
                        new XElement("ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\NETStandard\Ultraviolet.Core.NETStandard.csproj")
                        ),
                        new XElement("ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet\NETStandard\Ultraviolet.NETStandard.csproj")
                        )
                    ),
                    new XElement("Import",
                        new XAttribute("Project", @"..\Shared\Ultraviolet.BASS.projitems"),
                        new XAttribute("Label", "Shared"),
                        new XAttribute("Condition", @"Exists('..\Shared\Ultraviolet.BASS.projitems')")
                    )
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.NETStandard.csproj"));
        }

        private static void CreateAndroidProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating Android project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateAndroidProject_ProjectFile(projectName, projectPath, out var projectGuid);
            CreateAndroidProject_ResourceDesigner(projectName, projectPath);
        }

        private static void CreateAndroidProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XAttribute("ToolsVersion", "4.0"),
                    new XAttribute("DefaultTargets", "Build"),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "Configuration",
                            new XAttribute("Condition", @" '$(Configuration)' == '' "), new XText("Debug")
                        ),
                        new XElement(xmlns + "Platform",
                            new XAttribute("Condition", @" '$(Platform)' == '' "), new XText("AnyCPU")
                        ),
                        new XElement(xmlns + "ProductVersion", "8.0.30703"),
                        new XElement(xmlns + "SchemaVersion", "2.0"),
                        new XElement(xmlns + "ProjectGuid", $"{{{projectGuid}}}"),
                        new XElement(xmlns + "ProjectTypeGuids", "{EFBA0AD7-5A72-4C68-AF49-83D382785DCF};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"),
                        new XElement(xmlns + "OutputType", "Library"),
                        new XElement(xmlns + "AppDesignerFolder", "Properties"),
                        new XElement(xmlns + "RootNamespace", projectName),
                        new XElement(xmlns + "AssemblyName", projectName),
                        new XElement(xmlns + "FileAlignment", "512"),
                        new XElement(xmlns + "AndroidResgenFile", @"Resources\Resource.Designer.cs"),
                        new XElement(xmlns + "GenerateSerializationAssemblies", "Off"),
                        new XElement(xmlns + "TargetFrameworkVersion", "v8.1")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "full"),
                        new XElement(xmlns + "Optimize", "false"),
                        new XElement(xmlns + "OutputPath", @"bin\Debug\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;DEBUG;ANDROID"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Debug\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Release\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;ANDROID"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Release\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Signed|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Signed\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;SIGNED;ANDROID"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Signed\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "true"),
                        new XElement(xmlns + "DelaySign", "true")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "AssemblyOriginatorKeyFile", @"..\..\Ultraviolet.Public.snk")
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "Mono.Android")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "mscorlib")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System.Core")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Core.Android.csproj"),
                            new XElement(xmlns + "Project", "{7eb671f1-6b46-426d-8a27-730d2b682043}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Core.Android")
                        ),
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Android.csproj"),
                            new XElement(xmlns + "Project", "{0b17931c-1595-4ada-9086-f26e5f5a387d}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Android")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "None",
                            new XAttribute("Include", @"..\..\Ultraviolet.Public.snk"),
                            new XElement(xmlns + "Link", "Ultraviolet.Public.snk")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Content",
                            new XAttribute("Include", @"..\..\Version.tt"),
                            new XElement(xmlns + "Link", @"Properties\Version.tt"),
                            new XElement(xmlns + "Generator", "TextTemplatingFileGenerator"),
                            new XElement(xmlns + "LastGenOutput", "Version.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "EmbeddedResource",
                            new XAttribute("Include", @"..\Desktop\Resources\Strings.xml"),
                            new XElement(xmlns + "Link", @"Resources\Strings.xml")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\..\Version.cs"),
                            new XElement(xmlns + "Link", @"Properties\Version.cs"),
                            new XElement(xmlns + "AutoGen", "True"),
                            new XElement(xmlns + "DesignTime", "True"),
                            new XElement(xmlns + "DependentUpon", "Version.tt")
                        ),
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\Desktop\Properties\AssemblyInfo.cs"),
                            new XElement(xmlns + "Link", @"Properties\AssemblyInfo.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Service",
                            new XAttribute("Include", "{508349B6-6B84-4DF5-91F0-309BEEBAD82D}")
                        )
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", $@"..\Shared\{projectName}.projitems"),
                        new XAttribute("Label", "Shared"),
                        new XAttribute("Condition", @"Exists('..\Shared\Ultraviolet.BASS.projitems')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath)\Xamarin\Android\Xamarin.Android.CSharp.targets")
                    )
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.Android.csproj"));
        }

        private static void CreateAndroidProject_ResourceDesigner(String projectName, String projectPath)
        {
            var pathResources = Path.Combine(projectPath, "Resources");
            Directory.CreateDirectory(pathResources);

            File.WriteAllText(Path.Combine(pathResources, "Resource.Designer.cs"), String.Empty);
        }

        private static void CreateiOSProject(String projectName, String projectPath)
        {
            WriteColoredLine("Creating iOS project...", ConsoleColor.Yellow);

            Directory.CreateDirectory(projectPath);

            CreateiOSProject_ProjectFile(projectName, projectPath, out var projectGuid);
            CreateiOSProject_ApiDefinition(projectName, projectPath);
            CreateiOSProject_Structs(projectName, projectPath);
        }

        private static void CreateiOSProject_ProjectFile(String projectName, String projectPath, out Guid projectGuid)
        {
            projectGuid = Guid.NewGuid();

            var xmlns = (XNamespace)"http://schemas.microsoft.com/developer/msbuild/2003";
            var xmldoc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(xmlns + "Project",
                    new XAttribute("ToolsVersion", "4.0"),
                    new XAttribute("DefaultTargets", "Build"),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "Configuration",
                            new XAttribute("Condition", @" '$(Configuration)' == '' "), new XText("Debug")
                        ),
                        new XElement(xmlns + "Platform",
                            new XAttribute("Condition", @" '$(Platform)' == '' "), new XText("AnyCPU")
                        ),
                        new XElement(xmlns + "ProductVersion", "8.0.30703"),
                        new XElement(xmlns + "SchemaVersion", "2.0"),
                        new XElement(xmlns + "ProjectGuid", $"{{{projectGuid}}}"),
                        new XElement(xmlns + "ProjectTypeGuids", "{FEACFBD2-3405-455C-9665-78FE426C6842};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"),
                        new XElement(xmlns + "OutputType", "Library"),
                        new XElement(xmlns + "RootNamespace", projectName),
                        new XElement(xmlns + "AssemblyName", projectName),
                        new XElement(xmlns + "IPhoneResourcePrefix", "Resources")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "full"),
                        new XElement(xmlns + "Optimize", "false"),
                        new XElement(xmlns + "OutputPath", @"bin\Debug\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;DEBUG;IOS"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Debug\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "ConsolePause", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Release\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;IOS"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Release\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "false"),
                        new XElement(xmlns + "DelaySign", "false"),
                        new XElement(xmlns + "ConsolePause", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XAttribute("Condition", @" '$(Configuration)|$(Platform)' == 'Signed|AnyCPU' "),
                        new XElement(xmlns + "DebugSymbols", "true"),
                        new XElement(xmlns + "DebugType", "pdbonly"),
                        new XElement(xmlns + "Optimize", "true"),
                        new XElement(xmlns + "OutputPath", @"bin\Signed\"),
                        new XElement(xmlns + "DefineConstants", "TRACE;SIGNED;IOS"),
                        new XElement(xmlns + "ErrorReport", "prompt"),
                        new XElement(xmlns + "WarningLevel", "4"),
                        new XElement(xmlns + "AllowUnsafeBlocks", "true"),
                        new XElement(xmlns + "TreatWarningsAsErrors", "true"),
                        new XElement(xmlns + "DocumentationFile", $@"bin\Signed\{projectName}.xml"),
                        new XElement(xmlns + "SignAssembly", "true"),
                        new XElement(xmlns + "DelaySign", "true"),
                        new XElement(xmlns + "ConsolePause", "false")
                    ),
                    new XElement(xmlns + "PropertyGroup",
                        new XElement(xmlns + "AssemblyOriginatorKeyFile", @"..\..\Ultraviolet.Public.snk")
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "System")
                        ),
                        new XElement(xmlns + "Reference",
                            new XAttribute("Include", "Xamarin.iOS")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.Core.iOS.csproj"),
                            new XElement(xmlns + "Project", "{8F9F44FB-E63C-48DE-8752-19B745111559}"),
                            new XElement(xmlns + "Name", "Ultraviolet.Core.iOS")
                        ),
                        new XElement(xmlns + "ProjectReference",
                            new XAttribute("Include", @"..\..\Ultraviolet.Core\Desktop\Ultraviolet.iOS.csproj"),
                            new XElement(xmlns + "Project", "{4628DF73-2C5C-4CD1-AB2C-197F6233504F}"),
                            new XElement(xmlns + "Name", "Ultraviolet.iOS")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "None",
                            new XAttribute("Include", @"..\..\Ultraviolet.Public.snk"),
                            new XElement(xmlns + "Link", "Ultraviolet.Public.snk")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Content",
                            new XAttribute("Include", @"..\..\Version.tt"),
                            new XElement(xmlns + "Link", @"Properties\Version.tt"),
                            new XElement(xmlns + "Generator", "TextTemplatingFileGenerator"),
                            new XElement(xmlns + "LastGenOutput", "Version.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "EmbeddedResource",
                            new XAttribute("Include", @"..\Desktop\Resources\Strings.xml"),
                            new XElement(xmlns + "Link", @"Resources\Strings.xml")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\..\Version.cs"),
                            new XElement(xmlns + "Link", @"Properties\Version.cs"),
                            new XElement(xmlns + "AutoGen", "True"),
                            new XElement(xmlns + "DesignTime", "True"),
                            new XElement(xmlns + "DependentUpon", "Version.tt")
                        ),
                        new XElement(xmlns + "Compile",
                            new XAttribute("Include", @"..\Desktop\Properties\AssemblyInfo.cs"),
                            new XElement(xmlns + "Link", @"Properties\AssemblyInfo.cs")
                        )
                    ),
                    new XElement(xmlns + "ItemGroup",
                        new XElement(xmlns + "Service",
                            new XAttribute("Include", "{508349B6-6B84-4DF5-91F0-309BEEBAD82D}")
                        )
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", $@"..\Shared\{projectName}.projitems"),
                        new XAttribute("Label", "Shared"),
                        new XAttribute("Condition", @"Exists('..\Shared\Ultraviolet.BASS.projitems')")
                    ),
                    new XElement(xmlns + "Import",
                        new XAttribute("Project", @"$(MSBuildExtensionsPath)\Xamarin\iOS\Xamarin.iOS.ObjCBinding.CSharp.targets")
                    )
                )
            );
            xmldoc.Save(Path.Combine(projectPath, $"{projectName}.iOS.csproj"));
        }

        private static void CreateiOSProject_ApiDefinition(String projectName, String projectPath)
        {
            using (var stream = File.Open(Path.Combine(projectPath, "ApiDefinition.cs"), FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("using System;");
                writer.WriteLine();
                writer.WriteLine("using UIKit;");
                writer.WriteLine("using Foundation;");
                writer.WriteLine("using ObjCRuntime;");
                writer.WriteLine("using CoreGraphics;");
                writer.WriteLine();
                writer.WriteLine("namespace " + projectName + ".iOS");
                writer.WriteLine("{");
                writer.WriteLine("}");
            }
        }

        private static void CreateiOSProject_Structs(String projectName, String projectPath)
        {
            using (var stream = File.Open(Path.Combine(projectPath, "Structs.cs"), FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("using System;");
                writer.WriteLine();
                writer.WriteLine("namespace " + projectName + ".iOS");
                writer.WriteLine("{");
                writer.WriteLine("}");
            }
        }

        private static void WriteColoredLine(Object line, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
        }
    }
}