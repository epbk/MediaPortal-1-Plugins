﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MediaPortal.IptvChannels.Proxy.MediaServer
{
    public class ContentProtectionBox
    {
        public string PSSH
        { get; private set; }
                
        public readonly List<ContentProtectionKey> Keys = new List<ContentProtectionKey>();

        public DateTime LastRefresh = DateTime.MinValue;
        public DateTime LastAccess = DateTime.MinValue;

        public bool Refreshing = false;
        public readonly ManualResetEvent FlagRefreshDone = new ManualResetEvent(false);

        public ContentProtectionBox(string strPSSH)
        {
            this.PSSH = strPSSH;
        }
    }
}
