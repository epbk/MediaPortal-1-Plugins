using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public class GeoClock
    {
        private const string _PICTURE_DIR = @"C:\ProgramData\Team MediaPortal\MediaPortal\Skin\Titan";
        private const string _PICTURE_PATH_DAY = @"\Media\WorldWeather\GeoClock\high\day.png";
        private const string _PICTURE_PATH_NIGHT = @"\Media\WorldWeather\GeoClock\high\night.png";
        private const string _PICTURE_PATH_PIN = @"\Media\WorldWeather\GeoClock\high\pin.png";

        private const double _RAD_DEG = 180.0 / Math.PI;
        private const double _DEG_RAD = Math.PI / 180.0;

        //internal unsafe sealed class Picture
        //{
        //    internal struct ARGB
        //    {
        //        public byte B;
        //        public byte G;
        //        public byte R;
        //        public byte Alpha;
        //    }

        //    private readonly Bitmap _Bitmap;

        //    private BitmapData _BitmapData;

        //    private long[] _Array;

        //    private int _Width;
        //    private int _Height;

        //    private int _Stride;

        //    private IntPtr _FirstPixel;

        //    internal byte* FirstPixel
        //    {
        //        get { return this._FirstPixelPtr; }
        //    }private unsafe byte* _FirstPixelPtr = null;


        //    public unsafe Picture(Bitmap bmp)
        //    {
        //        this._Bitmap = bmp;
        //        this.setSize(bmp.Width, bmp.Height);
        //        this.setArray(new long[this._Width * this._Height]);
        //    }
        //    public unsafe Picture(int iWidth, int iHeight)
        //    {
        //        this._Bitmap = new Bitmap(iWidth, iHeight);
        //        this.setSize(iWidth, iHeight);
        //        this.setArray(new long[iWidth * iHeight]);
        //    }


        //    private void setArray(long[] array)
        //    {
        //        this._Array = array;
        //    }

        //    public int Width()
        //    {
        //        return this._Width;
        //    }

        //    public int Height()
        //    {
        //        return this._Height;
        //    }

        //    public Bitmap Bitmap()
        //    {
        //        return this._Bitmap;
        //    }

        //    private void setSize(int iWidth, int iHeight)
        //    {
        //        this._Width = iWidth;
        //        this._Height = iHeight;
        //    }

        //    public unsafe void Init()
        //    {
        //        this._BitmapData = this._Bitmap.LockBits(new Rectangle(0, 0, this._Bitmap.Width, this._Bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        //        this._Stride = this._BitmapData.Stride;
        //        this._FirstPixel = this._BitmapData.Scan0;
        //        this._FirstPixelPtr = (byte*)this._BitmapData.Scan0.ToPointer();
        //    }

        //    public unsafe void clean()
        //    {
        //        this._Bitmap.UnlockBits(this._BitmapData);
        //        this._BitmapData = null;
        //        this._FirstPixelPtr = null;
        //    }

        //    private unsafe ARGB* getArgbPointer(int iX, int iY)
        //    {
        //        return (ARGB*)(this._FirstPixelPtr + (long)iY * (long)this._Stride + (long)iX * (long)sizeof(ARGB));
        //    }

        //    public unsafe int GetRgbFromLocation(int iX, int iY)
        //    {
        //        ARGB* ptr = this.getArgbPointer(iX, iY);
        //        return (ptr->R << 16) | (ptr->G << 8) | ptr->B;
        //    }

        //    public unsafe void SetRgbFromLocation(int iX, int iY, int iRgb)
        //    {
        //        ARGB* ptr = this.getArgbPointer(iX, iY);
        //        ptr->Alpha = byte.MaxValue;
        //        ptr->R = (byte)((iRgb >> 16) & 0xFF);
        //        ptr->G = (byte)((iRgb >> 8) & 0xFF);
        //        ptr->B = (byte)(iRgb & 0xFF);
        //    }

        //    public void MoveAndReplace(int iXfrom, int iYfrom, int iXto, int iYto, int lRgbNew)
        //    {
        //        int iRgbOrig = this.GetRgbFromLocation(iXfrom, iYfrom);
        //        this.SetRgbFromLocation(iXfrom, iYfrom, lRgbNew);
        //        this.SetRgbFromLocation(iXto, iYto, iRgbOrig);
        //    }
        //}

        internal struct Location
        {
            public string Description;
            public double Longitude;
            public double Latitude;
        }

        //internal Picture PictureDay;

        //internal Picture PictureNight;

        //internal Picture PicOutput;

        internal readonly string Path;

        internal readonly Location[] Locations;

        internal int LocationCount;


        public GeoClock()
        {
            this.Path = string.Empty;
            this.Locations = new Location[10];
            this.ClearGeoClockLocation();
        }
        public GeoClock(string strPath)
        {
            this.Path = strPath;
            this.Locations = new Location[10];
            this.ClearGeoClockLocation();
        }

        public string GetGeoClockImageLocation()
        {
            return _PICTURE_PATH_DAY;
        }

        public void ClearGeoClockLocation()
        {
            this.LocationCount = 0;
            for (int i = 0; i < 10; i++)
            {
                this.Locations[i].Description = string.Empty;
                this.Locations[i].Longitude = 0.0;
                this.Locations[i].Latitude = 0.0;
            }
        }

        public void AddGeoClockLocation(string strDescription, double dLongitude, double dLatitude)
        {
            if (!string.IsNullOrEmpty(strDescription))
            {
                this.Locations[LocationCount].Description = strDescription;
                this.Locations[LocationCount].Longitude = dLongitude;
                this.Locations[LocationCount].Latitude = dLatitude;
                this.LocationCount++;
            }
        }

        //public Image GetGeoClockImage(DateTime time)
        //{
        //    if (File.Exists(getPath(_PICTURE_PATH_DAY)) && File.Exists(getPath(_PICTURE_PATH_NIGHT)))
        //    {
        //        Bitmap bmpDay = new Bitmap(getPath(_PICTURE_PATH_DAY));
        //        Bitmap bmpNight = new Bitmap(getPath(_PICTURE_PATH_NIGHT));
        //        Bitmap bmpOutput = new Bitmap(bmpDay.Width, bmpDay.Height);
        //        this.PictureDay = new Picture(bmpDay);
        //        this.PictureNight = new Picture(bmpNight);
        //        this.PicOutput = new Picture(bmpOutput);
        //        this.init();
        //        double dSiderealTime = getSiderealTime(time);
        //        double dRightAscension = getRightAscension(time);
        //        double dDeclination = getDeclination(time);
        //        int iWidth = bmpDay.Width;
        //        int iHeight = bmpDay.Height;
        //        for (int iX = 0; iX < iWidth; iX++)
        //        {
        //            double dXlongitude = 180.0 - ((double)iX + 0.5) / (double)iWidth * 360.0;
        //            double dLong = dSiderealTime * 360.0 / 24.0 - dXlongitude - dRightAscension;
        //            for (int iY = 0; iY < iHeight; iY++)
        //            {
        //                double dLat = 90.0 - ((double)iY + 0.5) / (double)iHeight * 180.0;
        //                double dAltitude = getAltitude(dLong, dDeclination, dLat);

        //                //1.0 = day; 0.0 = night
        //                //Conversion:  -9.0÷0.0 => 0.0÷1.0
        //                double dDayNightRatio = dAltitude <= 0.0 ? (dAltitude >= -9.0 ? ((dAltitude - -9.0) / 9.0) : 0.0) : 1.0;

        //                int iRgbDay = this.PictureDay.GetRgbFromLocation(iX, iY);
        //                int iRday = (iRgbDay >> 16) & 0xFF;
        //                int iGday = (iRgbDay >> 8) & 0xFF;
        //                int iBday = iRgbDay & 0xFF;

        //                int iRgbNight = this.PictureNight.GetRgbFromLocation(iX, iY);
        //                int iRnight = (iRgbNight >> 16) & 0xFF;
        //                int iGnight = (iRgbNight >> 8) & 0xFF;
        //                int iBnight = iRgbNight & 0xFF;

        //                int iRgbOut = ((int)((double)iRday * dDayNightRatio + (double)iRnight * (1.0 - dDayNightRatio)) << 16)
        //                    | ((int)((double)iGday * dDayNightRatio + (double)iGnight * (1.0 - dDayNightRatio)) << 8)
        //                    | (int)((double)iBday * dDayNightRatio + (double)iBnight * (1.0 - dDayNightRatio));

        //                this.PicOutput.SetRgbFromLocation(iX, iY, iRgbOut);
        //            }
        //        }

        //        this.clean();
        //        bmpDay.Dispose();
        //        bmpNight.Dispose();

        //        return bmpOutput;
        //    }

        //    return null;
        //}

        public unsafe Image GetGeoClockImage(DateTime time)
        {
            if (File.Exists(getPath(_PICTURE_PATH_DAY)) && File.Exists(getPath(_PICTURE_PATH_NIGHT)))
            {
                Bitmap bmpDay = new Bitmap(getPath(_PICTURE_PATH_DAY));
                Bitmap bmpNight = new Bitmap(getPath(_PICTURE_PATH_NIGHT));

                int iWidth = bmpDay.Width;
                int iHeight = bmpDay.Height;

                if (iWidth != bmpNight.Width || iHeight != bmpNight.Height)
                {
                    bmpDay.Dispose();
                    bmpDay.Dispose();
                    bmpDay = null;
                    bmpNight = null;
                    return null;
                }

                BitmapData bmpDataDay = bmpDay.LockBits(new Rectangle(0, 0, iWidth, iHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                byte* pDay = (byte*)bmpDataDay.Scan0.ToPointer();

                BitmapData bmpDataNight = bmpNight.LockBits(new Rectangle(0, 0, iWidth, iHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte* pNight = (byte*)bmpDataNight.Scan0.ToPointer();

                int iStride = iWidth * 4;
                
                double dSiderealTime = getSiderealTime(time);
                double dRightAscension = getRightAscension(time);
                double dDeclination = getDeclination(time);
                
                for (int iX = 0; iX < iWidth; iX++)
                {
                    double dXlongitude = 180.0 - ((double)iX + 0.5) / (double)iWidth * 360.0;
                    double dLong = dSiderealTime * 360.0 / 24.0 - dXlongitude - dRightAscension;

                    byte* pDayT = pDay;
                    byte* pNightT = pNight;

                    for (int iY = 0; iY < iHeight; iY++)
                    {
                        double dLat = 90.0 - ((double)iY + 0.5) / (double)iHeight * 180.0;
                        double dAltitude = getAltitude(dLong, dDeclination, dLat);

                        //1.0 = day; 0.0 = night
                        //Conversion:  -9.0÷0.0 => 0.0÷1.0
                        if (dAltitude >= 0.0)
                        {
                            //Day: keep original pixel
                        }
                        else if (dAltitude <= -9.0)
                        {
                            //Night: copy full night pixel
                            *pDayT++ = *pNightT++;
                            *pDayT++ = *pNightT++;
                            *pDayT = *pNightT;
                            pDayT -= 2;
                            pNightT -= 2;
                        }
                        else
                        {
                            //Mixed day/night
                            double dRatioDay = (dAltitude + 9.0) / 9.0;
                            double dRatioNight = 1.0 - dRatioDay;

                            *pDayT = (byte)(((double)*pDayT * dRatioDay) + ((double)*pNightT++ * dRatioNight)); //B
                            pDayT++;
                            *pDayT = (byte)(((double)*pDayT * dRatioDay) + ((double)*pNightT++ * dRatioNight)); //G
                            pDayT++;
                            *pDayT = (byte)(((double)*pDayT * dRatioDay) + ((double)*pNightT * dRatioNight)); //R

                            pDayT -= 2;
                            pNightT -= 2;
                        }

                        //Next row
                        pDayT += iStride;
                        pNightT += iStride;
                    }

                    //Next column
                    pDay += 4;
                    pNight += 4;
                }

                bmpDay.UnlockBits(bmpDataDay);
                bmpNight.UnlockBits(bmpDataNight);

                bmpNight.Dispose();

                return bmpDay;
            }

            return null;
        }

        public bool SaveGeoClockImage(Image image, string strFilePath)
        {
            image.Save(strFilePath, ImageFormat.Png);
            return File.Exists(strFilePath);
        }

        public Image SetGeoClockLocation(Font font, Color color, Image bitmap, bool bDrawText, bool bDrawPin, bool bDrawPoint)
        {
            if (bitmap != null)
            {
                Graphics g = Graphics.FromImage(bitmap);
                Location[] locations = this.Locations;
                for (int i = 0; i < locations.Length; i++)
                {
                    Location location = locations[i];
                    if (string.IsNullOrEmpty(location.Description))
                        continue;

                    if (bDrawPin && File.Exists(getPath(_PICTURE_PATH_PIN)))
                    {
                        Bitmap bmpPin = new Bitmap(getPath(_PICTURE_PATH_PIN));

                        g.DrawImageUnscaled(bmpPin,
                            getX(location.Longitude, bitmap.Width) - bmpPin.Width / 2,
                            getY(location.Latitude, bitmap.Height) - bmpPin.Height - 5);

                        if (bDrawPoint)
                            g.DrawRectangle(new Pen(Color.Red),
                                getX(location.Longitude, bitmap.Width),
                                getY(location.Latitude, bitmap.Height), 3, 3);
                    }

                    if (bDrawText)
                    {
                        Bitmap bmpText = getImageFromText(location.Description, font, color);
                        if (bmpText != null)
                        {
                            int iX = getX(location.Longitude, bitmap.Width) - bmpText.Width / 2;
                            iX = ((iX <= 0) ? 10 : iX);
                            iX = ((iX + bmpText.Width / 2 >= bitmap.Width - bmpText.Width / 2) ? (bitmap.Width - bmpText.Width - 10) : iX);

                            int iY = getY(location.Latitude, bitmap.Height) + 5;
                            iY = ((iY + bmpText.Height >= bitmap.Height) ? (bitmap.Height - bmpText.Height - 10) : iY);

                            g.DrawImageUnscaled(bmpText, iX, iY);
                        }
                    }
                }

                g.Dispose();

                return bitmap;
            }

            return null;
        }


        private string getPath(string strPath)
        {
            if (string.IsNullOrEmpty(this.Path))
                return _PICTURE_DIR + strPath;

            return this.Path + strPath;
        }


        private static double getDaysFromJ2000(DateTime dt)
        {
            return (double)(367 * dt.Year - 7 * (dt.Year + (dt.Month + 9) / 12) / 4 + 275 * dt.Month / 9 + dt.Day) - 730531.5 + getRealHour(dt) / 24.0;
        }

        private static double getRealHour(DateTime dt)
        {
            return (double)dt.Hour + (double)dt.Minute / 60 + (double)dt.Second / 3600;
        }

        private static double getSiderealTime(DateTime dt)
        {
            //Sidereal time (as a unit also sidereal day or sidereal rotation period) (sidereal /saɪˈdɪəriəl, sə-/ sy-DEER-ee-əl, sə-)
            //is a timekeeping system that astronomers use to locate celestial objects.
            //Using sidereal time, it is possible to easily point a telescope to the proper coordinates in the night sky.
            //Sidereal time is a "time scale that is based on Earth's rate of rotation measured relative to the fixed stars",
            //[1] or more correctly, relative to the March equinox.

            //A sidereal day on Earth is approximately 86164.0905 seconds (23 h 56 min 4.0905 s or 23.9344696 h).

            // Number of days from J2000.0.
            double dDaysFromJ2000 = getDaysFromJ2000(dt);
            double dJulianCenturies = dDaysFromJ2000 / 36525.0; //Julian year = 365.25 days
            double dResult;

            for (dResult = (280.46061837 + 360.98564736629 * dDaysFromJ2000 + 0.000388 * dJulianCenturies * dJulianCenturies) * 24.0 / 360.0; dResult < 0.0; dResult += 24.0)
            {
            }

            while (dResult >= 24.0)
            {
                dResult -= 24.0;
            }
            return dResult;
        }

        private static double getRightAscension(DateTime dt)
        {
            double dDaysFromJ2000 = getDaysFromJ2000(dt);
            double dJulianCenturies = dDaysFromJ2000 / 36525.0;
            double dMeanLongitude = 279.697 + 36000.769 * dJulianCenturies;
            double dMeanAnomaly = 358.476 + 35999.05 * dJulianCenturies;
            double dElipticalLongitude = dMeanLongitude + (1.919 - 0.005 * dJulianCenturies) * Math.Sin(dMeanAnomaly * _DEG_RAD) + 0.02 * Math.Sin(2.0 * dMeanAnomaly * _DEG_RAD);
            double dObliquity = 23.452 - 0.013 * dJulianCenturies;
            return Math.Atan2(Math.Sin(dElipticalLongitude * _DEG_RAD) * Math.Cos(dObliquity * _DEG_RAD), Math.Cos(dElipticalLongitude * _DEG_RAD)) * _RAD_DEG;
        }

        private static double getDeclination(DateTime dt)
        {
            //T = (JD at 0h UT on July 1 of year in question - 2415020.) / 36525
            //e = 23.452 294 - 0.013 0125*T - 0.000 001 64*T^2 + 0.000 000 503*T^3 
            //4. e can also be gotten from the Astronomical Ephemeris, Section B, page B18. For 1990.5, e = 23.440527

            double dDaysFromJ2000 = getDaysFromJ2000(dt);// +getRealHour(dt) / 24.0;
            double dJulianCenturies = dDaysFromJ2000 / 36525.0; //Julian year = 365.25 days
            double dObliquity = 23.452 - 0.013 * dJulianCenturies;
            return Math.Asin(Math.Sin(getRightAscension(dt) * _DEG_RAD) * Math.Sin(dObliquity * _DEG_RAD)) * _RAD_DEG;
        }

        private static double getAltitude(double dLongitude, double dDeclination, double dLatitude)
        {
            return Math.Asin(
                  Math.Sin(dLatitude * _DEG_RAD) * Math.Sin(dDeclination * _DEG_RAD)
                + Math.Cos(dLatitude * _DEG_RAD) * Math.Cos(dDeclination * _DEG_RAD) * Math.Cos(dLongitude * _DEG_RAD)
                ) * _RAD_DEG;
        }


        //private void init()
        //{
        //    this.PictureDay.Init();
        //    this.PictureNight.Init();
        //    this.PicOutput.Init();
        //}

        //private void clean()
        //{
        //    this.PictureDay.clean();
        //    this.PictureNight.clean();
        //    this.PicOutput.clean();
        //}


        private static int getX(double dLongitude, double dWidth)
        {
            return Convert.ToInt32((180.0 + dLongitude) * (dWidth / 360.0));
        }

        private static int getY(double dLatitude, double dHeight)
        {
            return Convert.ToInt32((90.0 - dLatitude) * (dHeight / 180.0));
        }


        private static Bitmap getImageFromText(string strText, Font font, Color color)
        {
            if (!string.IsNullOrEmpty(strText))
            {
                Bitmap bmp = new Bitmap(1, 1);
                Graphics g = Graphics.FromImage(bmp);
                int iWidth = (int)g.MeasureString(strText, font).Width;
                int iHeight = (int)g.MeasureString(strText, font).Height;
                bmp = new Bitmap(bmp, new Size(iWidth, iHeight));
                g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.DrawString(strText, font, new SolidBrush(color), 0f, 0f);
                g.Flush();
                g.Dispose();
                return bmp;
            }
            return null;
        }
    }
}
