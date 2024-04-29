using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class ContentProtectionKey
    {
        public string KID
        { get; private set; }

        public string Key
        { get; private set; }

        public DateTime LastRefresh = DateTime.MinValue;

        public ContentProtectionKey(string strKID, string strKey)
        {
            this.KID = strKID;
            this.Key = strKey;
        }
    }
}
