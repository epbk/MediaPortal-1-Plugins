using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net;
using System.Web;
using System.Reflection;

namespace MediaPortal.Pbk.Net.Http
{
    public class HttpUserWebRequestArguments
    {
        public string UserAgent;
        public string Accept;
        public string AcceptLanguage;
        public string ContentType;
        public string Referer;
        public NameValueCollection Cookies;
        public NameValueCollection Fields;
        public byte[] PostData;

        public string Serialize()
        {
            string strValue;
            StringBuilder sb = new StringBuilder(256);
            StringBuilder sbValue = null;
            FieldInfo[] fields = this.GetType().GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo fi = fields[i];
                if (fi.FieldType == typeof(string))
                    strValue = (string)fi.GetValue(this);
                else if (fi.FieldType == typeof(byte[]))
                    strValue = Pbk.Utils.Tools.PrintDataToHex((byte[])fi.GetValue(this), false, "x2");
                else if (fi.FieldType == typeof(NameValueCollection))
                {
                    NameValueCollection col = (NameValueCollection)fi.GetValue(this);

                    if (col == null || col.Count == 0)
                        continue;

                    if (sbValue == null)
                        sbValue = new StringBuilder(256);
                    else
                        sbValue.Clear();

                    for (int iCol = 0; iCol < col.Count; iCol++)
                    {
                        if (iCol > 0)
                            sbValue.Append('&');
                        sbValue.Append(col.GetKey(iCol));
                        sbValue.Append('=');
                        sbValue.Append(HttpUtility.UrlEncode(col[iCol]));
                    }

                    strValue = sbValue.ToString();
                }
                else
                    continue;

                if (!string.IsNullOrWhiteSpace(strValue))
                {
                    if (sb.Length > 0)
                        sb.Append('&');
                    sb.Append(fi.Name);
                    sb.Append('=');
                    sb.Append(HttpUtility.UrlEncode(strValue));
                }
            }

            return sb.ToString();
        }

        public static HttpUserWebRequestArguments Deserialize(string strPrms)
        {
            HttpUserWebRequestArguments result = new HttpUserWebRequestArguments();
            Dictionary<string, string> prms = Pbk.Utils.Tools.GetUrlParams(strPrms);
            FieldInfo[] fields = typeof(HttpUserWebRequestArguments).GetFields();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo fi = fields[i];
                object o;
                if (prms.TryGetValue(fi.Name, out string strValue) && !string.IsNullOrWhiteSpace(strValue))
                {
                    if (fi.FieldType == typeof(string))
                        o = strValue;
                    else if (fi.FieldType == typeof(byte[]))
                        o = Pbk.Utils.Tools.ParseByteArrayFromHex(strValue);
                    else if (fi.FieldType == typeof(NameValueCollection))
                    {
                        NameValueCollection col = new NameValueCollection();
                        foreach (KeyValuePair<string, string> pair in Pbk.Utils.Tools.GetUrlParams(strValue))
                            col.Add(pair.Key, pair.Value);

                        o = col;
                    }
                    else
                        continue;

                    fi.SetValue(result, o);
                }
            }

            return result;
        }
    }
}
