using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite
{
    public enum FullscreenVideoBehaviorEnum
    {
        [Description("Run always")]
        RunAlways = 0,

        [Description("Run when playback is TV only")]
        RunWhenTvPlayback,

        Sleep
    }
}
