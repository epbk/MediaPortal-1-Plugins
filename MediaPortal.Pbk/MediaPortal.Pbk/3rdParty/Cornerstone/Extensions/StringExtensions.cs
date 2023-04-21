using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace MediaPortal.Pbk.Cornerstone.Extensions
{
    public static class StringExtensions
    {

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="self">a string</param>
        /// <returns>
        ///   <c>true</c> if the value parameter is null or String.Empty, or if value consists exclusively of white-space characters.
        /// </returns>
        public static bool IsNullOrWhiteSpace(this string self)
        {
            return String.IsNullOrEmpty(self) || self.Trim().Length == 0;
        }

        /// <summary>
        /// Replaces multiple white-spaces with one space
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string TrimWhiteSpace(this string self)
        {
            return Regex.Replace(self, @"\s{2,}", " ").Trim();
        }

        /// <summary>
        /// Translates characters to their base form. ( ë/é/è -> e)
        /// </summary>
        /// <example>
        /// characters: ë, é, è
        /// result: e
        /// </example>
        /// <remarks>
        /// source: http://blogs.msdn.com/michkap/archive/2007/05/14/2629747.aspx
        /// </remarks>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string RemoveDiacritics(this string self)
        {
            string strFormD = self.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            for (int iCh = 0; iCh < strFormD.Length; iCh++)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(strFormD[iCh]);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(strFormD[iCh]);
                }
            }

            return (sb.ToString().Normalize(NormalizationForm.FormC));
        }

        /// <summary>
        /// Converts the string so it can be safely used as a filename.
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static string ToValidFilename(this string self)
        {
            if (String.IsNullOrEmpty(self))
                return string.Empty;

            string strRtFilename = self;
            char[] invalidFileChars = Path.GetInvalidFileNameChars();
            foreach (char invalidFileChar in invalidFileChars)
                strRtFilename = strRtFilename.Replace(invalidFileChar, '_');

            return strRtFilename;
        }

    }
}
