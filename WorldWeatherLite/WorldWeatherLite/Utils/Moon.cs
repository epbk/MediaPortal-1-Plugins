using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public class Moon
    {
        private const double _TOTAL_DAYS_OF_CYCLE = 29.53;

        //JD 2415018.5 corresponds to December 30, 1899
        private const double _JULIAN_CONSTANT = 2415018.5;

        public static MoonPhaseEnum GetMoonPhase(DateTime dtUtc, out double dPeriod)
        {
            double dJulianDate = dtUtc.ToOADate() + _JULIAN_CONSTANT;

            // London New Moon (1920)
            // https://www.timeanddate.com/moon/phases/uk/london?year=1920
            double dDaysSinceLastNewMoon = new DateTime(1920, 1, 21, 5, 25, 00, DateTimeKind.Utc).ToOADate() + _JULIAN_CONSTANT;

            double dNewMoons = (dJulianDate - dDaysSinceLastNewMoon) / _TOTAL_DAYS_OF_CYCLE;

            dPeriod = dNewMoons - Math.Truncate(dNewMoons);

            return (MoonPhaseEnum)(int)(dPeriod * 8);
        }

        /// <summary>
        /// Creates moon phase image
        /// </summary>
        /// <param name="dPeriod">Moon period: from 0.0 to 1.0</param>
        /// <param name="strFullMoonImagePath">Fullpath to full moon image</param>
        /// <param name="dMoonSize">Defines moon size in the image</param>
        /// <returns>Moon phase image</returns>
        public static unsafe Image GetMoonImage(double dPeriod, string strFullMoonImagePath, double dMoonSize = 1.0)
        {
            if (File.Exists(strFullMoonImagePath))
            {
                //Full moon image
                Bitmap bmpSrc;
                Bitmap bmpDst = null;

                try { bmpSrc = new Bitmap(strFullMoonImagePath); }
                catch { return null; }

                int iWidth = bmpSrc.Width;
                int iHeight = bmpSrc.Height;

                if (dPeriod < 0.0)
                    dPeriod = 0.0;
                else if (dPeriod >= 1.0)
                    dPeriod = dPeriod - Math.Truncate(dPeriod);

                //If the moon is not scaled across entire image, then we need to skip some pixels around the moon
                int iSkip = dMoonSize > 0.0 && dMoonSize < 1.0 ? (iWidth - (int)(dMoonSize * iWidth)) / 2 : 0;

                if (iWidth > 1 && iHeight > 1 && iWidth > iSkip)
                {
                    //Result image
                    bmpDst = new Bitmap(iWidth, iHeight, PixelFormat.Format32bppPArgb);

                    BitmapData dataSrc = bmpSrc.LockBits(new Rectangle(0, 0, iWidth, iHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    BitmapData dataDst = bmpDst.LockBits(new Rectangle(0, 0, iWidth, iHeight), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

                    //Final moon size
                    iWidth -= (iSkip * 2);
                    iHeight -= (iSkip * 2);

                    //Get pixel pointers
                    byte* pPixelSrc = (byte*)dataSrc.Scan0;
                    byte* pPixelDst = (byte*)dataDst.Scan0;

                    bool bWaning = false;
                    bool bGibous = false; //leftquadrant

                    //Ratio (elipse) calculation
                    double dRatio = dPeriod;
                    if (dPeriod >= 0.5)
                    {
                        dRatio = dPeriod - 0.5;
                        bWaning = true;
                    }

                    if (dRatio >= 0.25)
                    {
                        //Left quadrant
                        dRatio -= 0.25;
                        bGibous = true;
                    }
                    else
                        dRatio = 0.25 - dRatio;

                    dRatio /= 0.25;

                    //Quadrant size
                    int iQuadrantSize = iHeight / 2;

                    //Upper border
                    int iOffset = (iWidth + iSkip + iSkip) * iSkip * 4;
                    pPixelSrc += iOffset;
                    pPixelDst += iOffset;

                    //Every line
                    for (int iY = 0; iY < iHeight; iY++)
                    {
                        //First we need to get cos
                        double dCos = (double)(iQuadrantSize - iY) / iQuadrantSize;

                        //Now we can calculate angle
                        double dAngle = Math.Acos(dCos);

                        //Finally we can get sin
                        double dSin = Math.Sin(dAngle) * dRatio;

                        //X border
                        double dXedge = dSin * iQuadrantSize;

                        if (!bGibous)
                            dXedge += iQuadrantSize;
                        else
                            dXedge = (double)iQuadrantSize - dXedge;

                        //Front border
                        pPixelSrc += iSkip * 4;
                        pPixelDst += iSkip * 4;

                        //Every pixel on the line
                        for (int iX = 0; iX < iWidth; iX++)
                        {
                            byte bB = *pPixelSrc++;
                            byte bG = *pPixelSrc++;
                            byte bR = *pPixelSrc++;
                            byte bA = *pPixelSrc++;

                            float fShaddow = 0.25F;

                            double dDiff = bWaning ? dXedge - iX : (double)iX - dXedge;

                            if (Math.Abs(dDiff) <= 3.0)
                                fShaddow = 0.625F + (float)(dDiff / 9); //gradient
                            else
                            {
                                if (!bWaning)
                                {
                                    if (iX > dXedge)
                                        goto write; //full pixel
                                }
                                else
                                {
                                    if (iX < dXedge)
                                        goto write; //full pixel
                                }

                                //shaddow pixel
                            }

                            //Apply shaddow value
                            bB = (byte)(fShaddow * bB);
                            bG = (byte)(fShaddow * bG);
                            bR = (byte)(fShaddow * bR);


                        write:
                            //Write ARGB to the destination pixel
                            *pPixelDst++ = bB;
                            *pPixelDst++ = bG;
                            *pPixelDst++ = bR;
                            *pPixelDst++ = bA;
                        }

                        //Back border
                        pPixelSrc += iSkip * 4;
                        pPixelDst += iSkip * 4;
                    }

                    //Bottom border
                    //pPixelSrc += iOffset;
                    //pPixelDst += iOffset;

                    //Unlock bitmaps
                    bmpSrc.UnlockBits(dataSrc);
                    bmpDst.UnlockBits(dataDst);
                }

                //Dispose Full moon image
                bmpSrc.Dispose();
                bmpSrc = null;

                return bmpDst;
            }
            else
                return null;
        }
    }

}
