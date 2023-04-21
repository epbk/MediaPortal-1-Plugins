using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebBeforeSaveToFileEventArgs : EventArgs
    {
        public string FileNameFullPath = null;
    }
}
