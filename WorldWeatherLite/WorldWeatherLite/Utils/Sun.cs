using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public class Sun
    {
        private const double _INV360 = 1.0d / 360.0d;

        private const double _SUNRISE_SUNSET_ALTITUDE = -35d / 60d;
        private const double _CIVIL_TWILIGHT_ALTITUDE = -6d;
        private const double _NAUTICAL_TWILIGHT_ALTITUDE = -12d;
        private const double _ASTRONOMICAL_TWILIGHT_ALTITUDE = -18d;



        /* Some conversion factors between radians and degrees */
        private const double _RAD_DEG = 180.0 / Math.PI;
        private const double _DEG_RAD = Math.PI / 180.0;

        /// <summary>
        /// Compute sunrise/sunset times UTC
        /// </summary>
        /// <param name="iYear">The year</param>
        /// <param name="iMonth">The month of year</param>
        /// <param name="iDay">The day of month</param>
        /// <param name="dLat">The latitude</param>
        /// <param name="dLng">The longitude</param>
        /// <param name="dTsunrise">The computed sunrise time (in seconds)</param>
        /// <param name="dTsunset">The computed sunset time (in seconds)</param>
        public static void SunriseSunset(int iYear, int iMonth, int iDay, double dLat, double dLng, out double dTsunrise, out double dTsunset)
        {
            sunriseSunset(iYear, iMonth, iDay, dLng, dLat, _SUNRISE_SUNSET_ALTITUDE, true, out dTsunrise, out dTsunset);
        }

        /// <summary>
        /// Compute civil twilight times UTC
        /// </summary>
        /// <param name="iYear">The year</param>
        /// <param name="iMonth">The month of year</param>
        /// <param name="iDay">The day of month</param>
        /// <param name="dLat">The latitude</param>
        /// <param name="dLng">The longitude</param>
        /// <param name="dTsunrise">The computed civil twilight time at sunrise (in seconds)</param>
        /// <param name="dTsunset">The computed civil twilight time at sunset (in seconds)</param>
        public static void CivilTwilight(int iYear, int iMonth, int iDay, double dLat, double dLng, out double dTsunrise, out double dTsunset)
        {
            sunriseSunset(iYear, iMonth, iDay, dLng, dLat, _CIVIL_TWILIGHT_ALTITUDE, false, out dTsunrise, out dTsunset);
        }

        /// <summary>
        /// Compute nautical twilight times UTC
        /// </summary>
        /// <param name="iYear">The year</param>
        /// <param name="iMonth">The month of year</param>
        /// <param name="iDay">The day of month</param>
        /// <param name="dLat">The latitude</param>
        /// <param name="dLng">The longitude</param>
        /// <param name="dTsunrise">The computed nautical twilight time at sunrise (in seconds)</param>
        /// <param name="dTsunset">The computed nautical twilight time at sunset (in seconds)</param>
        public static void NauticalTwilight(int iYear, int iMonth, int iDay, double dLat, double dLng, out double dTsunrise, out double dTsunset)
        {
            sunriseSunset(iYear, iMonth, iDay, dLng, dLat, _NAUTICAL_TWILIGHT_ALTITUDE, false, out dTsunrise, out dTsunset);
        }

        /// <summary>
        /// Compute astronomical twilight times UTC
        /// </summary>
        /// <param name="iYear">The year</param>
        /// <param name="iMonth">The month of year</param>
        /// <param name="iDay">The day of month</param>
        /// <param name="iLat">The latitude</param>
        /// <param name="iLng">The longitude</param>
        /// <param name="dTsunrise">The computed astronomical twilight time at sunrise (in seconds)</param>
        /// <param name="dTsunset">The computed astronomical twilight time at sunset (in seconds)</param>
        public static void AstronomicalTwilight(int iYear, int iMonth, int iDay, double iLat, double iLng, out double dTsunrise, out double dTsunset)
        {
            sunriseSunset(iYear, iMonth, iDay, iLng, iLat, _ASTRONOMICAL_TWILIGHT_ALTITUDE, false, out dTsunrise, out dTsunset);
        }

        /// <summary>
        /// Note: year,month,date = calendar date, 1801-2099 only.
        /// Eastern longitude positive, Western longitude negative
        /// Northern latitude positive, Southern latitude negative
        /// The longitude value is not critical. Set it to the correct
        /// The latitude however IS critical - be sure to get it correct
        /// </summary>
        /// <param name="iYear">
        /// altit = the altitude which the Sun should cross
        /// Set to -35/60 degrees for rise/set, -6 degrees
        /// for civil, -12 degrees for nautical and -18
        /// degrees for astronomical twilight.
        /// </param>
        /// <param name="iMonth"></param>
        /// <param name="iDay"></param>
        /// <param name="dLon"></param>
        /// <param name="dLat"></param>
        /// <param name="dAlt"></param>
        /// <param name="bUpperLimb">
        /// true -> upper limb, true -> center
        /// Set to true (e.g. 1) when computing day length
        /// and to false when computing day+twilight length.
        /// </param>
        /// <returns></returns>
        public static double DayLen(int iYear, int iMonth, int iDay, double dLon, double dLat,
                          double dAlt, bool bUpperLimb)
        {
            double d;          /* Days since 2000 Jan 0.0 (negative before) */
            double dOblEcl;    /* Obliquity (inclination) of Earth's axis */
            double dSr;         /* Solar distance, astronomical units */
            double dSlon;       /* True solar longitude */
            double dSinSdecl;  /* Sine of Sun's declination */
            double dCosSdecl;  /* Cosine of Sun's declination */
            double dSradius;    /* Sun's apparent radius */
            double dT;          /* Diurnal arc */

            /* Compute d of 12h local mean solar time */
            d = daysSince2000Jan0(iYear, iMonth, iDay) + 0.5 - dLon / 360.0;

            /* Compute obliquity of ecliptic (inclination of Earth's axis) */
            dOblEcl = 23.4393 - 3.563E-7 * d;

            /* Compute Sun's ecliptic longitude and distance */
            sunpos(d, out dSlon, out dSr);

            /* Compute sine and cosine of Sun's declination */
            dSinSdecl = sind(dOblEcl) * sind(dSlon);
            dCosSdecl = Math.Sqrt(1.0 - dSinSdecl * dSinSdecl);

            /* Compute the Sun's apparent radius, degrees */
            dSradius = 0.2666 / dSr;

            /* Do correction to upper limb, if necessary */
            if (bUpperLimb)
                dAlt -= dSradius;

            /* Compute the diurnal arc that the Sun traverses to reach */
            /* the specified altitude altit: */
            double dCost = (sind(dAlt) - sind(dLat) * dSinSdecl) / (cosd(dLat) * dCosSdecl);

            /* Sun always below altit */
            if (dCost >= 1.0)
                dT = 0.0;
            /* Sun always above altit */
            else if (dCost <= -1.0)
                dT = 24.0;
            /* The diurnal arc, hours */
            else
                dT = (2.0 / 15.0) * acosd(dCost);

            return dT;
        }

        /* +++Date last modified: 05-Jul-1997 */
        /* Updated comments, 05-Aug-2013 */

        /*
            SUNRISET.C - computes Sun rise/set times, start/end of twilight, and
            the length of the day at any date and latitude
            Written as DAYLEN.C, 1989-08-16
            Modified to SUNRISET.C, 1992-12-01
            (c) Paul Schlyter, 1989, 1992
            Released to the public domain by Paul Schlyter, December 1992
        */

        /* Converted to C# by Mursaat 05-Feb-2017 */
        
        /// <summary>
        /// A function to compute the number of days elapsed since 2000 Jan 0.0 
        /// (which is equal to 1999 Dec 31, 0h UT)  
        /// </summary>
        /// <param name="iY"></param>
        /// <param name="iM"></param>
        /// <param name="iD"></param>
        /// <returns></returns>
        private static long daysSince2000Jan0(int iY, int iM, int iD)
        {
            return 367L * iY - ((7 * (iY + ((iM + 9) / 12))) / 4) + ((275 * iM) / 9) + iD - 730530L;
        }

        /* The trigonometric functions in degrees */
        private static double sind(double dX)
        {
            return Math.Sin(dX * _DEG_RAD);
        }

        private static double cosd(double dX)
        {
            return Math.Cos(dX * _DEG_RAD);
        }

        private static double tand(double dX)
        {
            return Math.Tan(dX * _DEG_RAD);
        }

        private static double atand(double dX)
        {
            return _RAD_DEG * Math.Atan(dX);
        }

        private static double asind(double dX)
        {
            return _RAD_DEG * Math.Asin(dX);
        }

        private static double acosd(double dX)
        {
            return _RAD_DEG * Math.Acos(dX);
        }

        private static double atan2d(double dY, double dX)
        {
            return _RAD_DEG * Math.Atan2(dY, dX);
        }

        /// <summary>
        /// The "workhorse" function for sun rise/set times
        /// Note: year,month,date = calendar date, 1801-2099 only.             
        /// Eastern longitude positive, Western longitude negative       
        /// Northern latitude positive, Southern latitude negative       
        /// The longitude value IS critical in this function! 
        /// </summary>
        /// <param name="iYear"></param>
        /// <param name="iMonth"></param>
        /// <param name="iDay"></param>
        /// <param name="dLon"></param>
        /// <param name="dLat"></param>
        /// <param name="dAlt">
        /// the altitude which the Sun should cross
        /// Set to -35/60 degrees for rise/set, -6 degrees
        /// for civil, -12 degrees for nautical and -18
        /// degrees for astronomical twilight.
        /// </param>
        /// <param name="bUpperLimb">
        /// true -> upper limb, false -> center
        /// Set to true (e.g. 1) when computing rise/set
        /// times, and to false when computing start/end of twilight.
        /// </param>
        /// <param name="dTrise">where to store the rise time</param>
        /// <param name="dTset">where to store the set time</param>
        /// <returns>
        ///  0	=	sun rises/sets this day, times stored at trise and tset
        /// +1	=	sun above the specified "horizon" 24 hours.
        ///			trise set to time when the sun is at south,
        ///			minus 12 hours while *tset is set to the south
        ///			time plus 12 hours. "Day" length = 24 hours
        /// -1	=	sun is below the specified "horizon" 24 hours
        ///			"Day" length = 0 hours, *trise and *tset are
        ///			both set to the time when the sun is at south.
        /// </returns>
        private static int sunriseSunset(int iYear, int iMonth, int iDay, double dLon, double dLat,
                         double dAlt, bool bUpperLimb, out double dTrise, out double dTset)
        {
            double d;		   /* Days since 2000 Jan 0.0 (negative before) */
            double dSr;         /* Solar distance, astronomical units */
            double dSra;        /* Sun's Right Ascension */
            double dSdec;       /* Sun's declination */
            double dSradius;    /* Sun's apparent radius */
            double dT;          /* Diurnal arc */
            double dTsouth;     /* Time when Sun is at south */
            double dSidTime;    /* Local sidereal time */

            int iResult = 0; /* Return cde from function - usually 0 */

            /* Compute d of 12h local mean solar time */
            d = daysSince2000Jan0(iYear, iMonth, iDay) + 0.5 - dLon / 360.0;

            /* Compute the local sidereal time of this moment */
            dSidTime = revolution(gmsto(d) + 180.0 + dLon);

            /* Compute Sun's RA, Decl and distance at this moment */
            sun_RA_dec(d, out dSra, out dSdec, out dSr);

            /* Compute time when Sun is at south - in hours UT */
            dTsouth = 12.0 - rev180(dSidTime - dSra) / 15.0;

            /* Compute the Sun's apparent radius in degrees */
            dSradius = 0.2666 / dSr;

            /* Do correction to upper limb, if necessary */
            if (bUpperLimb)
                dAlt -= dSradius;

            /* Compute the diurnal arc that the Sun traverses to reach */
            /* the specified altitude altit: */
            {
                double dCost;
                dCost = (sind(dAlt) - sind(dLat) * sind(dSdec)) / (cosd(dLat) * cosd(dSdec));
                if (dCost >= 1.0) /* Sun always below altit */
                {
                    iResult = -1;
                    dT = 0.0;
                }
                else if (dCost <= -1.0) /* Sun always above altit */
                {
                    iResult = +1;
                    dT = 12.0;
                }
                else
                    dT = acosd(dCost) / 15.0;   /* The diurnal arc, hours */
            }

            /* Store rise and set times - in hours UT */
            dTrise = dTsouth - dT;
            dTset = dTsouth + dT;

            return iResult;
        }

        /// <summary>
        /// Computes the Sun's ecliptic longitude and distance 
        /// at an instant given in d, number of days since
        /// 2000 Jan 0.0.  The Sun's ecliptic latitude is not
        /// computed, since it's always very near 0.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="dLon"></param>
        /// <param name="dR"></param>
        private static void sunpos(double d, out double dLon, out double dR)
        {
            double dM;         /* Mean anomaly of the Sun */
            double dW;         /* Mean longitude of perihelion */
            /* Note: Sun's mean longitude = M + w */
            double de;         /* Eccentricity of Earth's orbit */
            double dE;         /* Eccentric anomaly */
            double dX, dY;      /* x, y coordinates in orbit */
            double dV;         /* True anomaly */

            /* Compute mean elements */
            dM = revolution(356.0470 + 0.9856002585 * d);
            dW = 282.9404 + 4.70935E-5 * d;
            de = 0.016709 - 1.151E-9 * d;

            /* Compute true longitude and radius vector */
            dE = dM + de * _RAD_DEG * sind(dM) * (1.0 + de * cosd(dM));
            dX = cosd(dE) - de;
            dY = Math.Sqrt(1.0 - de * de) * sind(dE);
            dR = Math.Sqrt(dX * dX + dY * dY);       /* Solar distance */
            dV = atan2d(dY, dX);                   /* True anomaly */
            dLon = dV + dW;                        /* True solar longitude */

            if (dLon >= 360.0)
                dLon -= 360.0;                   /* Make it 0..360 degrees */
        }

        /// <summary>
        /// Computes the Sun's equatorial coordinates RA, Decl
        /// and also its distance, at an instant given in d,
        /// the number of days since 2000 Jan 0.0.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="dRA"></param>
        /// <param name="dDec"></param>
        /// <param name="dR"></param>
        private static void sun_RA_dec(double d, out double dRA, out double dDec, out double dR)
        {
            double dLon, dOblEcl, dX, dY, dZ;

            /* Compute Sun's ecliptical coordinates */
            sunpos(d, out dLon, out dR);

            /* Compute ecliptic rectangular coordinates (z=0) */
            dX = dR * cosd(dLon);
            dY = dR * sind(dLon);

            /* Compute obliquity of ecliptic (inclination of Earth's axis) */
            dOblEcl = 23.4393 - 3.563E-7 * d;

            /* Convert to equatorial rectangular coordinates - x is unchanged */
            dZ = dY * sind(dOblEcl);
            dY = dY * cosd(dOblEcl);

            /* Convert to spherical coordinates */
            dRA = atan2d(dY, dX);
            dDec = atan2d(dZ, Math.Sqrt(dX * dX + dY * dY));
        }

        /// <summary>
        /// This function reduces any angle to within the first revolution
        /// by subtracting or adding even multiples of 360.0 until the
        /// result is >= 0.0 and < 360.0
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static double revolution(double dX)
        {
            return (dX - 360.0 * Math.Floor(dX * _INV360));
        }

        /// <summary>
        /// Reduce angle to within +180..+180 degrees
        /// </summary>
        /// <param name="dX"></param>
        /// <returns></returns>
        private static double rev180(double dX)
        {
            return (dX - 360.0 * Math.Floor(dX * _INV360 + 0.5));
        }

        /// <summary>
        /// This function computes GMST0, the Greenwich Mean Sidereal Time  
        /// at 0h UT (i.e. the sidereal time at the Greenwhich meridian at  
        /// 0h UT).  GMST is then the sidereal time at Greenwich at any     
        /// time of the day.  I've generalized GMST0 as well, and define it 
        /// as:  GMST0 = GMST - UT  --  this allows GMST0 to be computed at 
        /// other times than 0h UT as well.  
        /// 
        /// While this sounds somewhat contradictory, it is very practical:
        /// instead of computing  GMST like:
        /// GMST = (GMST0) + UT * (366.2422/365.2422)                                                                                     
        /// where (GMST0) is the GMST last time UT was 0 hours, one simply  
        /// computes: GMST = GMST0 + UT                                                                                                          
        /// where GMST0 is the GMST "at 0h UT" but at the current moment! 
        /// 
        /// Defined in this way, GMST0 will increase with about 4 min a     
        /// day.  It also happens that GMST0 (in degrees, 1 hr = 15 degr)   
        /// is equal to the Sun's mean longitude plus/minus 180 degrees!    
        /// (if we neglect aberration, which amounts to 20 seconds of arc   
        /// or 1.33 seconds of time)    
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        private static double gmsto(double d)
        {
            /* Sidtime at 0h UT = L (Sun's mean longitude) + 180.0 degr  */
            /* L = M + w, as defined in sunpos().  Since I'm too lazy to */
            /* add these numbers, I'll let the C compiler do it for me.  */
            /* Any decent C compiler will add the constants at compile   */
            /* time, imposing no runtime or code overhead.               */
            return revolution((180.0 + 356.0470 + 282.9404) + (0.9856002585 + 4.70935E-5) * d);
        }
    }
}
