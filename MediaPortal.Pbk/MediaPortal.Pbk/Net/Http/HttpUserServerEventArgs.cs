using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserServerEventArgs: EventArgs
    {
        public bool Handled = false;
        public HttpMethodEnum Method;
        public string Path;
        public byte[] PostData;
        public CookieContainer Cookies;
        public Dictionary<string, string> HeaderFields;
        public Dictionary<string, string> ResponseHeaderFields;
        public HttpStatusCode ResponseCode = HttpStatusCode.NotFound;
        public string ResponseContentType;
        public byte[] ResponseData = null;
        public Stream ResponseStream = null;
        public Socket RemoteSocket = null;
        public bool ResponseSent = false;
        public bool KeepAlive = true;
        public bool CloseSocket = true;
    }
}
