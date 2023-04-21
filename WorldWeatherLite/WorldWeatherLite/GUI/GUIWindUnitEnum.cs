using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.GUI
{
    public enum GUIWindUnitEnum
    {
        [Description("Kilometers per hour")]
        KilometersPerHour = 0,

        [Description("Meters per second")]
        MetersPerSecond,

        [Description("Miles per hour")]
        MilesPerHour,

        [Description("Knots")]
        Knote,

        Beaufort
    }
}
