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
using MediaPortal.Pbk.Logging;
using System.Web;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Reflection;


namespace MediaPortal.Pbk.Net.Http
{
    public class WebTools
    {
        static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        static WebTools()
        {
            Logging.Log.Init();
        }

        public static bool CheckWebData(string strUrl)
        {
            HttpWebRequest request = null;
            HttpWebResponse response = null;
            try
            {
                if (Log.LogLevel <= LogLevel.Debug) _Logger.Debug("[CheckWebData] URL: {0}", strUrl);
                request = WebRequest.Create(strUrl) as HttpWebRequest;
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
                    request = null;
            }
        }

        public static XmlDocument LoadHtml(string strContent)
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
                txreader = new StringReader(strContent);
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
                if (sgmlReader != null) 
                    sgmlReader.Close();

                if (xmltxtrd != null)
                    xmltxtrd.Close();

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

        public static XmlDocument LoadXmlAndRemoveNamespace(string strContent)
        {
            TextReader txreader = null;
            XmlTextReader xmltxtrd = null;
            try
            {
                txreader = new StringReader(strContent);
                xmltxtrd = new XmlTextReader(txreader);
                xmltxtrd.Namespaces = false;
                XmlDocument xmldoc = new XmlDocument();
                xmldoc.Load(xmltxtrd);
                return xmldoc;

            }
            catch (Exception ex)
            {
                _Logger.Error(string.Format("[LoadXmlAndRemoveNamespace] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace));
                return null;
            }
            finally
            {
                if (xmltxtrd != null)
                    xmltxtrd.Close();

                if (txreader != null)
                {
                    txreader.Close();
                    txreader.Dispose();
                    txreader = null;
                }
            }
        }

        public static List<Cookie> GetAllCookies(CookieContainer cookies)
        {
            List<Cookie> result = new List<Cookie>();

            Hashtable domains = (Hashtable)cookies.GetType().InvokeMember("m_domainTable", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, cookies, new object[] { });

            foreach (var domain in domains.Keys)
            {
                String strDomain = (string)domain;
                if (strDomain[0] == '.')
                    strDomain = strDomain.Substring(1);

                SortedList m_list = (SortedList)domains[domain].GetType().InvokeMember("m_list", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, domains[domain], new object[] { });
                foreach (var item in m_list.Keys)
                {
                    ArrayList listCookie = (ArrayList)m_list[item].GetType().InvokeMember("m_list", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance, null, m_list[item], new object[] { });

                    foreach (Cookie cookie in listCookie)
                        result.Add(cookie);
                }
            }

            return result;
        }

        public static IPEndPoint ParseIPEndPoint(string endPoint)
        {
            string[] ep = endPoint.Split(':');

            if (ep.Length != 2)
                return null;

            IPAddress ip;

            if (!IPAddress.TryParse(ep[0], out ip)) 
                return null;

            int iPort;
            if (!int.TryParse(ep[1], System.Globalization.NumberStyles.None, System.Globalization.NumberFormatInfo.CurrentInfo, out iPort)) 
                return null;

            return new IPEndPoint(ip, iPort);
        }

        //Callback used to validate the certificate in an SSL conversation
        public static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors policyErrors)
        {
            bool bResult = false;
            if (cert.Subject.ToUpper().Contains("YourServerName"))
                bResult = true;

            return bResult;
        }


        public static byte[] Base64UrlDecode(string strArg)
        {
            string str = strArg;
            str = str.Replace('-', '+'); // 62nd char of encoding
            str = str.Replace('_', '/'); // 63rd char of encoding
            switch (str.Length % 4) // Pad with trailing '='s
            {
                case 0:
                    break; // No pad chars in this case

                case 2:
                    str += "=="; break; // Two pad chars

                case 3:
                    str += "="; break; // One pad char

                default:
                    throw new System.Exception("Illegal base64url string!");
            }
            return Convert.FromBase64String(str); // Standard base64 decoder
        }

        public static string Base64UrlEncode(byte[] arg)
        {
            string str = Convert.ToBase64String(arg);
            str = str.Replace('+', '-'); // 62nd char of encoding
            str = str.Replace('/', '_'); // 63rd char of encoding
            str = str.TrimEnd('=');

            return str;
        }

        public static bool IsLocalIpAddress(string strHost)
        {
            try
            {
                // get host IP addresses
                IPAddress[] hostIPs = System.Net.Dns.GetHostAddresses(strHost);

                // get local IP addresses
                IPAddress[] localIPs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());

                // test if any host IP equals to any local IP or to localhost
                foreach (IPAddress hostIP in hostIPs)
                {
                    // is localhost
                    if (IPAddress.IsLoopback(hostIP))
                        return true;

                    // is local address
                    foreach (IPAddress localIP in localIPs)
                    {
                        if (hostIP.Equals(localIP))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static IPAddress GetLocalIpAddress()
        {
            try
            {
                // get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                if (localIPs != null)
                {
                    foreach (IPAddress ip in localIPs)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            return ip;
                    }
                }


            }
            catch { }
            return null;
        }


    }
}
