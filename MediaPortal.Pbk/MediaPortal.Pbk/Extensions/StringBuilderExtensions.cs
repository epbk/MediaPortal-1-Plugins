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

        /// <summary>
        /// Reports the index of the first occurrence of the specified string in this instance.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to seek.</param>
        /// <returns>The zero-based index position of <paramref name="strText"/> if that string is found, or -1 if it is not.</returns>
        public static int IndexOf(this StringBuilder self, string strText)
        {
            return IndexOf(self, strText, 0, false);
        }

        /// <summary>
        /// Reports the index of the first occurrence of the specified string in this instance. The search starts at a specified character position.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to seek.</param>
        /// <param name="iIdxFrom">The search starting position.</param>
        /// <param name="bIgnoreCase">True to ignore case.</param>
        /// <returns>The zero-based index position of <paramref name="strText"/> if that string is found, or -1 if it is not.</returns>
        public static int IndexOf(this StringBuilder self, string strText, int iIdxFrom, bool bIgnoreCase)
        {
            if (strText == null || strText.Length == 0 || self.Length < strText.Length)
                return -1;

            if (iIdxFrom < 0 || iIdxFrom > self.Length)
                throw new ArgumentOutOfRangeException("iIdxFrom");

            int iLen = strText.Length;
            int iOffset = iIdxFrom;
            int iOffsetEnd = self.Length - iLen;
            while (iOffset <= iOffsetEnd)
            {
                for (int iIdx = 0; iIdx < iLen; iIdx++)
                {
                    if (!bIgnoreCase)
                    {
                        if (self[iOffset + iIdx] != strText[iIdx])
                            goto next;
                    }
                    else if (Char.ToLower(self[iOffset + iIdx]) != Char.ToLower(strText[iIdx]))
                        goto next;
                }

                //OK; all chars found
                return iOffset;

            next:
                iOffset++;
            }

            return -1;
        }

        /// <summary>
        /// Reports the index position of the last occurrence of a specified string within this instance.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to seek.</param>
        /// <returns>The zero-based index position of <paramref name="strText"/> if that string is found, or -1 if it is not.</returns>
        public static int LastIndexOf(this StringBuilder self, string strText)
        {
            return LastIndexOf(self, strText, self.Length, false);
        }

        /// <summary>
        /// Reports the index position of the last occurrence of a specified string within this instance.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to seek.</param>
        /// <param name="iIdxFrom">The search starting position.</param>
        /// <param name="bIgnoreCase">True to ignore case.</param>
        /// <returns>The zero-based index position of <paramref name="strText"/> if that string is found, or -1 if it is not.</returns>
        public static int LastIndexOf(this StringBuilder self, string strText, int iIdxFrom, bool bIgnoreCase)
        {
            if (strText == null || strText.Length == 0 || self.Length < strText.Length)
                return -1;

            if (iIdxFrom < 0 || iIdxFrom > self.Length)
                throw new ArgumentOutOfRangeException("iIdxFrom");

            int iLen = strText.Length;
            int iOffset = Math.Min(iIdxFrom, self.Length - iLen);
            while (iOffset >= 0)
            {
                for (int iIdx = 0; iIdx < iLen; iIdx++)
                {
                    if (!bIgnoreCase)
                    {
                        if (self[iOffset + iIdx] != strText[iIdx])
                            goto next;
                    }
                    else if (Char.ToLower(self[iOffset + iIdx]) != Char.ToLower(strText[iIdx]))
                        goto next;
                }

                //OK; all chars found
                return iOffset;

            next:
                iOffset--;
            }

            return -1;
        }

        /// <summary>
        /// Determines whether the beginning of this instance matches the specified <see cref="System.String" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to compare.</param>
        /// <returns>True if <paramref name="strText"/> matches the beginning of this <see cref="System.String" />; otherwise, false.</returns>
        public static bool StartsWith(this StringBuilder self, string strText)
        {
            return StartsWith(self, strText, false);
        }

        /// <summary>
        /// Determines whether the beginning of this instance matches the specified <see cref="System.String" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to compare.</param>
        /// <param name="bIgnoreCase">True to ignore case.</param>
        /// <returns>True if <paramref name="strText"/> matches the beginning of this <see cref="System.String" />; otherwise, false.</returns>
        public static bool StartsWith(this StringBuilder self, string strText, bool bIgnoreCase)
        {
            if (strText == null || strText.Length == 0 || self.Length < strText.Length)
                return false;

            int iLen = strText.Length;
            for (int iIdx = 0; iIdx < iLen; iIdx++)
            {
                if (!bIgnoreCase)
                {
                    if (self[iIdx] != strText[iIdx])
                        return false;
                }
                else if (Char.ToLower(self[iIdx]) != Char.ToLower(strText[iIdx]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the end of this instance matches the specified <see cref="System.String" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to compare.</param>
        /// <returns>True if <paramref name="strText"/> matches the end of this <see cref="System.String" />; otherwise, false.</returns>
        public static bool EndsWith(this StringBuilder self, string strText)
        {
            return EndsWith(self, strText, false);
        }

        /// <summary>
        /// Determines whether the end of this instance matches the specified <see cref="System.String" />.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="strText">The <see cref="System.String" /> to compare.</param>
        /// <param name="bIgnoreCase">True to ignore case.</param>
        /// <returns>True if <paramref name="strText"/> matches the end of this <see cref="System.String" />; otherwise, false.</returns>
        public static bool EndsWith(this StringBuilder self, string strText, bool bIgnoreCase)
        {
            if (strText == null || strText.Length == 0 || self.Length < strText.Length)
                return false;

            int iOffset = self.Length - 1;
            for (int iIdx = strText.Length - 1; iIdx >= 0; iIdx--)
            {
                if (!bIgnoreCase)
                {
                    if (self[iOffset] != strText[iIdx])
                        return false;
                }
                else if (Char.ToLower(self[iOffset]) != Char.ToLower(strText[iIdx]))
                    return false;

                iOffset--;
            }

            return true;
        }

        /// <summary>
        /// Append UTF8 byte buffer.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="buffer">The <see cref="System.Byte" /> to read.</param>
        /// <param name="iIdxFrom">Starting index of the <paramref name="buffer"/> to read from.</param>
        /// <param name="iLength">Length of the <paramref name="buffer"/> to read.</param>
        /// <returns>The current <see cref="System.Text.StringBuilder"/>.</returns>
        public static StringBuilder AppendUTF8Buffer(this StringBuilder self, byte[] buffer, int iIdxFrom, int iLength)
        {
            int iUTF8ToRead = 0;
            int iUTF8Char = 0;
            return AppendUTF8Buffer(self, buffer, iIdxFrom, iLength, ref iUTF8ToRead, ref iUTF8Char);
        }

        /// <summary>
        /// Append UTF8 byte buffer. Supports sequential call.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="buffer">The <see cref="System.Byte" /> to read.</param>
        /// <param name="iIdxFrom">Starting index of the <paramref name="buffer"/> to read from.</param>
        /// <param name="iLength">Length of the <paramref name="buffer"/> to read.</param>
        /// <param name="iUTF8ToRead">Reference of remaining bytes to read. Must be set to zero at the begining.</param>
        /// <param name="iUTF8Char">Reference to current char value. Must be set to zero at the begining.</param>
        /// <returns>The current <see cref="System.Text.StringBuilder"/>.</returns>
        public static StringBuilder AppendUTF8Buffer(this StringBuilder self, byte[] buffer, int iIdxFrom, int iLength, ref int iUTF8ToRead, ref int iUTF8Char)
        {
            if (iUTF8ToRead < 0 || iUTF8ToRead > 3)
                throw new ArgumentException("[AppendUTF8Buffer] Invalid argument 'iUTF8ToRead'.");

            if (buffer == null)
                throw new ArgumentNullException("[AppendUTF8Buffer] Invalid argument 'buffer'.");

            if (iIdxFrom < 0 || iIdxFrom > buffer.Length)
                throw new ArgumentException("[AppendUTF8Buffer] Invalid argument 'iIdxFrom'.");

            int iIdxTo = iIdxFrom + iLength;

            if (iLength < 0 || iIdxTo > buffer.Length)
                throw new ArgumentException("[AppendUTF8Buffer] Invalid argument 'iLength'.");

            while (iIdxFrom < iIdxTo)
            {
                //UTF8 stream decoding
                int iVal = buffer[iIdxFrom++];
                if (iUTF8ToRead == 0)
                {
                    // Range    Byte 1 	    Byte 2 	    Byte 3 	    Byte 4
                    //------------------------------------------------------
                    if (iVal < 0x80)                // 00007F   0xxxxxxx
                        self.Append((char)iVal);
                    else if ((iVal & 0xE0) == 0xC0) // 0007FF   110xxxxx    10xxxxxx
                    {
                        iUTF8Char = (iVal & 0x1F);
                        iUTF8ToRead = 1;
                    }
                    else if ((iVal & 0xF0) == 0xE0) // 00FFFF   1110xxxx	10xxxxxx	10xxxxxx
                    {
                        iUTF8Char = (iVal & 0x0F);
                        iUTF8ToRead = 2;
                    }
                    else if ((iVal & 0xF8) == 0xF0) // 10FFFF   11110xxx	10xxxxxx	10xxxxxx	10xxxxxx
                    {
                        iUTF8Char = (iVal & 0x7);
                        iUTF8ToRead = 3;
                    }
                    else
                        throw new Exception("[AppendUTF8Buffer] Invalid UTF8 encoding.");
                }
                else if ((iVal & 0xC0) == 0x80) //10xxxxxx
                {
                    iUTF8Char <<= 6;
                    iUTF8Char |= (iVal & 0x3F);
                    if (--iUTF8ToRead == 0)
                    {
                        if (iUTF8Char >= 0x10000)
                        {
                            //surrogate code
                            iUTF8Char -= 0x10000;
                            self.Append((char)((iUTF8Char >> 10) | 0xd800));
                            self.Append((char)((iUTF8Char & 0x03FF) | 0xdc00));
                        }
                        else
                            self.Append((char)iUTF8Char);
                    }
                }
                else
                    throw new Exception("[AppendUTF8Buffer] Invalid UTF8 encoding.");
            }

            return self;
        }


        /// <summary>
        /// Encodes current <see cref="System.Text.StringBuilder" /> to UTF8 and stores the result in the data argument from position given by iSize.
        /// </summary>
        /// <param name="self">The current <see cref="System.Text.StringBuilder" /> instance.</param>
        /// <param name="data">The byte array for the UTF8 destination bytes.</param>
        /// <param name="iSize">Cuurent position of data argument. Advanced after conversion.</param>
        /// <returns>The current <see cref="System.Text.StringBuilder" />.</returns>
        public static StringBuilder GetUTF8Bytes(this StringBuilder self, byte[] data, ref int iSize)
        {
            for (int iIdxSb = 0; iIdxSb < self.Length; iIdxSb++)
            {
                if (iSize >= data.Length)
                    throw new IndexOutOfRangeException("GetUTF8Bytes(): data buffer out of range");

                int iValue = self[iIdxSb];

                //surrogate code check
                if (iValue >= 0xd800 && iValue <= 0xdbff && (iIdxSb + 1) < self.Length && self[iIdxSb + 1] >= 0xdc00 && self[iIdxSb + 1] <= 0xdfff)
                    iValue = 0x10000 + (((iValue - 0xd800) << 10) | (self[++iIdxSb] - 0xdc00));

                if (iValue <= 0x7F)
                    data[iSize++] = (byte)iValue;
                else
                {
                    if (iValue <= 0x07FF)
                        data[iSize++] = (byte)((iValue >> 6) | 0xC0);
                    else
                    {
                        if (iValue <= 0xFFFF)
                            data[iSize++] = (byte)((iValue >> 12) | 0xE0);
                        else if (iValue <= 0x10FFFF)
                        {
                            data[iSize++] = (byte)((iValue >> 18) | 0xF0);
                            data[iSize++] = (byte)((iValue >> 12) & 0x3F | 0x80);
                        }
                        else
                            throw new OverflowException("GetUTF8Bytes(): char is too big");

                        data[iSize++] = (byte)((iValue >> 6) & 0x3F | 0x80);
                    }

                    data[iSize++] = (byte)((iValue & 0x3F) | 0x80);
                }
            }

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
