using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Globalization;

namespace MediaPortal.IptvChannels.Controls.UIEditor
{
    public class StreamingEngineConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            try
            {
                return Enum.Parse(typeof(Proxy.StreamingEngineEnum), (string)value, true);
            }
            catch
            {
                throw new ArgumentException("Invalid value.");
            }

        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            //returns true to indicate the TypeConverter can and will supply the values

            if (context.Instance is Database.dbSettings)
                return true;
            else
                return base.GetStandardValuesSupported();
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true; // indicates that, no the user can't opt to type in their own value.
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            string[] names = Enum.GetNames(typeof(Proxy.StreamingEngineEnum))
                               .Where(x => !x.Equals("default", StringComparison.OrdinalIgnoreCase)).ToArray();

            return new StandardValuesCollection(names); //filters the list and returns the new subset.
        }
    }
}
