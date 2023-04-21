using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.GUI
{
    public enum GUIPressureUnitEnum
    {
        Millibar,
        Hectopascal,

        [Description("Pounds per square inch")]
        PoundsPerSquareInch,

        Torr,
        Inch,

        [Description("Millimetre of mercury")]
        MillimetreOfMercury
    }
}
