using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter
{
    internal static class DataLoader
    {
        public static void Populate(
            IDictionary<string, string> ianaMap,
            IDictionary<string, string> windowsMap,
            IDictionary<string, string> railsMap,
            IDictionary<string, IList<string>> inverseRailsMap,
            IDictionary<string, string> links,
            IDictionary<string, IList<string>> ianaTerritoryZones)
        {
            IEnumerable<string> mapping = GetEmbeddedData("MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter.Data.Mapping.csv.gz");
            IEnumerable<string> aliases = GetEmbeddedData("MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter.Data.Aliases.csv.gz");
            IEnumerable<string> railsMapping = GetEmbeddedData("MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter.Data.RailsMapping.csv.gz");
            IEnumerable<string> territories = GetEmbeddedData("MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter.Data.Territories.csv.gz");

            foreach (string link in aliases)
            {
                string[] parts = link.Split(',');
                string value = parts[0];
                foreach (string key in parts[1].Split())
                {
                    links.Add(key, value);
                }
            }

            foreach (string item in territories)
            {
                string[] parts = item.Split(',');
                string territory = parts[0];
                List<string> zones = new List<string>(parts[1].Split(' '));
                ianaTerritoryZones.Add(territory, zones);
            }

            Dictionary<string, IList<string>> similarIanaZones = new Dictionary<string, IList<string>>();
            foreach (string item in mapping)
            {
                string[] parts = item.Split(',');
                string windowsZone = parts[0];        // e.g. "Pacific Standard Time"
                string territory = parts[1];          // e.g. "US"
                string[] ianaZones = parts[2].Split();  // e.g. "America/Vancouver America/Dawson America/Whitehorse" -> `new String[] { "America/Vancouver", "America/Dawson", "America/Whitehorse" }`

                // Create the Windows map entry
                string key = territory + '|' + windowsZone;
                windowsMap.Add(key, ianaZones[0]);

                // Create the IANA map entries
                foreach (string ianaZone in ianaZones)
                {
                    if (!ianaMap.ContainsKey(ianaZone))
                    {
                        ianaMap.Add(ianaZone, windowsZone);
                    }
                }

                if (ianaZones.Length > 1)
                {
                    foreach (string ianaZone in ianaZones)
                    {
                        similarIanaZones.Add(ianaZone, ianaZones.Except(new[] { ianaZone }).ToArray());
                    }
                }
            }

            // Expand the IANA map to include all links (both directions)
            List<KeyValuePair<string, string>> linksToMap = links.ToList();
            while (linksToMap.Count > 0)
            {
                List<KeyValuePair<string, string>> retry = new List<KeyValuePair<string, string>>();
                foreach (KeyValuePair<string, string> link in linksToMap)
                {
                    string mapFromKey, mapFromValue;
                    bool hasMapFromKey = ianaMap.TryGetValue(link.Key, out mapFromKey);
                    bool hasMapFromValue = ianaMap.TryGetValue(link.Value, out mapFromValue);

                    if (hasMapFromKey && hasMapFromValue)
                    {
                        // There are already mappings in both directions
                        continue;
                    }

                    if (!hasMapFromKey && hasMapFromValue)
                    {
                        // Forward mapping
                        ianaMap.Add(link.Key, mapFromValue);
                    }
                    else if (!hasMapFromValue && hasMapFromKey)
                    {
                        // Reverse mapping
                        ianaMap.Add(link.Value, mapFromKey);
                    }
                    else
                    {
                        // Not found yet, but we can try again
                        retry.Add(link);
                    }
                }

                linksToMap = retry;
            }

            foreach (string item in railsMapping)
            {
                string[] parts = item.Split(',');
                string railsZone = parts[0];
                string[] ianaZones = parts[1].Split();

                for (int i = 0; i < ianaZones.Length; i++)
                {
                    string ianaZone = ianaZones[i];
                    if (i == 0)
                    {
                        railsMap.Add(railsZone, ianaZone);
                    }
                    else
                    {
                        inverseRailsMap.Add(ianaZone, new[] { railsZone });
                    }
                }
            }

            foreach (IGrouping<string, string> grouping in railsMap.GroupBy(x => x.Value, x => x.Key))
            {
                inverseRailsMap.Add(grouping.Key, grouping.ToList());
            }

            // Expand the Inverse Rails map to include similar IANA zones
            foreach (string ianaZone in ianaMap.Keys)
            {
                if (inverseRailsMap.ContainsKey(ianaZone) || links.ContainsKey(ianaZone))
                {
                    continue;
                }

                IList<string> similarZones;
                IList<string> railsZones;
                if (similarIanaZones.TryGetValue(ianaZone, out similarZones))
                {
                    foreach (string otherZone in similarZones)
                    {
                        if (inverseRailsMap.TryGetValue(otherZone, out railsZones))
                        {
                            inverseRailsMap.Add(ianaZone, railsZones);
                            break;
                        }
                    }
                }
            }

            // Expand the Inverse Rails map to include links (in either direction)

            foreach (KeyValuePair<string, string> link in links)
            {
                IList<string> railsZone;
                if (!inverseRailsMap.ContainsKey(link.Key))
                {
                    if (inverseRailsMap.TryGetValue(link.Value, out railsZone))
                    {
                        inverseRailsMap.Add(link.Key, railsZone);
                    }
                }
                else if (!inverseRailsMap.ContainsKey(link.Value))
                {
                    if (inverseRailsMap.TryGetValue(link.Key, out railsZone))
                    {
                        inverseRailsMap.Add(link.Value, railsZone);
                    }
                }
            }

            // Expand the Inverse Rails map to use CLDR golden zones
            foreach (string ianaZone in ianaMap.Keys)
            {
                IList<string> railsZones;
                string windowsZone, goldenZone;
                if (!inverseRailsMap.ContainsKey(ianaZone) &&
                    ianaMap.TryGetValue(ianaZone, out windowsZone) &&
                    windowsMap.TryGetValue("001|" + windowsZone, out goldenZone) &&
                    inverseRailsMap.TryGetValue(goldenZone, out railsZones))
                {
                    inverseRailsMap.Add(ianaZone, railsZones);
                }
            }
        }

        private static IEnumerable<string> GetEmbeddedData(string resourceName)
        {
            Assembly assembly = typeof(DataLoader).Assembly;
            using (Stream compressedStream = assembly.GetManifestResourceStream(resourceName))
            {
                using (GZipStream stream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            yield return line;
                        }
                    }
                }
            }
        }
    }
}
