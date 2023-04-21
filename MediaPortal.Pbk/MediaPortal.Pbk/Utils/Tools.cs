using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace MediaPortal.Pbk.Utils
{
    public class Tools
    {
        private static List<string[]> _Replacelist = new List<string[]> { };

        private static readonly char[,] _CharsDia = new char[,] {
        { 'ä', 'a' }, { 'Ä', 'A' }, { 'á', 'a' }, { 'Á', 'A' }, { 'à', 'a' }, { 'À', 'A' }, { 'ã', 'a' }, { 'Ã', 'A' }, { 'â', 'a' }, { 'Â', 'A' },
        { 'č', 'c' }, { 'Č', 'C' }, { 'ć', 'c' }, { 'Ć', 'C' },
        { 'ď', 'd' }, { 'Ď', 'D' },
        { 'ě', 'e' }, { 'Ě', 'E' }, { 'é', 'e' }, { 'É', 'E' }, { 'ë', 'e' }, { 'Ë', 'E' }, { 'è', 'e' }, { 'È', 'E' }, { 'ê', 'e' }, { 'Ê', 'E' },
        { 'í', 'i' }, { 'Í', 'I' }, { 'ï', 'i' }, { 'Ï', 'I' }, { 'ì', 'i' }, { 'Ì', 'I' }, { 'î', 'i' }, { 'Î', 'I' }, { 'ľ', 'l' }, { 'Ľ', 'L' }, { 'ĺ', 'l' },
        { 'Ĺ', 'L' },
        { 'ń', 'n' }, { 'Ń', 'N' }, { 'ň', 'n' }, { 'Ň', 'N' }, { 'ñ', 'n' }, { 'Ñ', 'N' },
        { 'ó', 'o' }, { 'Ó', 'O' }, { 'ö', 'o' }, { 'Ö', 'O' }, { 'ô', 'o' }, { 'Ô', 'O' }, { 'ò', 'o' }, { 'Ò', 'O' }, { 'õ', 'o' }, { 'Õ', 'O' }, { 'ő', 'o' }, { 'Ő', 'O' },
        { 'ř', 'r' }, { 'Ř', 'R' }, { 'ŕ', 'r' }, { 'Ŕ', 'R' },
        { 'š', 's' }, { 'Š', 'S' }, { 'ś', 's' }, { 'Ś', 'S' },
        { 'ť', 't' }, { 'Ť', 'T' },
        { 'ú', 'u' }, { 'Ú', 'U' }, { 'ů', 'u' }, { 'Ů', 'U' }, { 'ü', 'u' }, { 'Ü', 'U' }, { 'ù', 'u' }, { 'Ù', 'U' }, { 'ũ', 'u' }, { 'Ũ', 'U' }, { 'û', 'u' }, { 'Û', 'U' },
        { 'ý', 'y' }, { 'Ý', 'Y' },
        { 'ž', 'z' }, { 'Ž', 'Z' }, { 'ź', 'z' }, { 'Ź', 'Z' } };

        private static readonly char[] _Skip = new char[] { '-', ':', ',' };

        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }
        public static UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                   (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public static string PrintFileSize(long lValue)
        {
            return PrintFileSize(lValue, "0", System.Globalization.CultureInfo.CurrentCulture);
        }
        public static string PrintFileSize(long lValue, string strFormat, System.Globalization.CultureInfo ci)
        {
            if (lValue < 0)
                return "";

            string strSuffix, strValue;

            if (lValue < 1024)
            {
                strValue = lValue.ToString();
                strSuffix = " B";
            }
            else if (lValue < 1048576)
            {
                strValue = ((double)lValue / 1024).ToString(strFormat, ci);
                strSuffix = " KB";
            }
            else if (lValue < 1073741824)
            {
                strValue = ((double)lValue / 1048576).ToString(strFormat, ci);
                strSuffix = " MB";
            }
            else
            {
                strValue = ((double)lValue / 1073741824).ToString("0.00", ci);
                strSuffix = " GB";
            }

            return strValue + strSuffix;
        }

        public static string PrintBitrate(int iValue)
        {
            return PrintBitrate(iValue, System.Globalization.CultureInfo.CurrentCulture);
        }
        public static string PrintBitrate(int iValue, System.Globalization.CultureInfo ci)
        {
            if (iValue > 1000000)
                return ((float)iValue / 1000000).ToString("0.00", ci) + " Mbit/s";
            else if (iValue > 1000)
                return ((float)iValue / 1000).ToString("0.00", ci) + " kbit/s";
            else
                return iValue.ToString() + " b/s";
        }

        public static string PrintDataToHex(byte[] data, bool bSpace = true, string strFormat = "X2" )
        {
            if (data == null || data.Length < 1)
                return string.Empty;

            StringBuilder sb = new StringBuilder(1024);
            foreach (byte uc in data)
            {
                if (bSpace && sb.Length > 0)
                    sb.Append(' ');

                sb.Append(uc.ToString(strFormat));
            }

            return sb.ToString();
        }

        public static System.Drawing.Image GetImageFromResources(string strPath)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(strPath))
                {
                    return System.Drawing.Image.FromStream(stream);
                }

            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, string> GetUrlParams(string strQuery)
        {
            return GetUrlParams(strQuery, true);
        }
        public static Dictionary<string, string> GetUrlParams(string strQuery, bool bUrlDecode)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            GetUrlParams(result, strQuery, bUrlDecode);

            return result;
        }
        public static void GetUrlParams(Dictionary<string, string> prms, string strQuery, bool bUrlDecode)
        {
            prms.Clear();

            if (!string.IsNullOrWhiteSpace(strQuery))
            {
                string[] items;

                int iIdx = strQuery.IndexOf('?');
                if (iIdx >= 0)
                    items = strQuery.Substring(iIdx + 1).TrimEnd().Split('&');
                else
                    items = strQuery.Trim().Split('&');

                foreach (string strItem in items)
                {
                    string[] parts = strItem.Split('=');
                    string s;
                    if (parts.Length == 2 && !prms.TryGetValue(parts[0], out s))
                        prms.Add(parts[0], bUrlDecode ? System.Web.HttpUtility.UrlDecode(parts[1]) : parts[1]);
                }
            }
        }

        public static string SerializeUrlParams(Dictionary<string, string> prms)
        {
            return SerializeUrlParams(prms, true);
        }
        public static string SerializeUrlParams(Dictionary<string, string> prms, bool bUrlEncode)
        {
            if (prms == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder(256);

            foreach (KeyValuePair<string, string> pair in prms)
            {
                if (sb.Length > 0)
                    sb.Append('&');

                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(bUrlEncode ? System.Web.HttpUtility.UrlEncode(pair.Value) : pair.Value);
            }

            return sb.ToString();
        }

        public static char SwapDiacritics(char c)
        {
            for (int i = 0; i < _CharsDia.GetLength(0); i++)
            {
                if (_CharsDia[i, 0] == c)
                    return _CharsDia[i, 1];
            }

            return c;
        }
        public static string SwapDiacritics(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput))
                return string.Empty;

            StringBuilder sb = new StringBuilder(strInput.Length * 2);

            foreach (char c in strInput)
            {
                for (int i = 0; i < _CharsDia.GetLength(0); i++)
                {
                    if (_CharsDia[i, 0] == c)
                    {
                        sb.Append(_CharsDia[i, 1]);
                        goto next;
                    }
                }

                sb.Append(c);

            next:
                continue;
            }

            return sb.ToString();

        }

        public static string GetSafeFilename(string strFilename, bool bReplaceByDot)
        {
            return GetSafeFilename(strFilename, _Skip, bReplaceByDot);
        }
        public static string GetSafeFilename(string strFilename, char[] skip, bool bReplaceByDot)
        {
            string str = strFilename.Trim();
            StringBuilder sb = new StringBuilder(str.Length);
            char[] charsInvalid = Path.GetInvalidFileNameChars();
            foreach (char c in str)
            {
                if ((char.IsWhiteSpace(c) || skip.Contains(c)) && bReplaceByDot && sb.Length > 0)
                {
                    if (sb[sb.Length - 1] != '.') //avoid double dot
                        sb.Append('.');
                }
                else if (c == ':' && !bReplaceByDot)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');

                    sb.Append('-');
                }
                else if (c == '.' && sb.Length > 0 && sb[sb.Length - 1] == '.')
                    continue; //avoid double dot
                else if (charsInvalid.Contains(c))
                    sb.Append('_'); //invalid char
                else
                    sb.Append(c); //pass
            }

            //Final string
            return sb.ToString();
        }

        public static int ConvertRomanNumtoInt(string strRomanValue)
        {
            if (string.IsNullOrWhiteSpace(strRomanValue))
                return -1;

            Dictionary<string, int> numbers = new Dictionary<string, int>
            {
                {"M", 1000},
                {"CM", 900},
                {"D", 500},
                {"CD", 400},
                {"C", 100},
                {"XC", 90},
                {"L", 50},
                {"XL", 40},
                {"X", 10},
                {"IX", 9},
                {"V", 5},
                {"IV", 4},
                {"I", 1}
            };
            int iResult = 0;
            int iIdx = 0;
            foreach (KeyValuePair<string, int> pair in numbers)
            {
                while (strRomanValue.IndexOf(pair.Key, iIdx) == iIdx)
                {
                    iResult += pair.Value;
                    iIdx += pair.Key.Length;
                }
            }
            return iResult > 0 ? iResult : -1;
        }

        public static byte[] ParseByteArrayFromHex(string strValue)
        {
            if (strValue == null || (strValue.Length % 2) != 0)
                throw new ArgumentException("Invalid string value.");

            byte[] result = new byte[strValue.Length / 2];
            int iVal;
            int iIdx = 0;
            for (int i = 0; i < strValue.Length; i++)
            {
                char c = strValue[i];
                if (c >= '0' && c <= '9')
                    iVal = (int)c - 48;
                else if (c >= 'a' && c <= 'f')
                    iVal = (int)c - 87;
                else if (c >= 'A' && c <= 'F')
                    iVal = (int)c - 55;
                else
                    throw new ArgumentException("Invalid character.");

                if ((i & 1) == 0)
                    result[iIdx] = (byte)(iVal << 4);
                else
                    result[iIdx++] |= (byte)iVal;
            }

            return result;
        }

        public static string StringCorrect(string strText)
        {

            // Nonbreaking space is 0xA0 in ISO-8859-1, and when it comes up in UTF-8 it is 0xC2A0.
            // This will look like "Â ". I used the Yen example above because it's easier to see something rather than just a space.

            if (_Replacelist.Count == 0)
            {
                _Replacelist.Add(new string[] { "„", "\"" }); // 0x84
                _Replacelist.Add(new string[] { "&#8222;", "\"" }); // 0x84

                _Replacelist.Add(new string[] { "‘", "'" }); // 0x91
                _Replacelist.Add(new string[] { "’", "'" }); // 0x92

                _Replacelist.Add(new string[] { "“", "\"" }); // 0x93
                _Replacelist.Add(new string[] { "&#8220;", "\"" }); // 0x93

                _Replacelist.Add(new string[] { "”", "\"" }); // 0x94
                _Replacelist.Add(new string[] { "&#8221;", "\"" }); // 0x94

                _Replacelist.Add(new string[] { "…", "..." }); // 0x85
                _Replacelist.Add(new string[] { "&#8230;", "..." });

                _Replacelist.Add(new string[] { "–", "-" }); // 0x96
                _Replacelist.Add(new string[] { "&#8211;", "-" }); // 0x96

                _Replacelist.Add(new string[] { "—", "-" }); // 0x97
                _Replacelist.Add(new string[] { "&#8212;", "-" }); // 0x97

                _Replacelist.Add(new string[] { "&nbsp", " " }); //
                _Replacelist.Add(new string[] { "\t", "" }); //
                //_replacelist.Add(new string[] { new string(new char[] { (char)0x09 }), "" }); // 



                _Replacelist.Add(new string[] { " .", "." }); //
                _Replacelist.Add(new string[] { " ,", "," }); //
                _Replacelist.Add(new string[] { new string(new char[] { (char)0xc2, (char)0xa0 }), " " }); // 
                _Replacelist.Add(new string[] { new string(new char[] { (char)0xa0 }), " " }); // 

                //if (Log.Initialized)
                //{
                //    foreach (string[] item in _replacelist) _logger.Debug(string.Format("[StringCorrect] Added to replace: {0} for {1}", item[0], item[1]));
                //}

            }

            StringBuilder sb = new StringBuilder(strText.Length * 2);
            sb.Append(System.Web.HttpUtility.HtmlDecode(strText));

            foreach (string[] item in _Replacelist)
                sb.Replace(item[0], item[1]);

            int i = 1;
            while (i < sb.Length)
            {
                if (sb[i - 1] == ' ' && sb[i] == ' ')
                    sb.Remove(i, 1);
                else
                    i++;
            }

            return sb.ToString();
        }

       
    }
}
