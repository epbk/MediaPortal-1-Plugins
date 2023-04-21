using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class WeatherData
    {
        /// <summary>
        /// Observation location name
        /// </summary>
        public string Location = null;

        /// <summary>
        /// Observation location longitude
        /// </summary>
        public double Longitude = double.MinValue;

        /// <summary>
        /// Observation location latitude
        /// </summary>
        public double Latitude = double.MinValue;

        /// <summary>
        /// Observation time
        /// </summary>
        public DateTime RefreshedAt = DateTime.MinValue;

        /// <summary>
        /// Temperature [°C]
        /// </summary>
        public int Temperature = int.MinValue;

        /// <summary>
        /// Feelslike temperature [°C]
        /// </summary>
        public int TemperatureFeelsLike = int.MinValue;

        /// <summary>
        /// Dew point [°C]
        /// </summary>
        public int DewPoint = int.MinValue;

        /// <summary>
        /// Humidity [%]
        /// </summary>
        public int Humidity = -1;

        /// <summary>
        /// Cloud coverage [%]
        /// </summary>
        public int CloudCoverage = -1;

        /// <summary>
        /// Visibility [m]
        /// </summary>
        public int Visibility = -1;

        /// <summary>
        /// Pressure [hPa]
        /// </summary>
        public float Pressure = -1;

        /// <summary>
        /// Condition text
        /// </summary>
        public string ConditionText = null;

        /// <summary>
        /// Condition icon code
        /// </summary>
        public int ConditionIconCode = -1;

        /// <summary>
        /// Condition translation code
        /// </summary>
        public Language.TranslationEnum ConditionTranslationCode = Language.TranslationEnum.unknown;

        /// <summary>
        /// Day sunrise time
        /// </summary>
        public DateTime SunRise = DateTime.MinValue;

        /// <summary>
        /// Day sunset time
        /// </summary>
        public DateTime SunSet = DateTime.MinValue;

        /// <summary>
        /// Wind strength [m/s]
        /// </summary>
        public float Wind = -1;

        /// <summary>
        /// Wind direction [°]
        /// </summary>
        public float WindDirection = -1;

        /// <summary>
        /// UV Index
        /// </summary>
        public int UVIndex = int.MinValue;

        /// <summary>
        /// Rain precipitation
        /// </summary>
        public float RainPrecipitation = float.NaN;
        
        /// <summary>
        /// Forecast
        /// </summary>
        public List<ForecastDay> ForecastDays = null;

    }
}
