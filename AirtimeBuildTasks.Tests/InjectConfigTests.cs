﻿using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

using Xunit;
using Xunit.Abstractions;


namespace AirtimeBuildTasks.Tests
{

	public class InjectConfigTests
	{

		public InjectConfigTests(ITestOutputHelper output)
		{
			Output = output;
		}


		[StaFact]
		private void ShouldGenerateBuildableCode()
		{
			bool success = CallMsBuild(ProjectXml, out string output);
			Output.WriteLine(output);
			Assert.True(success);
		}


		private bool CallMsBuild(string buildProject, out string output)
		{
			//TODO: refer to this for future updates: http://stackoverflow.com/a/7854806/84899
			using (var collection = new ProjectCollection()) {
				var project = collection.LoadProject(XmlReader.Create(new StringReader(buildProject)));
				var outputBuilder = new StringBuilder();
				using (var writer = new StringWriter(outputBuilder)) {
					try {
						collection.RegisterLogger(new ConsoleLogger(LoggerVerbosity.Normal, x => writer.Write(x), null, null));
						bool result = project.Build();
						return result;
					} finally {
						output = outputBuilder.ToString();
						collection.UnregisterAllLoggers();
					}
				}
			}
		}


		protected readonly ITestOutputHelper Output;


		private const string ProjectXml =
@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
	<UsingTask AssemblyFile=""AirtimeBuildTasks.dll"" TaskName=""InjectConfigTask"" />
	<PropertyGroup>
		<IntermediateOutputPath Condition=""$(IntermediateOutputPath) == '' Or $(IntermediateOutputPath) == '*Undefined*'"">$(MSBuildProjectDirectory)\obj\$(Configuration)</IntermediateOutputPath>
	</PropertyGroup>
	<Target Name=""InjectConfig"" BeforeTargets=""CoreCompile"">
		<InjectConfigTask Source=""Settings.config"" Namespace=""Airtime"" OutputPath=""$(IntermediateOutputPath)"">
			<Output ItemName=""Generated"" TaskParameter=""GeneratedConfigPath"" />
			<Output PropertyName=""GeneratedCode"" TaskParameter=""GeneratedCode""/>
		</InjectConfigTask>
		<ItemGroup>
			<Compile Include=""@(Generated)""/>
			<FileWrites Include=""@(Generated)""/>
		</ItemGroup>
		<Message Text=""$(GeneratedCode)"" />
	</Target>
</Project>";

	}

}
