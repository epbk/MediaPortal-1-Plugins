using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using NLog;

namespace MediaPortal.IptvChannels.Tools
{
    public static class RegularExpressions
    {
        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();
        private static Dictionary<string, Regex> _Cache = new Dictionary<string, Regex>();
        private static readonly char[] _EscapeChars = new char[] { '\\', '.', '?', '+', '*', ':', '(', ')', '[', ']', '{', '}', '<', '>', '|', '^', '$', '!', '=' };

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static Regex Get(string strRegex)
        {
            if (string.IsNullOrWhiteSpace(strRegex))
                return null;

            Regex regex;
            if (!_Cache.TryGetValue(strRegex, out regex))
            {
                try 
                { 
                    regex = new Regex(strRegex);
                    _Cache.Add(strRegex, regex);
                }
                catch (Exception ex)
                {
                    _Logger.Error("[Get] Regex: '{3}' Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, strRegex);
                    return null;
                }
            }

            return regex;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static void Clear()
        {
            _Cache.Clear();
        }
                
        public static string Escape(string strText)
        {
            StringBuilder sb = new StringBuilder(strText.Length * 2);
            Escape(strText, sb);
            return sb.ToString();
        }
        public static void Escape(string strText, StringBuilder sb)
        {
            for (int i = 0; i < strText.Length; i++)
            {
                char c = strText[i];

                if (_EscapeChars.Count(p => p == c) > 0)
                    sb.Append('\\');

                sb.Append(c);
            }
        }

    }
}
