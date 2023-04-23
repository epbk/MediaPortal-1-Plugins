using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace MediaPortal.Pbk.Utils
{
    public static class Enums
    {
        public static string[] GetEnumNames(Type tEnum)
        {
            FieldInfo[] fields = tEnum.GetFields();
            string[] result = tEnum.GetEnumNames();

            for (int iN = 0; iN < result.Length; iN++)
            {
                string strName = result[iN];

                for (int iF = 0; iF < fields.Length; iF++)
                {
                    FieldInfo fi = fields[iF];

                    if (fi.Name.Equals(strName))
                    {
                        DescriptionAttribute attr = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                        if (attr != null)
                        {
                            result[iN] = attr.Description;
                            break;
                        }
                    }
                }
            }

            return result;
        }
    }
}
