using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using Sgml;
using NLog;
using System.Web;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Reflection;

namespace MediaPortal.IptvChannels
{
    public class WebTools
    {
        public const string EOL = "\r\n";
        public const string HTTP_HEADER_END = EOL + EOL;

        public const string HTTP_POST_CONTENT_TYPE = "application/x-www-form-urlencoded";
        public const string HTTP_DEFAULT_USER_AGENT = "Mozilla/5.0 (Windows NT 6.1; rv:41.0) Gecko/20100101 Firefox/41.0";
        public const string HTTP_POST_ACCEPT = "application/json, text/javascript, */*; q=0.01, text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1";

        private static Regex _RegexHttpFieldStatus = new Regex("HTTP[^\\s]+ (?<code>[^\\s]+) (?<result>.+)");
        private static Regex _RegexHttpField = new Regex("(?<key>[^:\\s]+)\\s*:\\s*(?<value>.+)");
        private static Regex _RegexHttpFieldMethod = new Regex("(?<type>GET|HEAD) (?<path>[^\\s]+)");
        private static Regex _RegexHttpFieldCookie = new Regex("(?<key>[^;]+)=(?<value>[^;]+)");

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public static bool CheckWebData(string url)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                _Logger.Debug("[CheckWebData] URL: {0}", url);
                request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "HEAD"; //Setting the Request method HEAD, you can also use GET too.
                response = request.GetResponse() as HttpWebResponse; //Getting the Web Response.
                return (response.StatusCode == HttpStatusCode.OK); //Returns TURE if the Status code == 200
            }
            catch (Exception ex)
            {
                _Logger.Error("[CheckWebData] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return false;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }

                if (request != null)
                {
                    request = null;
                }
            }
        }
        public static T GetWebData<T>(string url)
        {
            return GetWebData<T>(url, null);
        }
        public static T GetWebData<T>(string url, out string redirect)
        {
            return GetWebData<T>(url, null, true, out redirect, null, null, null, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, null, null, null, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, int timeout)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, null, null, null, null, null, null, null, null, false, timeout, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, string proxy)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, proxy, null, null, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, out string redirect)
        {
            return GetWebData<T>(url, encoding, true, out redirect, null, null, null, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, CookieContainer cookie, string strMethod)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, null, null, null, null, null, null, null, cookie, false, -1, strMethod);
        }
        public static T GetWebData<T>(string url, Encoding encoding, out string redirect, CookieContainer cookie)
        {
            return GetWebData<T>(url, encoding, true, out redirect, null, null, null, null, null, null, null, cookie, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, byte[] post)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, null, post, null, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, string strContentType, byte[] post)
        {
            string redirect;
            return GetWebData<T>(url, Encoding.UTF8, true, out redirect, null, post, strContentType, null, null, null, null, null, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, byte[] post, CookieContainer cookie)
        {
            string redirect;
            return GetWebData<T>(url, encoding, true, out redirect, null, post, null, null, null, null, null, cookie, false, -1, null);
        }
        public static T GetWebData<T>(string url, Encoding encoding, bool allowredirect, out string redirecturl, string proxy, byte[] post, string contentype, string referer,
            NameValueCollection headers, string userAgent, string accept, CookieContainer cookie, bool ignoreNotFound, int timeout, string strMethod)
        {
            redirecturl = null;
            SgmlReader sgmlReader = null;
            XmlTextReader xmltxtrd = null;
            MemoryStream mem_stream = null;
            HttpWebResponse resp = null;
            TextReader txreader = null;
            Stream respStream = null;
            Stream postStream = null;

            try
            {
                _Logger.Debug("[GetWebData] URL: {0}", url);

                HttpWebRequest req = WebRequest.Create(url) as HttpWebRequest;

                //req.AllowWriteStreamBuffering = true;
                //req.Timeout = 20000;

                if (!string.IsNullOrEmpty(proxy)) req.Proxy = new WebProxy(proxy); //x.x.x.x:yyyy

                if (!string.IsNullOrEmpty(userAgent)) req.UserAgent = userAgent;
                else req.UserAgent = HTTP_DEFAULT_USER_AGENT;

                if (!string.IsNullOrEmpty(accept)) req.Accept = accept;
                else req.Accept = "text/html, application/xml;q=0.9, application/xhtml+xml, image/png, image/jpeg, image/gif, image/x-xbitmap, */*;q=0.1";

                req.AllowAutoRedirect = allowredirect;
                req.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
                //req.Connection = "Alive";
                if (cookie != null) req.CookieContainer = cookie;
                if (headers != null) req.Headers.Add(headers);

                if (!string.IsNullOrEmpty(contentype)) req.ContentType = contentype;
                if (!string.IsNullOrEmpty(referer)) req.Referer = referer;

                if (post != null && post.Length > 0)
                {
                    req.Method = "POST";
                    if (string.IsNullOrEmpty(contentype)) req.ContentType = HTTP_POST_CONTENT_TYPE;
                    postStream = req.GetRequestStream();
                    postStream.Write(post, 0, post.Length);
                    postStream.Close();
                }
                else if (!string.IsNullOrEmpty(strMethod) && strMethod == "HEAD") req.Method = "HEAD";

                //Handle the response
                try
                {
                    if (timeout > 0)
                    {
                        IAsyncResult ia = req.BeginGetResponse(null, null);
                        if (!ia.AsyncWaitHandle.WaitOne(timeout))
                        {
                            req.Abort();
                            _Logger.Error("[GetWebData] Error: Connection timeout. '{0}'", url);
                            return (T)(object)null;
                        }

                        resp = (HttpWebResponse)req.EndGetResponse(ia);
                    }

                    else resp = req.GetResponse() as HttpWebResponse;

                }
                catch (WebException ex)
                {
                    HttpWebResponse hresp = (HttpWebResponse)ex.Response;
                    if (hresp == null)
                    {
                        _Logger.Error("[GetWebData] Error: error getting reponse('{0}') , '{1}'", ex.Message, url);
                        return (T)(object)null;
                    }

                    if (ignoreNotFound)// && hresp.StatusCode == HttpStatusCode.NotFound)
                    {
                        //Get response even with 404 error
                        resp = ex.Response as HttpWebResponse;
                    }
                    else
                    {
                        _Logger.Error("[GetWebData] Error: '{0}' , '{1}'", hresp.StatusCode, url);
                        return (T)(object)null;
                    }
                }

                if (allowredirect) redirecturl = resp.ResponseUri.AbsoluteUri;
                else
                {
                    if (resp.StatusCode == HttpStatusCode.Redirect) redirecturl = resp.GetResponseHeader("Location");
                    else redirecturl = resp.ResponseUri.AbsoluteUri;
                }
                //_logger.Debug(string.Format("[GetXML] ResponseUri: {0}", redirect));
                //if (resp.StatusCode == HttpStatusCode.Redirect)
                //{
                //    redirect = resp.GetResponseHeader("Location");
                //    _logger.Debug(string.Format("[GetXML] Redirect: {0}", redirect));
                //    resp.Close();
                //    req = WebRequest.Create(redirect) as HttpWebRequest;
                //    resp = req.GetResponse() as HttpWebResponse;
                //}
                //respStream = resp.GetResponseStream();

                if (resp.ContentEncoding.ToLower().Contains("gzip"))
                {
                    respStream = new GZipStream(resp.GetResponseStream(), CompressionMode.Decompress);
                }
                else if (resp.ContentEncoding.ToLower().Contains("deflate"))
                {
                    respStream = new DeflateStream(resp.GetResponseStream(), CompressionMode.Decompress);
                }
                else respStream = resp.GetResponseStream();


                if (typeof(T) == typeof(string))
                {
                    txreader = new StreamReader(respStream, encoding != null ? encoding : Encoding.GetEncoding(resp.CharacterSet));
                    string output = txreader.ReadToEnd();
                    return (T)(object)output;
                }
                else if (typeof(T) == typeof(byte[]))
                {
                    byte[] buffer = new byte[int.Parse(resp.Headers["Content-Length"])];

                    //BinaryReader br = new BinaryReader(respStream);
                    //buffer = br.ReadBytes(buffer.Length);

                    int bytesRead = 0;
                    int totalBytesRead = 0;
                    while (totalBytesRead < buffer.Length)
                    {
                        bytesRead = respStream.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                        totalBytesRead += bytesRead;
                    }

                    return (T)(object)buffer;
                }
                else if (typeof(T) == typeof(Image))
                {
                    Image img = Image.FromStream(respStream);
                    return (T)(object)img;
                }
                else if (typeof(T) == typeof(XmlDocument))
                {
                    // setup SgmlReader
                    sgmlReader = new SgmlReader();
                    sgmlReader.DocType = "HTML";
                    sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
                    sgmlReader.CaseFolding = CaseFolding.ToLower;
                    //sgmlReader.Href = url;
                    //if (!string.IsNullOrEmpty(proxy)) sgmlReader.WebProxy = proxy;
                    txreader = new StreamReader(respStream, encoding != null ? encoding : Encoding.GetEncoding(resp.CharacterSet));
                    sgmlReader.InputStream = txreader;
                    //sgmlReader.Read();

                    // create document
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.PreserveWhitespace = false;
                    xmldoc.XmlResolver = null;
                    xmldoc.Load(sgmlReader);

                    // I need to "reload" xml via XmlTextReader to ignore namespace
                    mem_stream = new MemoryStream();
                    xmldoc.Save(new XmlTextWriter(mem_stream, null));
                    mem_stream.Position = 0;
                    xmltxtrd = new XmlTextReader(mem_stream);
                    xmltxtrd.Namespaces = false;
                    xmldoc.Load(xmltxtrd);


                    return (T)(object)xmldoc;
                }
                else if (typeof(T) == typeof(JObject))
                {
                    txreader = new StreamReader(respStream, encoding != null ? encoding : Encoding.GetEncoding(resp.CharacterSet));
                    string strContent = txreader.ReadToEnd();
                    JObject jobject = (JObject)JsonConvert.DeserializeObject(strContent, typeof(JObject));
                    return (T)(object)jobject;
                }
                else if (typeof(T) == typeof(JToken))
                {
                    txreader = new StreamReader(respStream, encoding != null ? encoding : Encoding.GetEncoding(resp.CharacterSet));
                    string strContent = txreader.ReadToEnd();
                    JToken jobject = (JToken)JsonConvert.DeserializeObject(strContent, typeof(JToken));
                    return (T)(object)jobject;
                }
                else return (T)(object)null;

            }
            catch (Exception ex)
            {
                _Logger.Error("[GetWebData] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                return (T)(object)null;
            }
            finally
            {
                if (postStream != null)
                {
                    postStream.Close();
                    postStream.Dispose();
                }
                if (sgmlReader != null) sgmlReader.Close();
                if (xmltxtrd != null) xmltxtrd.Close();
                if (mem_stream != null)
                {
                    mem_stream.Close();
                    mem_stream.Dispose();
                }

                if (txreader != null)
                {
                    txreader.Close();
                    txreader.Dispose();
                }

                if (respStream != null)
                {
                    respStream.Close();
                    respStream.Dispose();
                }

                if (resp != null) resp.Close();

            }

        }

        internal static XmlDocument LoadHtml(string content)
        {
            SgmlReader sgmlReader = null;
            TextReader txreader = null;
            MemoryStream mem_stream = null;
            XmlTextReader xmltxtrd = null;
            try
            {
                //_logger.Debug(string.Format("[LoadHtml]"));
                // setup SgmlReader
                sgmlReader = new SgmlReader();
                sgmlReader.DocType = "HTML";
                sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
                sgmlReader.CaseFolding = CaseFolding.ToLower;
                txreader = new StringReader(content);
                sgmlReader.InputStream = txreader;
                //sgmlReader.Read();

                // create document
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.PreserveWhitespace = false;
                xmldoc.XmlResolver = null;
                xmldoc.Load(sgmlReader);

                txreader.Close();
                txreader.Dispose();
                txreader = null;

                // I need to "reload" xml via XmlTextReader to ignore namespace
                mem_stream = new MemoryStream();
                xmldoc.Save(new XmlTextWriter(mem_stream, null));
                mem_stream.Position = 0;
                xmltxtrd = new XmlTextReader(mem_stream);
                xmltxtrd.Namespaces = false;
                xmldoc.Load(xmltxtrd);

                return xmldoc;

            }
            catch (Exception ex)
            {
                _Logger.Error(string.Format("[LoadHtml] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace));
                return null;
            }
            finally
            {
                if (sgmlReader != null) sgmlReader.Close();
                if (xmltxtrd != null) xmltxtrd.Close();
                if (mem_stream != null)
                {
                    mem_stream.Close();
                    mem_stream.Dispose();
                    mem_stream = null;
                }

                if (txreader != null)
                {
                    txreader.Close();
                    txreader.Dispose();
                    txreader = null;
                }
            }

        }

        internal static List<Cookie> GetAllCookies(CookieContainer cookies)
        {
            List<Cookie> result = new List<Cookie>();

            Hashtable domains = (Hashtable)cookies.GetType().InvokeMember("m_domainTable", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, cookies, new object[] { });

            foreach (var domain in domains.Keys)
            {
                String strDomain = (string)domain;
                if (strDomain[0] == '.') strDomain = strDomain.Substring(1);

                SortedList m_list = (SortedList)domains[domain].GetType().InvokeMember("m_list", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, domains[domain], new object[] { });
                foreach (var item in m_list.Keys)
                {
                    ArrayList listCookie = (ArrayList)m_list[item].GetType().InvokeMember("m_list", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, m_list[item], new object[] { });

                    foreach (Cookie cookie in listCookie) result.Add(cookie);
                }
            }

            return result;
        }

        internal static IPEndPoint ParseIPEndPoint(string endPoint)
        {
            string[] ep = endPoint.Split(':');
            if (ep.Length != 2) return null;
            IPAddress ip;
            if (!IPAddress.TryParse(ep[0], out ip)) return null;
            int port;
            if (!int.TryParse(ep[1], System.Globalization.NumberStyles.None, System.Globalization.NumberFormatInfo.CurrentInfo, out port)) return null;
            return new IPEndPoint(ip, port);
        }

        //Callback used to validate the certificate in an SSL conversation
        internal static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            bool result = false;
            if (cert.Subject.ToUpper().Contains("YourServerName"))
            {
                result = true;
            }

            return result;
        }


        internal static byte[] Base64UrlDecode(string arg)
        {
            string s = arg;
            s = s.Replace('-', '+'); // 62nd char of encoding
            s = s.Replace('_', '/'); // 63rd char of encoding
            switch (s.Length % 4) // Pad with trailing '='s
            {
                case 0: break; // No pad chars in this case
                case 2: s += "=="; break; // Two pad chars
                case 3: s += "="; break; // One pad char
                default: throw new System.Exception("Illegal base64url string!");
            }
            return Convert.FromBase64String(s); // Standard base64 decoder
        }

        internal static string Base64UrlEncode(byte[] arg)
        {
            string s = Convert.ToBase64String(arg);
            s = s.Replace('+', '-'); // 62nd char of encoding
            s = s.Replace('/', '_'); // 63rd char of encoding
            s = s.TrimEnd('=');

            return s;
        }

        internal static HttpStatusCode GetHttpResponse(string strUrl, byte[] buffer, int iOffset, int iLength, ref Dictionary<string, string> headersRequest, out int iResponseLength, out string strHeader, ref CookieContainer cookies)
        {
            HttpStatusCode httpStatus = 0;

            Uri uri = new Uri(strUrl);

            iResponseLength = 0;
            strHeader = "";

            if (buffer == null) return httpStatus;

            if (headersRequest == null) headersRequest = new Dictionary<string, string>();
            else headersRequest.Clear();

            cookies = new CookieContainer();


            bool bHttpOK = false;
            bool bHttpStatusAcquired = false;

            int iHeaderLength = 0;
            int iPos = iOffset;
            int iIdx = 0;

            HttpStatusCode http = 0;

            while (iPos < buffer.Length && iHeaderLength < iLength)
            {
                //Get the bytes from the buffer one by one
                iHeaderLength++;
                strHeader += (char)buffer[iPos++];

                //Check HTTP start
                if (!bHttpOK && strHeader.Length > 5)
                {
                    if (!strHeader.StartsWith("HTTP/")) break;
                    else bHttpOK = true;
                }

                //HTTP header end
                if (strHeader.EndsWith(HTTP_HEADER_END) || strHeader.EndsWith("\n\n"))
                {
                    httpStatus = http;
                    break;
                }

                //Line
                if (bHttpOK && (strHeader.EndsWith(EOL) || strHeader.EndsWith("\n")))
                {
                    string strLine = strHeader.Substring(iIdx).Trim();
                    iIdx = strHeader.Length;

                    Match match;
                    if (!bHttpStatusAcquired)
                    {
                        match = _RegexHttpFieldStatus.Match(strLine);
                        if (match.Success)
                        {
                            http = (HttpStatusCode)int.Parse(match.Groups["code"].Value);
                            bHttpStatusAcquired = true;
                        }
                        else
                        {
                            _Logger.Error("[GetHttpResponse] Invalid response status line:" + strLine);
                            break;
                        }
                    }
                    else
                    {
                        //Get header field
                        match = _RegexHttpField.Match(strLine);
                        if (match.Success)
                        {
                            string strKey = match.Groups["key"].Value.Trim();

                            if (strKey == "Set-Cookie")
                            {
                                cookies.Add(uri,GetCookie(match.Groups["value"].Value.Trim()));
                            }
                            else
                            {
                                if (!headersRequest.ContainsKey(strKey))
                                {
                                    string strValue = match.Groups["value"].Value.Trim();
                                    headersRequest.Add(strKey, strValue);
                                }
                            }
                        }
                        else _Logger.Error("[GetHttpResponse] Invalid line:" + strLine);
                    }
                }

                //Check the max length
                if (iHeaderLength > 8192)
                {
                    _Logger.Error("[GetHttpResponse] Error occured: bad HTTP response - header too long.");
                    httpStatus = 0;
                    break;
                }
            }

            iResponseLength = iPos;

            return httpStatus;
        }

        internal static bool GetHttpRequest(byte[] buffer, int iOffset, int iLength,  ref Dictionary<string, string> headersRequest,
            out string strMethod, out string strPath, out int iResponseLength, out string strHttpHeaderData)
        {
            iResponseLength = 0;
            strHttpHeaderData = "";
            strMethod = "";
            strPath = "";

            if (buffer == null) return false;

            if (headersRequest == null) headersRequest = new Dictionary<string, string>();
            else headersRequest.Clear();

            bool bHttpOK = false;
            bool bHttpRqAcquired = false;

            int iHeaderLength = 0;
            int iPos = iOffset;
            int iIdx = 0;


            while (iPos < buffer.Length && iHeaderLength < iLength)
            {
                //Get the bytes from the buffer one by one
                iHeaderLength++;
                strHttpHeaderData += (char)buffer[iPos++];

                //Check HTTP start
                if (!bHttpOK && strHttpHeaderData.Length > 5)
                {
                    if (!strHttpHeaderData.StartsWith("HEAD ") && !strHttpHeaderData.StartsWith("GET ")) break;
                    else bHttpOK = true;
                }

                //HTTP header end
                if (strHttpHeaderData.EndsWith(HTTP_HEADER_END) || strHttpHeaderData.EndsWith("\n\n")) break;

                //Line
                if (bHttpOK && (strHttpHeaderData.EndsWith(EOL) || strHttpHeaderData.EndsWith("\n")))
                {
                    string strLine = strHttpHeaderData.Substring(iIdx).Trim();
                    iIdx = strHttpHeaderData.Length;

                    Match match;
                    if (!bHttpRqAcquired)
                    {
                        match = _RegexHttpFieldMethod.Match(strLine);
                        if (match.Success)
                        {
                            strMethod = match.Groups["type"].Value.Trim();
                            strPath = match.Groups["path"].Value.Trim();
                            bHttpRqAcquired = true;
                        }
                        else
                        {
                            _Logger.Error("[GetHttpResponse] Invalid response status line:" + strLine);
                            return false;
                        }
                    }
                    else
                    {
                        //Get header field
                        match = _RegexHttpField.Match(strLine);
                        if (match.Success)
                        {
                            string strKey = match.Groups["key"].Value.Trim();
                            if (!headersRequest.ContainsKey(strKey))
                            {
                                string strValue = match.Groups["value"].Value.Trim();
                                headersRequest.Add(strKey, strValue);
                            }
                        }
                        else _Logger.Error("[GetHttpResponse] Invalid line:" + strLine);
                    }
                }

                //Check the max length
                if (iHeaderLength > 8192)
                {
                    _Logger.Error("[GetHttpResponse] Error occured: bad HTTP response - header too long.");
                    return false;
                }
            }

            iResponseLength = iPos;

            return true;
        }

        internal static Cookie GetCookie(string strValue)
        {

            Cookie c = new Cookie();

            MatchCollection mc = _RegexHttpFieldCookie.Matches(strValue);
            if (mc.Count > 0)
            {

                for (int i = 0; i < mc.Count; i++)
                {
                    Match m = mc[i];
                    if (i == 0)
                    {
                        c.Name = m.Groups["key"].Value.Trim();
                        c.Value = m.Groups["value"].Value.Trim();
                    }
                    else
                    {
                        switch (m.Groups["key"].Value.Trim())
                        {
                            case "path":
                                c.Path = m.Groups["value"].Value.Trim();
                                break;

                            case "expires":
                                c.Expires = DateTime.Parse((m.Groups["value"].Value));
                                break;
                        }
                    }

                }
            }

            return c;
        }
    }
}
