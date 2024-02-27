using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace MediaPortal.IptvChannels.Tools
{
    public class Utils
    {
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

    }
}
