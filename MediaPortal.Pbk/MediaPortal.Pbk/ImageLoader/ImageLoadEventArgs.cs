using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.ImageLoader
{
    public class ImageLoadEventArgs: EventArgs
    {
        public string Url = null;
        public string FilePath = null;
        public bool DownloadComplete = false;
        public object Tag = null;
    }
}
