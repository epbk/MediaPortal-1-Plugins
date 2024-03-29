﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public interface IWeatherProvider
    {
        /// <summary>
        /// Provider type
        /// </summary>
        ProviderTypeEnum Type
        { get; }

        /// <summary>
        /// Get name
        /// </summary>
        string Name
        { get; }

        /// <summary>
        /// Get current weather condition
        /// </summary>
        /// <param name="loc">Location</param>
        /// <param name="iRefreshInterval">Refresh interval in seconds</param>
        /// <returns>Weather data</returns>
        WeatherData GetCurrentWeatherData(Database.dbProfile loc, int iRefreshInterval);

        /// <summary>
        /// Search for locations
        /// </summary>
        /// <param name="strQuery">Name of location to search</param>
        /// <param name="strApiKey">Provider API key.</param>
        /// <returns>Locations</returns>
        IEnumerable<Database.dbProfile> Search(string strQuery, string strApiKey);

        /// <summary>
        /// Validate all informations(id, coordinates) id needed.
        /// </summary>
        /// <param name="profile">Profile to validate.</param>
        void FinalizeLocationData(Database.dbProfile profile);
    }
}
