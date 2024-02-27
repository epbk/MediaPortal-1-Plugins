using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Net;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.IptvChannels.Proxy
{
    class HlsSequencer
    {
        public static readonly Regex RegexTitle = new Regex("(?<dur>[\\d\\.]+)(\\s*,\\s*(?<tit>.+)*)*");
        public static readonly Regex RegexXparam = new Regex("(?<key>[^,=]+)=(?<value>[^,]+)");
        public static readonly Regex RegexSqid = new Regex("(?<sqid>\\d+)");
        public static readonly Regex RegexResolution = new Regex("(?<resx>\\d+)x(?<resy>\\d+)");
    }
}
