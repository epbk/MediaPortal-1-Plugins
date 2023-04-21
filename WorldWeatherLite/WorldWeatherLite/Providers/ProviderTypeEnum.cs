using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public enum ProviderTypeEnum
    {
        MSN = 0,
        FORECA = 1,

        [Description("AccuWeather")]
        ACCU_WEATHER = 2
    }
}
