using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using LibGit2Sharp;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AirtimeBuildTasks
{

	public class GenerateVersionInfoFromGitTask : Task
	{

		[Required]
		public string RepositoryDirectory { get; set; }

		[Required]
		public string TemplateFile { get; set; }

		[Required]
		public string OutputFile { get; set; }

		public string VersionTagFormat { get; set; } = "v0.0.0";


		public override bool Execute()
		{
			try {
				if (!File.Exists(TemplateFile)) {
					throw new Exception($"Could not find TemplateFile \"{TemplateFile}\"");
				}

				var tagPattern = new Regex(VersionTagFormat.Replace("0", @"\d+").Replace(".", @"\."), RegexOptions.Compiled);

				string template = ReadFile(TemplateFile);
				string oldOutput = ReadFile(OutputFile);
				string newOutput;

				using (var repo = new Repository(RepositoryDirectory)) {
					var result = FindLatestMatchingTag(repo, tagPattern);
					var match = VersionNumberPattern.Match(result.Tag?.FriendlyName ?? VersionTagFormat);
					var valuesLookup = new Dictionary<string, object> {
						["Year"] = DateTime.Now.Year,
						["Branch"] = result.Branch,
						["CommitHashShort"] = result.Commit?.Sha.Substring(0, 8) ?? new String('0', 8),
						["CommitHashLong"] = result.Commit?.Sha,
						["TagDistance"] = result.Distance,
						["VersionTag"] = result.Tag?.FriendlyName ?? VersionTagFormat,
						["VersionTagNumber"] = match.Value,
						["VersionTagMajor"] = match.Groups["major"].Value,
						["VersionTagMinor"] = match.Groups["minor"].Value,
						["VersionTagBuild"] = match.Groups["build"].Value,
						["VersionTagRevision"] = match.Groups["revision"].Value,
					};
					newOutput = template.NamedFormat(valuesLookup);
				}

				if (oldOutput != newOutput) {
					File.WriteAllText(OutputFile, newOutput);
				}

			} catch (IOException ex) {
				Log.LogMessage(MessageImportance.Low, $"{GetType()?.Name} warning: {ex.Message}");
				return true;
			} catch (UnauthorizedAccessException ex) {
				Log.LogMessage(MessageImportance.Low, $"{GetType()?.Name} warning: {ex.Message}");
				return true;
			} catch (RepositoryNotFoundException ex) {
				Log.LogWarning($"{GetType()?.Name} warning: {ex.Message}");
				return true;

			} catch (Exception ex) {
				Log.LogError($"{GetType()?.Name} failed: {ex.Message}");
				return false;
			}

			return true;
		}


		private static string ReadFile(string filePath)
		{
			try {
				using (var inStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var reader = new StreamReader(inStream)) {
					return reader.ReadToEnd();
				}
			} catch (DirectoryNotFoundException) {
				return String.Empty;
			} catch (FileNotFoundException) {
				return String.Empty;
			}
		}


		private static TagSearchResult FindLatestMatchingTag(IRepository repo, Regex matchTagName)
		{
			int distance = 0;
			Tag matchedTag = null;
			var tip = repo.Head.Tip;

			if (tip != null) {
				var matchedTags = repo.Tags
					.Where(x => matchTagName.IsMatch(x.FriendlyName))
					.ToLookup(x => x.Target.Sha);

				foreach (var commit in repo.Commits.QueryBy(new CommitFilter { IncludeReachableFrom = tip })) {
					matchedTag = matchedTags[commit.Sha].FirstOrDefault();
					if (matchedTag != null) {
						break;
					}
					distance++;
				}
			}

			return new TagSearchResult {
				Commit = tip,
				Tag = matchedTag,
				Distance = distance,
				Branch = (repo.Info.IsHeadUnborn || repo.Info.IsHeadDetached) ? "no branch" : repo.Head.FriendlyName
			};
		}

		private class TagSearchResult
		{
			public Tag Tag { get; set; }
			public Commit Commit { get; set; }
			public int Distance { get; set; }
			public string Branch { get; set; }
		}


		private readonly static Regex VersionNumberPattern = new Regex(@"(?<major>\d+)(\.(?<minor>\d+))?(\.(?<build>\d+))?(\.(?<revision>\d+))?", RegexOptions.Compiled);

	}

}

