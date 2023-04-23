    using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public enum HolidayTypeEnum
    {
        Unused = 0,

        [Description("New Year")]
        NewYear,

        Epiphany,

        [Description("Holy Thurstday")]
        HolyThurstday,

        [Description("Good Friday")]
        GoodFriday,

        [Description("Easter Sunday")]
        EasterSunday,

        [Description("Ascension Day")]
        AscensionDay,

        [Description("Whit Sunday")]
        WhitSunday,

        [Description("Corpus Christi")]
        CorpusChristi,

        [Description("Assumption Day")]
        AssumptionDay,

        [Description("Reformation Day")]
        ReformationDay,

        [Description("All Saints Day")]
        AllSaintsDay,

        [Description("Christmas Day")]
        ChristmasDay,

        Custom,

        [Description("Easter Monday")]
        EasterMonday
    }
}
