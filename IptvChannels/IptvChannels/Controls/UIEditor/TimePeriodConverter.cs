using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;

namespace MediaPortal.IptvChannels.Controls.UIEditor
{
    public class TimePeriodConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            string strValue = ((string)value).Replace(',', '.');

            System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");

            long lResult = 0;
            float fMul;
            int iSegmentStart = -1;
            bool bIsValue = false;
            bool bIsNeg = false;
            int iFlagsMul = 0;
            float f = 0;
            for (int i = 0; i <= strValue.Length; i++)
            {
                char c = i < strValue.Length ? strValue[i] : '\0';
                if (c == ' ')
                    continue; //skip space
                else if (c == '-')
                {
                    if (bIsNeg || iSegmentStart >= 0)
                        goto error; //sign already specified or not at begining

                    bIsNeg = true;
                }
                else if ((c >= '0' && c <= '9') || c == '.' || (c == '\0' && iSegmentStart >= 0 && !bIsValue))
                {
                    if (iSegmentStart < 0)
                    {
                        //First segment: value
                        iSegmentStart = i;
                        bIsValue = true;
                    }
                    else if (bIsValue)
                        continue; //value continues
                    else
                    {
                        //Transition from text to value

                        //Get current segment as text
                        string strMul = strValue.Substring(iSegmentStart, i - iSegmentStart).ToLowerInvariant().TrimEnd();
                        switch (strMul)
                        {
                            //weeks
                            case "w":
                                if (iFlagsMul >= 0x01)
                                    goto error;

                                iFlagsMul |= 0x01;

                                fMul = 604800000F;
                                break;

                            //days
                            case "d":
                                if (iFlagsMul >= 0x02)
                                    goto error;

                                iFlagsMul |= 0x02;

                                fMul = 86400000F;
                                break;

                            //hours
                            case "h":
                                if (iFlagsMul >= 0x04)
                                    goto error;

                                iFlagsMul |= 0x04;

                                fMul = 3600000F;
                                break;

                            //minutes
                            case "m":
                                if (iFlagsMul >= 0x08)
                                    goto error;

                                iFlagsMul |= 0x08;

                                fMul = 60000F;
                                break;

                            //seconds
                            case "s":
                                if (iFlagsMul >= 0x10)
                                    goto error;

                                iFlagsMul |= 0x10;

                                fMul = 1000F;
                                break;

                            //milisecs
                            case "ms":
                                if (iFlagsMul >= 0x20)
                                    goto error;

                                iFlagsMul |= 0x20;

                                fMul = 1F;
                                break;

                            default:
                                goto error;
                        }

                        //Update the result value
                        lResult += (long)(f * fMul);

                        //Next segment: value
                        iSegmentStart = i;
                        bIsValue = true;

                        //Termination
                        if (c == '\0')
                            break;
                    }
                }
                else
                {
                    if (iSegmentStart < 0)
                    {
                        //First segment: text
                        iSegmentStart = i;
                        bIsValue = false;
                    }
                    else if (!bIsValue)
                        continue; //text continues
                    else
                    {
                        //Transition from value to text

                        //Parse current segment as value
                        if (!float.TryParse(strValue.Substring(iSegmentStart, i - iSegmentStart), System.Globalization.NumberStyles.Number, ciEn, out f))
                            goto error;

                        if (c == '\0') //Termination
                        {
                            lResult += (long)f;
                            break;
                        }

                        //Next segment: text
                        iSegmentStart = i;
                        bIsValue = false;
                    }
                }
            }

            if (bIsNeg)
                lResult *= -1;

            if (lResult >= int.MinValue && lResult <= int.MaxValue)
                return (int)lResult;
            else
                return lResult;

        error:
            throw new ArgumentException("Invalid value.");

        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            long lValue;
            StringBuilder sb = new StringBuilder(32);

            if (value is int)
                lValue = (long)(int)value;
            else
                lValue = (long)value;

            if (lValue < 0)
            {
                sb.Append('-');
                lValue *= -1;
            }

            if (lValue < 1000)
            {
                sb.Append(lValue);
                sb.Append("ms");
            }
            else
            {
                //Weeks
                if (print(ref lValue, sb, 604800000, 'w', "0"))
                    goto ext;

                //Days
                if (print(ref lValue, sb, 86400000, 'd', "0"))
                    goto ext;

                //Hours
                if (print(ref lValue, sb, 3600000, 'h', "00"))
                    goto ext;

                //Minutes
                if (print(ref lValue, sb, 60000, 'm', "00"))
                    goto ext;

                //Seconds
                if (print(ref lValue, sb, 1000, 's', "00"))
                    goto ext;
                
                //Millisec
                sb.Append(' ');
                sb.Append(lValue.ToString("000"));
                sb.Append("ms");
            }

        ext:
            return sb.ToString();
        }

        /// <summary>
        /// Prints specific part of the value based on reference into string builder.
        /// </summary>
        /// <param name="lValue">Value to print.</param>
        /// <param name="sb">String builder to be used for the output.</param>
        /// <param name="lRef">Reference value.</param>
        /// <param name="cRef">Reference symbol.</param>
        /// <param name="strValueFormat">Format of the value to be printed.</param>
        /// <returns>True if reminder is zero.</returns>
        private static bool print(ref long lValue, StringBuilder sb, long lRef, char cRef, string strValueFormat)
        {
            if (lValue >= lRef)
            {
                long lReminder = lValue % lRef;
                lValue /= lRef;
                if (sb.Length > 1)
                    sb.Append(lValue.ToString(strValueFormat));
                else
                    sb.Append(lValue);

                sb.Append(cRef);

                lValue = lReminder;
                if (lReminder == 0)
                    return true;
                else
                    sb.Append(' ');
            }
            else if (sb.Length > 1)
            {
                sb.Append(strValueFormat);
                sb.Append(cRef);
                sb.Append(' ');
            }

            return false;
        }
    }
}
