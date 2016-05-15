using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using SandcastleBuilder.Utils;
using SandcastleBuilder.Utils.BuildComponent;
using SandcastleBuilder.Utils.BuildEngine;

namespace UvDocs
{
	/// <summary>
	/// A plugin for Sandcastle Help File Builder which modifies the configuration for MRefBuilder
	/// so that it can locate the Ultraviolet Presentation Foundation's dependency properties
	/// and routed events.
	/// </summary>
	[HelpFileBuilderPlugInExport("UPF Dependency Object Support", 
		Version = AssemblyInfo.ProductVersion,
		Copyright = AssemblyInfo.Copyright, 
		Description = "Ultraviolet dependency object plug-in")]
	public sealed class UvDependencyObjectPlugin : IPlugIn
	{
		/// <inheritdoc/>
		public String ConfigurePlugIn(SandcastleProject project, String currentConfig)
		{
			MessageBox.Show("This plug-in has no configurable settings", "Build Process Plug-In",
				MessageBoxButtons.OK, MessageBoxIcon.Information);

			return currentConfig;
		}

		/// <inheritdoc/>
		public void Initialize(BuildProcess buildProcess, XPathNavigator configuration)
		{
			builder = buildProcess;

			var metadata = (HelpFileBuilderPlugInExportAttribute)this.GetType().GetCustomAttributes(
				typeof(HelpFileBuilderPlugInExportAttribute), false).First();

			builder.ReportProgress("{0} Version {1}\r\n{2}", metadata.Id, metadata.Version, metadata.Copyright);
		}

		/// <inheritdoc/>
		public void Execute(ExecutionContext context)
		{
			var mrefConfigPath = Path.Combine(builder.WorkingFolder, "MRefBuilder.config");
			var mrefConfig = XDocument.Load(mrefConfigPath);

			var xamlAttachedMembersAddInElement = (
				from addin in mrefConfig.Descendants("addin")
				where
					(String)addin.Attribute("type") == "Microsoft.Ddue.Tools.XamlAttachedMembersAddIn"
				select addin).SingleOrDefault();

			xamlAttachedMembersAddInElement.ReplaceNodes(
				new XElement("dependencyPropertyTypeName", "TwistedLogik.Ultraviolet.UI.Presentation.DependencyProperty"),
				new XElement("dependencyPropertySuffix", "Property"),
				new XElement("routedEventTypeName", "TwistedLogik.Ultraviolet.UI.Presentation.RoutedEvent"),
				new XElement("routedEventSuffix", "Event"));

			mrefConfig.Save(mrefConfigPath);

			builder.ReportProgress("Configured MRefBuilder to use Ultraviolet Presentation Foundation dependency system types.");
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		public IEnumerable<ExecutionPoint> ExecutionPoints
		{
			get
			{
				yield return new ExecutionPoint(BuildStep.GenerateReflectionInfo, ExecutionBehaviors.Before);
			}
		}

		// The build process for this plugin.
		private BuildProcess builder;		
	}
}
