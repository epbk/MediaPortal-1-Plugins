using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;

namespace MediaPortal.IptvChannels.Controls.UIEditor
{
    public class FileSizeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            try
            {
                string strValue = ((string)value).TrimEnd().Replace(',', '.');

                System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");

                long l;
                if (strValue.EndsWith("kb", StringComparison.CurrentCultureIgnoreCase))
                    l = (long)double.Parse(strValue.Substring(0, strValue.Length - 2), ciEn) * 1024;
                else if (strValue.EndsWith("mb", StringComparison.CurrentCultureIgnoreCase))
                    l = (long)double.Parse(strValue.Substring(0, strValue.Length - 2), ciEn) * (1024 * 1024);
                else if (strValue.EndsWith("gb", StringComparison.CurrentCultureIgnoreCase))
                    l = (long)double.Parse(strValue.Substring(0, strValue.Length - 2), ciEn) * (1024 * 1024 * 1024);
                else if (strValue.EndsWith("b", StringComparison.CurrentCultureIgnoreCase))
                    l = long.Parse(strValue.Substring(0, strValue.Length - 1), ciEn);
                else
                    l = long.Parse(strValue);

                if (l <= int.MaxValue)
                    return (int)l;
                else
                    return l;
            }
            catch
            {
                throw new ArgumentException("Invalid value.");
            }
            
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (value is int)
                return Pbk.Utils.Tools.PrintFileSize((long)(int)value);

            return Pbk.Utils.Tools.PrintFileSize((long)value);
        }
    }
}
