using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public class Calendar
    {
        public static DateTime JulianToGregorian(double dJulianTime)
        {
            //There is a measurement of time that is used in Astronomy, called Julian Day Number (JDN).
            //For reasons regarding historical calendar systems, it starts measuring time from noon, UTC, January 1, 4713 BC, and it measures time in days with a floating point value.
            //Because it is a sort-of standard, there is code available for converting JDN's to calendar dates.

            dJulianTime += 0.5;
            int iJt = (int)Math.Floor(dJulianTime);
            double dFraction = dJulianTime - (double)iJt;

            int iL = iJt + 68569;
            int iDays;

            if (iJt <= 2361221)
            {
                iL += 38;
                iDays = 146100;
            }
            else
                iDays = 146097; //400 years have exactly 146097 days.

            int iN = 4 * iL / iDays;
            iL -= (iDays * iN + 3) / 4;
            int iYr = 4000 * (iL + 1) / 1461001;
            iL = iL - 1461 * iYr / 4 + 31; //1461 = 365.25 * 4
            int iMn = 80 * iL / 2447;
            int iDay = iL - 2447 * iMn / 80;
            iL = iMn / 11;
            int iMonth = iMn + 2 - 12 * iL;
            int iYear = 100 * (iN - 49) + iYr + iL;
            int iHour = (int)(dFraction * 24.0);
            int iMinute = (int)((dFraction * 24.0 - iHour) * 60.0);
            return new DateTime(iYear, iMonth, iDay, iHour, iMinute, 0);
        }


        public static DateTime GetSpringStartDate(DateTime date, HemisphereTypeEnum type)
        {
            if (type == HemisphereTypeEnum.NorthernHemisphere)
                return getAstronomicalSpring(date);
            else
                return getAstronomicalAutumn(date);
        }

        public static DateTime GetSummerStartDate(DateTime date, HemisphereTypeEnum type)
        {
            if (type == HemisphereTypeEnum.NorthernHemisphere)
                return getAstronomicalSummer(date);
            else
                return getAstronomicalWinter(date);
        }

        public static DateTime GeAutumnStartDate(DateTime date, HemisphereTypeEnum type)
        {
            if (type == HemisphereTypeEnum.NorthernHemisphere)
                return getAstronomicalAutumn(date);
            else
                return getAstronomicalSpring(date);
        }

        public static DateTime GeWinterStartDate(DateTime date, HemisphereTypeEnum type)
        {
            if (type == HemisphereTypeEnum.NorthernHemisphere)
                return getAstronomicalWinter(date);
            else
                return getAstronomicalSummer(date);
        }


        public static DateTime GetNewYearDate(DateTime date)
        {
            return new DateTime(date.Year, 1, 1);
        }

        public static DateTime GetEpiphanyDate(DateTime date)
        {
            return new DateTime(date.Year, 1, 6);
        }

        public static DateTime GetAssumptionDayDate(DateTime date)
        {
            return new DateTime(date.Year, 8, 15);
        }

        public static DateTime GetReformationDayDate(DateTime date)
        {
            return new DateTime(date.Year, 10, 31);
        }

        public static DateTime GetAllSaintsDayDate(DateTime date)
        {
            return new DateTime(date.Year, 11, 1);
        }

        public static DateTime GetEasterSundayDate(DateTime date)
        {
            int iA = date.Year % 19;
            int iB = date.Year / 100;
            int iC = (iB - (iB / 4) - ((8 * iB + 13) / 25) + (19 * iA) + 15) % 30;
            int iD = iC - (iC / 28) * (1 - (iC / 28) * (29 / (iC + 1)) * ((21 - iA) / 11));
            int iE = iD - ((date.Year + (date.Year / 4) + iD + 2 - iB + (iB / 4)) % 7);
            int iMmonth = 3 + ((iE + 40) / 44);
            int iDay = iE + 28 - (31 * (iMmonth / 4));
            return new DateTime(date.Year, iMmonth, iDay);
        }

        public static DateTime GetHolyThursdayDate(DateTime date)
        {
            return GetEasterSundayDate(date).AddDays(-3.0);
        }

        public static DateTime GetGoodFridayDate(DateTime date)
        {
            return GetEasterSundayDate(date).AddDays(-2.0);
        }

        public static DateTime GetAscensionDayDate(DateTime date)
        {
            return GetEasterSundayDate(date).AddDays(39.0);
        }

        public static DateTime GetWhitSundayDate(DateTime date)
        {
            return GetEasterSundayDate(date).AddDays(49.0);
        }

        public static DateTime GetCorpusChristiDate(DateTime date)
        {
            return GetEasterSundayDate(date).AddDays(60.0);
        }

        public static DateTime GetChristmasDayDate(DateTime date)
        {
            return new DateTime(date.Year, 12, 25);
        }


        public static int GetDayNumber(DateTime date)
        {
            return CultureInfo.CurrentCulture.Calendar.GetDayOfYear(date);
        }

        public static int GetDayCount(DateTime date)
        {
            return new DateTime(date.Year, 12, 31).DayOfYear;
        }

        public static int GetWeekNumber(DateTime date)
        {
            CultureInfo currentCulture = CultureInfo.CurrentCulture;
            return currentCulture.Calendar.GetWeekOfYear(date, currentCulture.DateTimeFormat.CalendarWeekRule, currentCulture.DateTimeFormat.FirstDayOfWeek);
        }

        public static int GetWeekCount(DateTime date)
        {
            int[] array = new int[71]
	        {
		        2004,
		        2009,
		        2015,
		        2020,
		        2026,
		        2032,
		        2037,
		        2043,
		        2048,
		        2054,
		        2060,
		        2065,
		        2071,
		        2076,
		        2082,
		        2088,
		        2093,
		        2099,
		        2105,
		        2111,
		        2116,
		        2122,
		        2128,
		        2133,
		        2139,
		        2144,
		        2150,
		        2156,
		        2161,
		        2167,
		        2172,
		        2178,
		        2184,
		        2189,
		        2195,
		        2201,
		        2207,
		        2212,
		        2218,
		        2224,
		        2229,
		        2235,
		        2240,
		        2246,
		        2252,
		        2257,
		        2263,
		        2268,
		        2274,
		        2280,
		        2285,
		        2291,
		        2296,
		        2303,
		        2308,
		        2314,
		        2320,
		        2325,
		        2331,
		        2336,
		        2342,
		        2348,
		        2353,
		        2359,
		        2364,
		        2370,
		        2376,
		        2381,
		        2387,
		        2392,
		        2398
	        };

            if (Array.IndexOf(array, date.Year) == -1)
                return 52;

            return 53;
        }


        private static DateTime getAstronomicalSpring(DateTime date)
        {
            double dY = (double)(date.Year - 2000) / 1000D;
            return JulianToGregorian(2451623.80984 + 365242.37404 * dY + 0.05169 * Math.Pow(dY, 2) - 0.00411 * Math.Pow(dY, 3) - 0.00057 * Math.Pow(dY, 4));
        }

        private static DateTime getAstronomicalSummer(DateTime date)
        {
            double dY = (double)(date.Year - 2000) / 1000D;
            return JulianToGregorian(2451716.56767 + 365241.62603 * dY + 0.00325 * Math.Pow(dY, 2) + 0.00888 * Math.Pow(dY, 3) - 0.00030 * Math.Pow(dY, 4));
        }

        private static DateTime getAstronomicalAutumn(DateTime date)
        {
            double dY = (double)(date.Year - 2000) / 1000D;
            return JulianToGregorian(2451810.21715 + 365242.01767 * dY - 0.11575 * Math.Pow(dY, 2) + 0.00337 * Math.Pow(dY, 3) + 0.00078 * Math.Pow(dY, 4));
        }

        private static DateTime getAstronomicalWinter(DateTime date)
        {
            double dY = (double)(date.Year - 2000) / 1000D;
            return JulianToGregorian(2451900.05952 + 365242.74049 * dY - 0.06223 * Math.Pow(dY, 2) - 0.00823 * Math.Pow(dY, 3) + 0.00032 * Math.Pow(dY, 4));
        }
    }
}
