using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Extensions
{
    public static class StringExtensions
    {
        // Nonbreaking space is 0xA0 in ISO-8859-1, and when it comes up in UTF-8 it is 0xC2A0.
        // This will look like "Â ". I used the Yen example above because it's easier to see something rather than just a space.

        private static List<string[]> _Replacelist = new List<string[]>
        {
                new string[] { "„", "\"" }, // 0x84
                new string[] { "&#8222;", "\"" }, // 0x84

                new string[] { "‘", "'" }, // 0x91
                new string[] { "’", "'" }, // 0x92

                new string[] { "“", "\"" }, // 0x93
                new string[] { "&#8220;", "\"" }, // 0x93

                new string[] { "”", "\"" }, // 0x94
                new string[] { "&#8221;", "\"" }, // 0x94

                new string[] { "…", "..." }, // 0x85
                new string[] { "&#8230;", "..." },

                new string[] { "–", "-" }, // 0x96
                new string[] { "&#8211;", "-" }, // 0x96

                new string[] { "—", "-" }, // 0x97
                new string[] { "&#8212;", "-" }, // 0x97

                new string[] { "&nbsp", " " }, //
                new string[] { "\t", "" }, //
                //_replacenew string[] { new string(new char[] { (char)0x09 }), "" }, // 

                new string[] { " .", "." }, //
                new string[] { " ,", "," }, //
                new string[] { new string(new char[] { (char)0xc2, (char)0xa0 }), " " }, // 
                new string[] { new string(new char[] { (char)0xa0 }), " " }, // 
        };

        public static string Correct(this string self)
        {
            StringBuilder sb = new StringBuilder(System.Web.HttpUtility.HtmlDecode(self));

            //movie._description = Regex.Replace(movie._description, "\x84", "\"");
            //movie._description = Regex.Replace(movie._description, "\x93", "\"");
            //movie._description = Regex.Replace(movie._description, "\x85", "... ");

            _Replacelist.ForEach(item => sb.Replace(item[0], item[1]));

            int i = 1;
            while (i < sb.Length)
            {
                if (sb[i] == ' ' && sb[i - 1] == ' ')
                    sb.Remove(i, 1);
                else
                    i++;
            }

            return sb.ToString();
        }
    }
}
