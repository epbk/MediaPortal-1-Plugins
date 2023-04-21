using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebBeforeRequestEventArgs : EventArgs
    {
        public bool Handled = false;
        public byte[] HttpRequest;
    }
}
