using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace MediaPortal.Pbk.Cornerstone.Extensions
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Creates a list based on a JSON Array
        /// </summary>
        public static IEnumerable<T> FromJsonArray<T>(this string strJsonArray)
        {
            if (string.IsNullOrEmpty(strJsonArray))
                return new List<T>();

            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(strJsonArray)))
                {
                    var ser = new DataContractJsonSerializer(typeof(IEnumerable<T>));
                    var result = (IEnumerable<T>)ser.ReadObject(ms);

                    if (result == null)
                        return new List<T>();
                    else
                        return result;
                }
            }
            catch (Exception)
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// Creates an object from JSON
        /// </summary>
        public static T FromJson<T>(this string strJson)
        {
            if (string.IsNullOrEmpty(strJson)) 
                return default(T);

            try
            {
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(strJson.ToCharArray())))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    return (T)ser.ReadObject(ms);
                }
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Creates an object from JSON (this works with Dictionary types)
        /// Note: this does not use DataContracts from T
        /// </summary>
        public static T FromJsonDictionary<T>(this string strJson)
        {
            if (string.IsNullOrEmpty(strJson)) return default(T);

            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                return (T)ser.Deserialize<T>(strJson);
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Turns an object into JSON
        /// </summary>
        public static string ToJson(this object obj)
        {
            if (obj == null) 
                return string.Empty;

            using (var ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(obj.GetType());
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}