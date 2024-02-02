using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebResumeAbortEventArgs : EventArgs
    {
        public bool Abort = true;
        public long FileOffset = 0;
        public long FileLength = -1;
    }
}
