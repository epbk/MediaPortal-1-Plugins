using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.ComponentModel;
using System.Reflection;

namespace MediaPortal.Pbk.Controls.UIEditor
{
    public class EnumValueConverter : EnumConverter
    {
        private Type _TypeEnum;

        public EnumValueConverter(Type type)
            : base(type)
        {
            this._TypeEnum = type;
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type typeDest)
        {
            return typeDest == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type typeDest)
        {
            if (value == null || value is string)
                return value;
            else
                return ToString(value);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type typeScr)
        {
            //return false;
            return typeScr == typeof(string);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            int iResult = 0;

            string[] parts = ((string)value).Split(new string[]{ ", "}, StringSplitOptions.RemoveEmptyEntries);

            string[] names = Enum.GetNames(this._TypeEnum);

            foreach (string strValue in parts)
            {

                foreach (FieldInfo fi in this._TypeEnum.GetFields())
                {
                    DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                    if ((dna != null) && (strValue == dna.Description))
                        iResult |= (int)Convert.ChangeType(Enum.Parse(this._TypeEnum, fi.Name), typeof(int));
                    else if (names.FirstOrDefault(p => p.Equals(strValue, StringComparison.CurrentCultureIgnoreCase)) != null)
                        iResult |= (int)Convert.ChangeType(Enum.Parse(this._TypeEnum, strValue, true), typeof(int));
                }
            }

            return Enum.ToObject(this._TypeEnum, iResult);
        }

        public static string ToString(object value)
        {
            if (value == null || value is string)
                return null;


            StringBuilder sb = new StringBuilder(256);

            int iValue = (int)Convert.ChangeType(value, typeof(int));

            int iResult = 0; //remember already parsed values

            Type t = value.GetType();

            foreach (object o in Enum.GetValues(t))
            {
                int i = (int)Convert.ChangeType(o, typeof(int));

                if ((iResult & i) == 0 && (iValue == i || (iValue & i) != 0))
                {
                    FieldInfo fi = t.GetField(Enum.GetName(t, o));
                    DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                    string str = dna != null ? dna.Description : value.ToString();

                    if (sb.Length > 0)
                        sb.Append(", ");

                    sb.Append(str);

                    iResult |= i;
                }
            }

            string strResult = sb.ToString();

            foreach (object o in Enum.GetValues(t))
            {
                int i = (int)Convert.ChangeType(o, typeof(int));

                if (i == iResult)
                {
                    FieldInfo fi = t.GetField(Enum.GetName(t, o));
                    DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                    string str = dna != null ? dna.Description : value.ToString();

                    if (strResult != str)
                        return str;
                }
            }

            return strResult;
        }
    }
}
