using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebBeforeRequestEventArgs : EventArgs
    {
        public bool Abort = false;
        public bool Handled = false;
        public byte[] HttpRequest;
    }
}
