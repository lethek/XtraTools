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

				var tagPattern = new Regex(VersionTagFormat.Replace("0", @"\d").Replace(".", @"\."), RegexOptions.Compiled);

				string template;
				using (var inStream = new FileStream(TemplateFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var reader = new StreamReader(inStream)) {
					template = reader.ReadToEnd();
				}

				using (var outStream = new FileStream(OutputFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
					using (var repo = new Repository(RepositoryDirectory)) {
						var result = FindLatestMatchingTag(repo, tagPattern);

						Dictionary<string, object> valuesLookup;
						if (result.Tag != null) {
							var match = VersionNumberPattern.Match(result.Tag.Name);
							valuesLookup = new Dictionary<string, object> {
								["Year"] = DateTime.Now.Year,
								["CommitHashShort"] = result.Commit?.Sha.Substring(0, 8),
								["CommitHashLong"] = result.Commit?.Sha,
								["TagDistance"] = result.Distance,
								["VersionTag"] = result.Tag.Name,
								["VersionTagNumber"] = match.Value,
								["VersionTagMajor"] = match.Groups["major"].Value,
								["VersionTagMinor"] = match.Groups["minor"].Value,
								["VersionTagBuild"] = match.Groups["build"].Value,
								["VersionTagRevision"] = match.Groups["revision"].Value
							};

						} else {
							var match = VersionNumberPattern.Match(VersionTagFormat);
							valuesLookup = new Dictionary<string, object> {
								["Year"] = DateTime.Now.Year,
								["CommitHashShort"] = new String('0', 8),
								["CommitHashLong"] = "",
								["TagDistance"] = 0,
								["VersionTag"] = VersionTagFormat,
								["VersionTagNumber"] = match.Value,
								["VersionTagMajor"] = match.Groups["major"].Value,
								["VersionTagMinor"] = match.Groups["minor"].Value,
								["VersionTagBuild"] = match.Groups["build"].Value,
								["VersionTagRevision"] = match.Groups["revision"].Value
							};
						}

						using (var writer = new StreamWriter(outStream)) {
							writer.Write(template.NamedFormat(valuesLookup));
						}
					}
				}

			} catch (IOException ex) {
				Log.LogMessage(MessageImportance.Low, $"{GetType()?.Name} warning: {ex.Message}");
				return true;

			} catch (RepositoryNotFoundException ex) {
				Log.LogWarning($"{ex.Message}");
				return true;

			} catch (Exception ex) {
				Log.LogError($"{GetType()?.Name} failed: {ex.Message}");
				return false;
			}

			return true;
		}


		private static TagSearchResult FindLatestMatchingTag(IRepository repo, Regex matchTagName, string sha)
		{
			var tip = repo.Lookup<Commit>(sha);
			if (tip == null) {
				throw new ArgumentException($"Commit hash not found: {sha}", nameof(sha));
			}

			return FindLatestMatchingTag(repo, matchTagName, tip);
		}


		private static TagSearchResult FindLatestMatchingTag(IRepository repo, Regex matchTagName, Commit tip = null)
		{
			int distance = 0;
			Tag matchedTag = null;

			if (tip == null) {
				tip = repo.Head.Tip;
			}

			var tags = repo.Tags
				.Where(x => matchTagName.IsMatch(x.Name))
				.ToDictionary(x => x.Target.Sha);

			foreach (var commit in repo.Commits.QueryBy(new CommitFilter { Since = tip })) {
				tags.TryGetValue(commit.Sha, out matchedTag);
				if (matchedTag != null) {
					break;
				}
				distance++;
			}

			return new TagSearchResult() {
				Commit = tip,
				Tag = matchedTag,
				Distance = distance
			};
		}

		private class TagSearchResult
		{
			public Tag Tag { get; set; }
			public Commit Commit { get; set; }
			public int Distance { get; set; }
		}


		private readonly static Regex VersionNumberPattern = new Regex(@"(?<major>\d+)(\.(?<minor>\d+))?(\.(?<build>\d+))?(\.(?<revision>\d+))?", RegexOptions.Compiled);

	}

}

