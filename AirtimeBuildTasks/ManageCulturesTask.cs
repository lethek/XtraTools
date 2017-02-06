using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AirtimeBuildTasks
{

    public class ManageCulturesTask : Task
    {

        [Required]
        public string ConfigFile { get; set; }


        public override bool Execute()
        {
            try {
                if (!File.Exists(ConfigFile)) {
                    throw new Exception($"Could not find ConfigFile \"{ConfigFile}\"");
                }

                var cultureCodes = XDocument
                    .Load(ConfigFile)
                    .XPathSelectElements("/ManageCultures/Install/Culture")
                    .Select(x => x.Value.Trim())
                    .ToList();

                try {
                    cultureCodes.ForEach(RegisterCulture);
                } catch (UnauthorizedAccessException) {
                    Log.LogError($"{GetType()?.Name} failed: Insufficient privileges to register supplementary cultures; please try again as a system Administrator, or manually use the ManageCultures tool to ensure the following cultures are registered: {0}", String.Join(", ", cultureCodes));
                    return false;
                }

            } catch (Exception ex) {
                Log.LogError($"{GetType()?.Name} failed: {ex.Message}");
                return false;
            }

            return true;
        }


        private void RegisterCulture(string cultureName)
        {
            if (TryGetCultureInfo(cultureName) != null) {
                Log.LogMessage(MessageImportance.Low, $"Culture {cultureName} is already registered");
                return;
            }

            var cultureNameParts = ParseCultureNameRegex.Match(cultureName);

            if (!cultureNameParts.Success) {
                throw new Exception($"{cultureName} is not a valid culture name");
            }

            var baseCultureCode = cultureNameParts.Groups["culture"].Value;
            var regionCode = cultureNameParts.Groups["region"].Value;

            var baseCulture = TryGetCultureInfo(baseCultureCode);
            if (baseCulture == null) {
                throw new Exception($"Unable to find any pre-existing culture named {baseCultureCode} to load from");
            }

            var baseRegion = TryGetRegionInfo(regionCode);
            var cultureType = (baseRegion == null)
                ? CultureAndRegionModifiers.Neutral
                : CultureAndRegionModifiers.None;

            var builder = new CultureAndRegionInfoBuilder(cultureName, cultureType);
            builder.LoadDataFromCultureInfo(baseCulture);
            builder.Parent = baseCulture;
            if (baseRegion != null) {
                builder.LoadDataFromRegionInfo(baseRegion);
            }

            builder.Register();

            var culture = new CultureInfo(cultureName);
            string cultureTypeString = culture.IsNeutralCulture ? "neutral" : "supplemental";

            Log.LogMessage(MessageImportance.High, $"Registered new {cultureTypeString} culture {cultureName} from {baseCulture.Name}");
        }


        private static CultureInfo TryGetCultureInfo(string cultureName, bool useUserOverride = false)
        {
            try {
                return new CultureInfo(cultureName, useUserOverride);
            } catch (CultureNotFoundException) {
                return null;
            }
        }


        private static RegionInfo TryGetRegionInfo(string regionCode)
        {
            try {
                return new RegionInfo(regionCode);
            } catch (ArgumentException) {
                return null;
            }
        }


        private static readonly Regex ParseCultureNameRegex = new Regex(@"^((?<prefix>[iIxX])-)?(?<culture>(?<language>\w+)(-(?<region>\w+))?)(-(?<suffix>.*))$", RegexOptions.Compiled);

    }

}

