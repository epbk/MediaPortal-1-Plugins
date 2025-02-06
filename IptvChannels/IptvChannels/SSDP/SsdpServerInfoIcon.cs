using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.SSDP
{
    public class SsdpServerInfoIcon
    {
        public string MimeType { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Depth { get; private set; }
        public string Url { get; private set; }


        public SsdpServerInfoIcon(string strMime, string strUrl , int iWidth, int iHeight, int iDepth)
        {
            this.MimeType = strMime;
            this.Url = strUrl;
            this.Width = iWidth;
            this.Height = iHeight;
            this.Depth = iDepth;
        }
    }
}
