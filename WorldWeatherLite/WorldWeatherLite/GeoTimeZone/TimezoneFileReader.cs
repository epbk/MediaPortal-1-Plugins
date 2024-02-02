using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace MediaPortal.Plugins.WorldWeatherLite.GeoTimeZone
{

    internal static class TimezoneFileReader
    {
        private const int LineLength = 8;
        private const int LineEndLength = 1;

        private static readonly Lazy<MemoryStream> LazyData = new Lazy<MemoryStream>(LoadData);
        private static readonly Lazy<int> LazyCount = new Lazy<int>(GetCount);

        private static MemoryStream LoadData()
        {
            Assembly assembly = typeof(TimezoneFileReader).Assembly;
            using (Stream compressedStream = assembly.GetManifestResourceStream("MediaPortal.Plugins.WorldWeatherLite.GeoTimeZone.TZ.dat.gz"))
            {
                using (GZipStream stream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    MemoryStream ms = new MemoryStream();
                    stream.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms;
                }
            }
        }

        private static int GetCount()
        {
            return (int)(LazyData.Value.Length / (LineLength + LineEndLength));
        }

        public static int Count
        {
            get { return LazyCount.Value; }
        }

        public static byte[] GetGeohash(int line)
        {
            return GetLine(line, 0, Geohash.Precision);
        }

        public static int GetLineNumber(int line)
        {
            byte[] digits = GetLine(line, Geohash.Precision, LineLength - Geohash.Precision);
            return GetDigit(digits[2]) + ((GetDigit(digits[1]) + (GetDigit(digits[0]) * 10)) * 10);
        }

        private static int GetDigit(byte b)
        {
            return b - '0';
        }

        private static byte[] GetLine(int line, int start, int count)
        {
            int index = ((LineLength + LineEndLength) * (line - 1)) + start;
            Lazy<MemoryStream> stream = LazyData;
            byte[] buffer = new byte[count];
            Array.Copy(stream.Value.GetBuffer(), index, buffer, 0, count);
            return buffer;
        }
    }
}
