using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebBeforeDownloadEventArgs : EventArgs
    {
        public bool Abort = false;
    }
}
