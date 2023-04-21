using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class ForecastDay
    {
        /// <summary>
        /// Forecast date
        /// </summary>
        public DateTime Date = DateTime.MinValue;

        /// <summary>
        /// Temperature: min [°C]
        /// </summary>
        public int TemperatureMin = int.MinValue;

        /// <summary>
        /// Temperature: max [°C]
        /// </summary>
        public int TemperatureMax = int.MinValue;

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
    }
}
