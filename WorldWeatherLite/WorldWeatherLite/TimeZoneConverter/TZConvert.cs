using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter
{
    /// <summary>
    /// Converts time zone identifiers from various sources.
    /// </summary>
    public static class TZConvert
    {
        private static readonly bool IsWindows = true; //RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly Dictionary<string, string> IanaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> WindowsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> RailsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, IList<string>> InverseRailsMap = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> Links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, TimeZoneInfo> SystemTimeZones;

        private static readonly IDictionary<string, IList<string>> IanaTerritoryZones = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);

        static TZConvert()
        {
            DataLoader.Populate(IanaMap, WindowsMap, RailsMap, InverseRailsMap, Links, IanaTerritoryZones);

            HashSet<string> knownIanaTimeZoneNames = new HashSet<string>(IanaMap.Select(x => x.Key));
            HashSet<string> knownWindowsTimeZoneIds = new HashSet<string>(WindowsMap.Keys.Select(x => x.Split('|')[1]).Distinct());
            HashSet<string> knownRailsTimeZoneNames = new HashSet<string>(RailsMap.Select(x => x.Key));

            // Special case - not in any map.
            knownIanaTimeZoneNames.Add("Antarctica/Troll");

            // Remove zones from KnownIanaTimeZoneNames that have been removed from IANA data.
            // (They should still map to Windows zones correctly.)
            knownIanaTimeZoneNames.Remove("Canada/East-Saskatchewan"); // Removed in 2017c
            knownIanaTimeZoneNames.Remove("US/Pacific-New"); // Removed in 2018a

            // Remove zones from KnownWindowsTimeZoneIds that are marked obsolete in the Windows Registry.
            // (They should still map to IANA zones correctly.)
            knownWindowsTimeZoneIds.Remove("Kamchatka Standard Time");
            knownWindowsTimeZoneIds.Remove("Mid-Atlantic Standard Time");

            KnownIanaTimeZoneNames = knownIanaTimeZoneNames;
            KnownWindowsTimeZoneIds = knownWindowsTimeZoneIds;
            KnownRailsTimeZoneNames = knownRailsTimeZoneNames;

            SystemTimeZones = GetSystemTimeZones();
        }

        /// <summary>
        /// Gets a collection of all IANA time zone names known to this library.
        /// </summary>
        public static IEnumerable<string> KnownIanaTimeZoneNames { get; private set; }

        /// <summary>
        /// Gets a collection of all Windows time zone IDs known to this library.
        /// </summary>
        public static IEnumerable<string> KnownWindowsTimeZoneIds { get; private set; }

        /// <summary>
        /// Gets a collection of all Rails time zone names known to this library.
        /// </summary>
        public static IEnumerable<string> KnownRailsTimeZoneNames { get; private set; }

        /// <summary>
        /// Gets a dictionary that has an sorted collection of IANA time zone names keyed by territory code.
        /// </summary>
        /// <param name="fullList">
        /// When set <c>true</c>, each territory contains the full list zones applicable to that territory.
        /// Otherwise, the list is condensed to only those typically needed for selecting a time zone.
        /// </param>
        /// <returns>The dictionary of territories and time zone names.</returns>
        public static Dictionary<string, ReadOnlyCollection<string>> GetIanaTimeZoneNamesByTerritory(bool fullList = false)
        {
            if (fullList)
            {
                return new Dictionary<string, ReadOnlyCollection<string>>(
                    IanaTerritoryZones.ToDictionary(
                        x => x.Key,
                        x => (ReadOnlyCollection<string>)x.Value
                            .OrderBy(zone => zone)
                            .ToList().AsReadOnly()));
            }

            // Converting to windows and back has the reduction effect we are looking for
            string winId;
            return new Dictionary<string, ReadOnlyCollection<string>>(
                IanaTerritoryZones.ToDictionary(
                    x => x.Key,
                    x => (ReadOnlyCollection<string>)x.Value
                        .Select(zone => TryIanaToWindows(zone, out winId)
                            ? WindowsToIana(winId, x.Key)
                            : zone)
                        .OrderBy(zone => zone)
                        .Distinct()
                        .ToList().AsReadOnly()));
        }

        /// <summary>
        /// Converts an IANA time zone name to the equivalent Windows time zone ID.
        /// </summary>
        /// <param name="ianaTimeZoneName">The IANA time zone name to convert.</param>
        /// <returns>A Windows time zone ID.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent Windows
        /// zone.
        /// </exception>
        public static string IanaToWindows(string ianaTimeZoneName)
        {
            string windowsTimeZoneId;
            if (TryIanaToWindows(ianaTimeZoneName, out windowsTimeZoneId))
            {
                return windowsTimeZoneId;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid IANA time zone name, or has no equivalent Windows time zone.", ianaTimeZoneName));
        }

        /// <summary>
        /// Attempts to convert an IANA time zone name to the equivalent Windows time zone ID.
        /// </summary>
        /// <param name="ianaTimeZoneName">The IANA time zone name to convert.</param>
        /// <param name="windowsTimeZoneId">A Windows time zone ID.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryIanaToWindows(string ianaTimeZoneName, out string windowsTimeZoneId)
        {
            return IanaMap.TryGetValue(ianaTimeZoneName, out windowsTimeZoneId);
        }

        /// <summary>
        /// Converts a Windows time zone ID to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <returns>An IANA time zone name.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent IANA
        /// zone.
        /// </exception>
        public static string WindowsToIana(string windowsTimeZoneId, string territoryCode = "001")
        {
            return WindowsToIana(windowsTimeZoneId, territoryCode, LinkResolution.Default);
        }

        /// <summary>
        /// Converts a Windows time zone ID to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="linkResolutionMode">The mode of resolving links for the result.</param>
        /// <returns>An IANA time zone name.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent IANA
        /// zone.
        /// </exception>
        public static string WindowsToIana(string windowsTimeZoneId, LinkResolution linkResolutionMode)
        {
            return WindowsToIana(windowsTimeZoneId, "001", linkResolutionMode);
        }

        /// <summary>
        /// Converts a Windows time zone ID to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <param name="linkResolutionMode">The mode of resolving links for the result.</param>
        /// <returns>An IANA time zone name.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent IANA
        /// zone.
        /// </exception>
        public static string WindowsToIana(string windowsTimeZoneId, string territoryCode, LinkResolution linkResolutionMode)
        {
            string ianaTimeZoneName;
            if (TryWindowsToIana(windowsTimeZoneId, territoryCode, out ianaTimeZoneName, linkResolutionMode))
            {
                return ianaTimeZoneName;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid Windows time zone ID.", windowsTimeZoneId));
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to an equivalent IANA time zone name.
        /// Uses the "golden zone" - the one that is the most prevalent.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="ianaTimeZoneName">An IANA time zone name.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToIana(string windowsTimeZoneId, out string ianaTimeZoneName)
        {
            return TryWindowsToIana(windowsTimeZoneId, "001", out ianaTimeZoneName, LinkResolution.Default);
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to an equivalent IANA time zone name.
        /// Uses the "golden zone" - the one that is the most prevalent.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="ianaTimeZoneName">An IANA time zone name.</param>
        /// <param name="linkResolutionMode">The mode of resolving links for the result.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToIana(string windowsTimeZoneId, out string ianaTimeZoneName,
            LinkResolution linkResolutionMode)
        {
            return TryWindowsToIana(windowsTimeZoneId, "001", out ianaTimeZoneName, linkResolutionMode);
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <param name="ianaTimeZoneName">An IANA time zone name.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToIana(string windowsTimeZoneId, string territoryCode, out string ianaTimeZoneName)
        {
            return TryWindowsToIana(windowsTimeZoneId, territoryCode, out ianaTimeZoneName, LinkResolution.Default);
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <param name="ianaTimeZoneName">An IANA time zone name.</param>
        /// <param name="linkResolutionMode">The mode of resolving links for the result.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToIana(string windowsTimeZoneId, string territoryCode, out string ianaTimeZoneName,
            LinkResolution linkResolutionMode)
        {
            // try first with the given region
            string ianaId;
            bool found = WindowsMap.TryGetValue(territoryCode + '|' + windowsTimeZoneId, out ianaId);

            string goldenIanaId = null;
            if (territoryCode != "001" && (linkResolutionMode == LinkResolution.Default || !found))
            {
                // we need to look up the golden zone also
                bool goldenFound = WindowsMap.TryGetValue("001" + '|' + windowsTimeZoneId, out goldenIanaId);

                if (!found)
                {
                    found = goldenFound;
                    ianaId = goldenIanaId;
                }
            }

            if (!found)
            {
                ianaTimeZoneName = null;
                return false;
            }

            ianaTimeZoneName = ianaId;

            // resolve links
            switch (linkResolutionMode)
            {
                case LinkResolution.Default:
                    if (goldenIanaId == null || ianaId == goldenIanaId)
                    {
                        ianaTimeZoneName = ResolveLink(ianaId);
                    }
                    else
                    {
                        string goldenResolved = ResolveLink(goldenIanaId);
                        string specificResolved = ResolveLink(ianaId);
                        if (goldenResolved != specificResolved && !WindowsMap.ContainsValue(specificResolved))
                        {
                            ianaTimeZoneName = specificResolved;
                        }
                    }

                    return true;

                case LinkResolution.Canonical:
                    ianaTimeZoneName = ResolveLink(ianaId);
                    return true;

                case LinkResolution.Original:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(string.Format("linkResolutionMode: {0}", linkResolutionMode));
            }
        }

        private static string ResolveLink(string linkOrZone)
        {
            string zone;
            return Links.TryGetValue(linkOrZone, out zone) ? zone : linkOrZone;
        }

        /// <summary>
        /// Retrieves a <see cref="TimeZoneInfo" /> object given a valid Windows or IANA time zone identifier,
        /// regardless of which platform the application is running on.
        /// </summary>
        /// <param name="windowsOrIanaTimeZoneId">A valid Windows or IANA time zone identifier.</param>
        /// <returns>A <see cref="TimeZoneInfo" /> object.</returns>
        public static TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId)
        {
            TimeZoneInfo timeZoneInfo;
            if (TryGetTimeZoneInfo(windowsOrIanaTimeZoneId, out timeZoneInfo))
            {
                return timeZoneInfo;
            }

            throw new TimeZoneNotFoundException(string.Format("\"{0}\" was not found.", windowsOrIanaTimeZoneId));
        }

        /// <summary>
        /// Attempts to retrieve a <see cref="TimeZoneInfo" /> object given a valid Windows or IANA time zone identifier,
        /// regardless of which platform the application is running on.
        /// </summary>
        /// <param name="windowsOrIanaTimeZoneId">A valid Windows or IANA time zone identifier.</param>
        /// <param name="timeZoneInfo">A <see cref="TimeZoneInfo" /> object.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryGetTimeZoneInfo(string windowsOrIanaTimeZoneId, out TimeZoneInfo timeZoneInfo)
        {
            if (string.Equals(windowsOrIanaTimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                timeZoneInfo = TimeZoneInfo.Utc;
                return true;
            }

            // Try a direct approach 
            if (SystemTimeZones.TryGetValue(windowsOrIanaTimeZoneId, out timeZoneInfo))
            {
                return true;
            }

            // Convert to the opposite platform and try again.
            // Note, we use LinkResolution.Original here for some minor perf gain.
            string tzid;
            if (((IsWindows && TryIanaToWindows(windowsOrIanaTimeZoneId, out tzid)) ||
                 TryWindowsToIana(windowsOrIanaTimeZoneId, out tzid, LinkResolution.Original)) &&
                SystemTimeZones.TryGetValue(tzid, out timeZoneInfo))
            {
                return true;
            }

            // See if we know how to create an equivalent custom time zone.
            if (CustomTimeZoneFactory.TryGetTimeZoneInfo(windowsOrIanaTimeZoneId, out timeZoneInfo))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts an IANA time zone name to one or more equivalent Rails time zone names.
        /// </summary>
        /// <param name="ianaTimeZoneName">The IANA time zone name to convert.</param>
        /// <returns>One or more equivalent Rails time zone names.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent Rails
        /// zone.
        /// </exception>
        public static IList<string> IanaToRails(string ianaTimeZoneName)
        {
            IList<string> railsTimeZoneNames;
            if (TryIanaToRails(ianaTimeZoneName, out railsTimeZoneNames))
            {
                return railsTimeZoneNames;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid IANA time zone name, or has no equivalent Rails time zone.", ianaTimeZoneName));
        }

        /// <summary>
        /// Attempts to convert an IANA time zone name to one or more equivalent Rails time zone names.
        /// </summary>
        /// <param name="ianaTimeZoneName">The IANA time zone name to convert.</param>
        /// <param name="railsTimeZoneNames">One or more equivalent Rails time zone names.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryIanaToRails(string ianaTimeZoneName, out IList<string> railsTimeZoneNames)
        {
            // try directly first
            if (InverseRailsMap.TryGetValue(ianaTimeZoneName, out railsTimeZoneNames))
            {
                return true;
            }

            // try again with the golden zone
            string windowsTimeZoneId;
            string ianaGoldenZone;
            if (TryIanaToWindows(ianaTimeZoneName, out windowsTimeZoneId) &&
                TryWindowsToIana(windowsTimeZoneId, out ianaGoldenZone) &&
                InverseRailsMap.TryGetValue(ianaGoldenZone, out railsTimeZoneNames))
            {
                return true;
            }

            railsTimeZoneNames = new List<string>();
            return false;
        }

        /// <summary>
        /// Converts a Rails time zone name to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="railsTimeZoneName">The Rails time zone name to convert.</param>
        /// <returns>An IANA time zone name.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent IANA
        /// zone.
        /// </exception>
        public static string RailsToIana(string railsTimeZoneName)
        {
            string ianaTimeZoneName;
            if (TryRailsToIana(railsTimeZoneName, out ianaTimeZoneName))
            {
                return ianaTimeZoneName;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid Rails time zone name.", railsTimeZoneName));
        }

        /// <summary>
        /// Attempts to convert a Rails time zone name to an equivalent IANA time zone name.
        /// </summary>
        /// <param name="railsTimeZoneName">The Rails time zone name to convert.</param>
        /// <param name="ianaTimeZoneName">An IANA time zone name.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryRailsToIana(string railsTimeZoneName, out string ianaTimeZoneName)
        {
            return RailsMap.TryGetValue(railsTimeZoneName, out ianaTimeZoneName);
        }

        /// <summary>
        /// Converts a Rails time zone name to an equivalent Windows time zone ID.
        /// </summary>
        /// <param name="railsTimeZoneName">The Rails time zone name to convert.</param>
        /// <returns>A Windows time zone ID.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent Windows
        /// zone.
        /// </exception>
        public static string RailsToWindows(string railsTimeZoneName)
        {
            string windowsTimeZoneId;
            if (TryRailsToWindows(railsTimeZoneName, out windowsTimeZoneId))
            {
                return windowsTimeZoneId;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid Rails time zone name.", railsTimeZoneName));
        }

        /// <summary>
        /// Attempts to convert a Rails time zone name to an equivalent Windows time zone ID.
        /// </summary>
        /// <param name="railsTimeZoneName">The Rails time zone name to convert.</param>
        /// <param name="windowsTimeZoneId">A Windows time zone ID.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryRailsToWindows(string railsTimeZoneName, out string windowsTimeZoneId)
        {
            string ianaTimeZoneName;
            if (TryRailsToIana(railsTimeZoneName, out ianaTimeZoneName) &&
                TryIanaToWindows(ianaTimeZoneName, out windowsTimeZoneId))
            {
                return true;
            }

            windowsTimeZoneId = null;
            return false;
        }

        /// <summary>
        /// Converts a Windows time zone ID to one ore more equivalent Rails time zone names.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <returns>One or more equivalent Rails time zone names.</returns>
        /// <exception cref="InvalidTimeZoneException">
        /// Thrown if the input string was not recognized or has no equivalent Rails
        /// zone.
        /// </exception>
        public static IList<string> WindowsToRails(string windowsTimeZoneId, string territoryCode = "001")
        {
            IList<string> railsTimeZoneNames;
            if (TryWindowsToRails(windowsTimeZoneId, territoryCode, out railsTimeZoneNames))
            {
                return railsTimeZoneNames;
            }

            throw new InvalidTimeZoneException(
                string.Format("\"{0}\" was not recognized as a valid Windows time zone ID, or has no equivalent Rails time zone.", windowsTimeZoneId));
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to one ore more equivalent Rails time zone names.
        /// Uses the "golden zone" - the one that is the most prevalent.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="railsTimeZoneNames">One or more equivalent Rails time zone names.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToRails(string windowsTimeZoneId, out IList<string> railsTimeZoneNames)
        {
            return TryWindowsToRails(windowsTimeZoneId, "001", out railsTimeZoneNames);
        }

        /// <summary>
        /// Attempts to convert a Windows time zone ID to one ore more equivalent Rails time zone names.
        /// </summary>
        /// <param name="windowsTimeZoneId">The Windows time zone ID to convert.</param>
        /// <param name="territoryCode">
        /// An optional two-letter ISO Country/Region code, used to get a a specific mapping.
        /// Defaults to "001" if not specified, which means to get the "golden zone" - the one that is most prevalent.
        /// </param>
        /// <param name="railsTimeZoneNames">One or more equivalent Rails time zone names.</param>
        /// <returns><c>true</c> if successful, <c>false</c> otherwise.</returns>
        public static bool TryWindowsToRails(string windowsTimeZoneId, string territoryCode,
            out IList<string> railsTimeZoneNames)
        {
            string ianaTimeZoneName;
            if (TryWindowsToIana(windowsTimeZoneId, territoryCode, out ianaTimeZoneName) &&
                TryIanaToRails(ianaTimeZoneName, out railsTimeZoneNames))
            {
                return true;
            }

            railsTimeZoneNames = new List<string>();
            return false;
        }

        private static Dictionary<string, TimeZoneInfo> GetSystemTimeZones()
        {
            // Clear the TZI cache to ensure we have as pristine data as possible
            TimeZoneInfo.ClearCachedData();

            // Get the system time zones, grouped to remove duplicates with casing (though this should be very rare since we cleared cache)
            Dictionary<string, TimeZoneInfo> zones = TimeZoneInfo.GetSystemTimeZones()
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            if (IsWindows)
            {
                return zones;
            }

            // On non-Windows systems, expand to include any known IANA time zone names that weren't returned by the
            // GetSystemTimeZones call.  Specifically, links and Etc zones.
            foreach (string name in KnownIanaTimeZoneNames)
            {
                if (zones.ContainsKey(name))
                {
                    continue;
                }

                try
                {
                    TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById(name);
                    zones.Add(tzi.Id, tzi);
                }
                catch
                {
                    // ignored
                }
            }

            return zones;
        }
    }
}
