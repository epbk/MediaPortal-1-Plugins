using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Pbk.Extensions
{
    /// <summary>
    /// String Builder extensions
    /// </summary>
    public static class StringBuilderExtensions
    {
        private static readonly char[] _WhiteSpaceChars = new char[] { ' ', '\t', '\r', '\n', '\f', (char)0xA0 };

        public static StringBuilder Remove(this StringBuilder self, char c)
        {
            int iIdx = 0;

            while (iIdx < self.Length)
            {
                if (self[iIdx] == c)
                    self.Remove(iIdx, 1);
                else
                    iIdx++;
            }

            return self;
        }

        /// <summary>
        /// Removes all leading and trailing white-space characters from the current <see cref="System.Text.StringBuilder" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <returns>The current <see cref="System.Text.StringBuilder" /> that remains after all white-space characters are removed from the start and end of the current <see cref="System.Text.StringBuilder" />.</returns>
        public static StringBuilder Trim(this StringBuilder self)
        {
            int iLength = self.Length;

            while (iLength > 0 && isWhiteSpace(self[iLength - 1]))
                iLength--;

            if (iLength != self.Length)
                self.Length = iLength;

            iLength = 0;

            while (iLength < self.Length && isWhiteSpace(self[iLength]))
                iLength++;

            if (iLength > 0)
                self.Remove(0, iLength);

            return self;
        }

        /// <summary>
        /// Removes all leading and trailing characters from the current <see cref="System.Text.StringBuilder" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="c">Character to remove.</param>
        /// <returns>The current <see cref="System.Text.StringBuilder" /> that remains after all white-space characters are removed from the start and end of the current <see cref="System.Text.StringBuilder" />.</returns>
        public static StringBuilder Trim(this StringBuilder self, char c)
        {
            int iLength = self.Length;

            while (iLength > 0 && self[iLength - 1] == c)
                iLength--;

            if (iLength != self.Length)
                self.Length = iLength;

            iLength = 0;

            while (iLength < self.Length && self[iLength] == c)
                iLength++;

            if (iLength > 0)
                self.Remove(0, iLength);

            return self;
        }

        private static bool isWhiteSpace(char c)
        {
            for (int i = 0; i < _WhiteSpaceChars.Length; i++)
            {
                if (_WhiteSpaceChars[i] == c)
                    return true;
            }

            return false;
        }
    }
}
