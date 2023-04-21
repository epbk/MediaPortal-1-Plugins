using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MediaPortal.Pbk.Utils
{
    public static class Json
    {
        private static System.Globalization.CultureInfo _Culture_EN = new System.Globalization.CultureInfo("en-US");

        public static T GetJsonEnum<T>(JToken jToken, string strField, T def)
            where T : struct
        {
            JToken j;
            T result;

            if (typeof(T).IsEnum)
            {
                if ((j = jToken.SelectToken(strField)) != null && j.Type == JTokenType.String &&
                  Enum.TryParse<T>((string)j, true, out result))
                    return result;
            }

            return def;
        }

        public static T GetJsonObject<T>(JToken jToken, string strField, T def)
        {
            JToken j;

            if (typeof(T) == typeof(string))
            {
                if ((j = jToken.SelectToken(strField)) != null)
                {
                    if (j.Type == JTokenType.String)
                        return (T)(object)(string)j;
                    else
                        return (T)(object)j.ToString();
                }
            }
            else if (typeof(T) == typeof(bool))
            {
                if ((j = jToken.SelectToken(strField)) != null)
                {
                    if (j.Type == JTokenType.Boolean)
                        return (T)(object)(bool)j;
                    else if (j.Type == JTokenType.Integer)
                        return (T)(object)(bool)((int)j > 0);
                }
            }
            else if (typeof(T) == typeof(int))
            {
                int i;
                double d;
                if ((j = jToken.SelectToken(strField)) != null && j.Type == JTokenType.Integer)
                    return (T)(object)(int)j;
                else if (j != null && j.Type == JTokenType.String)
                {
                    if (int.TryParse((string)j, out i))
                        return (T)(object)i;
                    else if (double.TryParse((string)j, System.Globalization.NumberStyles.Number, _Culture_EN, out d))
                        return (T)(object)Convert.ToInt32(d);
                }
            }
            else if (typeof(T) == typeof(long))
            {
                long l;
                double d;
                if ((j = jToken.SelectToken(strField)) != null && j.Type == JTokenType.Integer)
                    return (T)(object)(long)j;
                else if (j != null && j.Type == JTokenType.String)
                {
                    if (long.TryParse((string)j, out l))
                        return (T)(object)l;
                    else if (double.TryParse((string)j, System.Globalization.NumberStyles.Number, _Culture_EN, out d))
                        return (T)(object)Convert.ToInt64(d);
                }
            }
            else if (typeof(T) == typeof(DateTime))
            {
                DateTime dt;
                if ((j = jToken.SelectToken(strField)) != null && j.Type == JTokenType.String && DateTime.TryParse((string)j, out dt))
                    return (T)(object)dt;
            }
            else if (typeof(T) == typeof(float))
            {
                float f;
                if ((j = jToken.SelectToken(strField)) != null)
                {
                    if (j.Type == JTokenType.Float)
                        return (T)(object)(float)j;
                    else if (j.Type == JTokenType.Integer)
                        return (T)(object)(float)(int)j;
                    else if ((j.Type == JTokenType.String && float.TryParse((string)j, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("en-US"), out f)))
                        return (T)(object)f;
                }
            }
            else if (typeof(T) == typeof(double))
            {
                double d;
                if ((j = jToken.SelectToken(strField)) != null)
                {
                    if (j.Type == JTokenType.Float)
                        return (T)(object)(double)j;
                    else if (j.Type == JTokenType.Integer)
                        return (T)(object)(double)(int)j;
                    else if ((j.Type == JTokenType.String && double.TryParse((string)j, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("en-US"), out d)))
                        return (T)(object)d;
                }
            }

            return def;;
        }

        public static string PrintJArrayString(JArray array)
        {
            StringBuilder sb = new StringBuilder(256);
            foreach (JToken j in array)
            {
                sb.Append((string)j);
                sb.Append(", ");
            }

            return sb.Length > 0 ? sb.ToString(0, sb.Length - 2) : string.Empty;
        }
    }
}
