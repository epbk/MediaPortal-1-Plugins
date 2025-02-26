using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Globalization;

namespace MediaPortal.IptvChannels.SiteUtils
{
    internal class ValueConverter : ExpandableObjectConverter
    {
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destType)
        {
            if (destType == typeof(string))
            {
                if (value is IptvChannel ch)
                    return ch.Name;

                if (value is IptvChannelCollection)
                    return "Channels";
            }

            return base.ConvertTo(context, culture, value, destType);
        }
    }
}
