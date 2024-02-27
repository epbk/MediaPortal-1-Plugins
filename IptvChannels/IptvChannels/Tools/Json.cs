using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.IptvChannels.Tools
{
    public class Json
    {
        public static StringBuilder AppendAndValidate(string strValue, StringBuilder sb)
        {
            if (strValue != null)
            {
                for (int i = 0; i < strValue.Length; i++)
                {
                    char c = strValue[i];
                    if (c == '\\' || c == '\"')
                        sb.Append('\\');
                    sb.Append(c);
                }
            }

            return sb;
        }
    }
}
