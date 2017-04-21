using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CSharp;
using Microsoft.VisualBasic;


namespace XtraTools.Tasks
{

	[Serializable]
	[LoadInSeparateAppDomain]
	public class XtraConfigTask : AppDomainIsolatedTask
	{

		[Required]
		public string Source { get; set; }

		[Required]
		public string OutputPath { get; set; }

		[Required]
		public string Namespace { get; set; }

		public string Class { get; set; } = "Config";

		public string CodeProvider { get; set; } = "CS";

		public bool InternalClass { get; set; } = false;

		[Output]
		public string GeneratedConfigPath { get; set; }

		[Output]
		public string GeneratedCode { get; set; }


		public override bool Execute()
		{
			if (String.IsNullOrEmpty(Source)) {
				return true;
			}

			try {
				var provider = CodeProvider.ToUpperInvariant() == "VB"
					? (CodeDomProvider)new VBCodeProvider()
					: (CodeDomProvider)new CSharpCodeProvider();

				//Validate config keys and generate code
				using (provider) {
					IList<KeyValuePair<string, string>> settings;
					if (File.Exists(Source)) {
						settings = XDocument
							.Load(Source)
							.XPathSelectElements("/configuration/appSettings/add")
							.Select(x => new KeyValuePair<string, string>(x.Attribute("key")?.Value, x.Attribute("value")?.Value))
							.ToList();

						ThrowWhenDuplicateKeys(settings);
						ThrowWhenDuplicateProperties(settings, provider);
					} else {
						Log.LogWarning($"Could not find source config file: \"{Source}\"");
						settings = new List<KeyValuePair<string, string>>();
					}

					using (var writer = new StringWriter()) {
						var ccu = StronglyTypedConfigBuilder.Create(settings, Class, Namespace, provider, InternalClass);
						provider.GenerateCodeFromCompileUnit(ccu, writer, new CodeGeneratorOptions { BlankLinesBetweenMembers = false });
						GeneratedCode = writer.ToString();
					}
				}

				//Prepare temporary file location for generated code
				string tempFilePath = OutputPath == null
					? Path.Combine(Path.GetTempPath(), $"{Namespace.Replace(".", "_")}_{Class}_{Path.GetRandomFileName().Replace(".", "")}.g.cs")
					: Path.Combine(OutputPath, $"{Namespace.Replace(".", "_")}_{Class}.g.cs");

				string tempFileDir = Path.GetDirectoryName(tempFilePath);
				if (tempFileDir != null) {
					Directory.CreateDirectory(tempFileDir);
				}

				//Write and output generated code
				File.WriteAllText(tempFilePath, GeneratedCode, Encoding.UTF8);
				GeneratedConfigPath = tempFilePath;
				return true;

			} catch (Exception ex) {
				Log.LogErrorFromException(ex, false, false, Source);
				return false;
			}
		}


		private static void ThrowWhenDuplicateKeys(IEnumerable<KeyValuePair<string, string>> settings)
		{
			var duplicateKeys = settings
				.GroupBy(s => s.Key)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key)
				.ToList();

			if (duplicateKeys.Any()) {
				var keys = String.Join(", ", duplicateKeys.ToArray());
				throw new Exception($"Duplicate keys are not allowed: {keys}");
			}
		}


		private static void ThrowWhenDuplicateProperties(IEnumerable<KeyValuePair<string, string>> settings, CodeDomProvider provider)
		{
			var duplicateCollapsedKeys = settings
				.GroupBy(s => StronglyTypedConfigBuilder.VerifyConfigKey(s.Key, provider))
				.Where(g => g.Count() > 1)
				.Select(g => g.Select(x => x.Key).Distinct())
				.ToList();

			if (duplicateCollapsedKeys.Any()) {
				var keys = String.Join("; ", duplicateCollapsedKeys.Select(x => String.Join(", ", x)));
				throw new Exception($"The following sets of keys are unsupported as they'll normalize to duplicate property names: {keys}");
			}
		}

	}

}
