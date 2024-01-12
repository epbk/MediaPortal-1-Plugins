using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using NLog;
using NLog.Config;
using NLog.Targets;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.Profile;
using MediaPortal.Configuration;
using MediaPortal.Services;
using MediaPortal.Player;
using MediaPortal.Localisation;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using System.Reflection;
#if MS_DIRECTX
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
#else
using SharpDX.Direct3D9;
#endif

namespace MediaPortal.Plugins.WorldWeatherLite
{
    [PluginIcons("MediaPortal.Plugins.WorldWeatherLite.Graphics.WorldWeather-enabled.png", "MediaPortal.Plugins.WorldWeatherLite.Graphics.WorldWeather-disabled.png")]
    public class GUIWorldWeaterLite : GUIWindow, ISetupForm, IPluginReceiver
    {
        #region Constants
        internal const int PLUGIN_ID = 7977;
        internal const string PLUGIN_NAME = "WorldWeather";

        internal const string PLUGIN_TITLE = "World Weather";

        private const int _REFRESH_INTERVAL = 30 * 60; //sec


        private const string _GEOCLOCK_IMAGE_ID = "[" + PLUGIN_NAME + ":MediaPortalWorldWeatherGeoClock]";
        private const string _MOON_IMAGE_ID = "[" + PLUGIN_NAME + ":MediaPortalWorldWeatherMoon]";

        private const int WM_POWERBROADCAST = 0x0218;
        private const int PBT_APMQUERYSUSPEND = 0x0000;
        private const int PBT_APMQUERYSTANDBY = 0x0001;
        private const int PBT_APMQUERYSUSPENDFAILED = 0x0002;
        private const int PBT_APMQUERYSTANDBYFAILED = 0x0003;
        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMSTANDBY = 0x0005;
        private const int PBT_APMRESUMECRITICAL = 0x0006;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMRESUMESTANDBY = 0x0008;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;


        #region Tags
        private const string _TAG_PREFFIX = "#" + PLUGIN_NAME;

        private const string _TAG_VIEW = _TAG_PREFFIX + ".View"; //[Condition,Astronomy,GeoClock,Image,Calendar]
        private const string _TAG_VIEW_IMAGE = _TAG_PREFFIX + ".ImageView";
        private const string _TAG_VIEW_CONDITION = _TAG_PREFFIX + ".ConditionView";  //[Normal,History,Graphic]

        private const string _TAG_IMAGE_SELECTED_LABEL = _TAG_PREFFIX + ".ImageSelectedLabel";

        private const string _TAG_PROVIDER = _TAG_PREFFIX + ".Provider";
        private const string _TAG_PROVIDER_IMAGE = _TAG_PREFFIX + ".ProviderImage";
        private const string _TAG_FORECAST_PROVIDER = _TAG_PREFFIX + ".ForecastProvider"; //[foreca,accuweather,previmeteo,Yahoo,weather2,weatherbug,weather underground,openweathermap,wetter.com]
        private const string _TAG_FORECAST_PROVIDER_IMAGE = _TAG_PREFFIX + ".ForecastProviderImage";


        private const string _TAG_FEED_ENABLED = _TAG_PREFFIX + ".FeedEnabled";

        private const string _TAG_GEOCLOCK_IMAGE = _TAG_PREFFIX + ".ImageGeoClock";

        private const string _TAG_REFRESH_DATE = _TAG_PREFFIX + ".RefreshDate";
        private const string _TAG_REFRESH_TIME = _TAG_PREFFIX + ".RefreshTime";

        private const string _TAG_TODAY_TEMPERATURE = _TAG_PREFFIX + ".TodayTemperature";
        private const string _TAG_TODAY_TEMPERATURE_FEELSLIKE = _TAG_PREFFIX + ".TodayTemperatureFeelsLike";
        private const string _TAG_TODAY_WIND_SPEED = _TAG_PREFFIX + ".TodayWindSpeed";
        private const string _TAG_TODAY_WIND_DIRECTION = _TAG_PREFFIX + ".TodayWindDirection";
        private const string _TAG_TODAY_WIND_DIRECTION_IMAGE = _TAG_PREFFIX + ".TodayWindDirectionImage";
        private const string _TAG_TODAY_WIND_DIRECTION_DEGREE = _TAG_PREFFIX + ".TodayWindDirectionDegree";
        private const string _TAG_TODAY_HUMIDITY = _TAG_PREFFIX + ".TodayHumidity";
        private const string _TAG_TODAY_ICON_NUMBER = _TAG_PREFFIX + ".TodayIconNumber";
        private const string _TAG_TODAY_ICON_NUMBER2 = _TAG_PREFFIX + ".TodayIconNumberV2";
        private const string _TAG_TODAY_ICON_IMAGE = _TAG_PREFFIX + ".TodayIconImage";
        private const string _TAG_TODAY_ICON_IMAGE2 = _TAG_PREFFIX + ".TodayIconImageV2";
        private const string _TAG_TODAY_CONDITION = _TAG_PREFFIX + ".TodayCondition";
        private const string _TAG_TODAY_CLOUD_COVERAGE = _TAG_PREFFIX + ".TodayCloudCoverage";
        private const string _TAG_TODAY_DEWPOINT = _TAG_PREFFIX + ".TodayDewPoint";
        private const string _TAG_TODAY_VISIBILITY = _TAG_PREFFIX + ".TodayVisibility";
        private const string _TAG_TODAY_PRESSURE = _TAG_PREFFIX + ".TodayPressure";
        private const string _TAG_TODAY_PRESSURE_BAROMETRIC = _TAG_PREFFIX + ".TodayBarometricPressure";
        private const string _TAG_TODAY_PRECIPITATION = _TAG_PREFFIX + ".TodayPrecipitation";
        private const string _TAG_TODAY_UV_INDEX = _TAG_PREFFIX + ".TodayUVIndex";


        private const string _TAG_TODAY_HISTORY_DAY_RECORD_MAX_TEMPERATURE = _TAG_PREFFIX + ".HistoryDayRecordMaxTemperature";
        private const string _TAG_TODAY_HISTORY_DAY_RECORD_MIN_TEMPERATURE = _TAG_PREFFIX + ".HistoryDayRecordMinTemperature";

        private const string _TAG_TODAY_HISTORY_DAY_RECORD_MAX_TEMPERATURE_YEAR = _TAG_PREFFIX + ".HistoryDayRecordMaxTemperatureYear";
        private const string _TAG_TODAY_HISTORY_DAY_RECORD_MIN_TEMPERATURE_YEAR = _TAG_PREFFIX + ".HistoryDayRecordMinTemperatureYear";

        private const string _TAG_TODAY_HISTORY_DAY_AVERAGE_MAX_TEMPERATURE = _TAG_PREFFIX + ".HistoryDayAverageMaxTemperature";
        private const string _TAG_TODAY_HISTORY_DAY_AVERAGE_MIN_TEMPERATURE = _TAG_PREFFIX + ".HistoryDayAverageMinTemperaturer";

        #region Loaction
        private const string _TAG_LOCATION = _TAG_PREFFIX + ".Location";
        private const string _TAG_LOCATION_COORDINATES = _TAG_PREFFIX + ".LocationCoordinates";
        private const string _TAG_LOCATION_COORDINATES_DEGREE = _TAG_PREFFIX + ".LocationCoordinatesDegree";
        private const string _TAG_LOCATION_DESCRIPTION = _TAG_PREFFIX + ".LocationDescription";
        private const string _TAG_LOCATION_CITY_CODE = _TAG_PREFFIX + ".LocationCityCode";
        private const string _TAG_LOCATION_POSTAL_CODE = _TAG_PREFFIX + ".LocationPostalCode";
        private const string _TAG_LOCATION_COUNTRY = _TAG_PREFFIX + ".LocationCountry";
        private const string _TAG_LOCATION_REGION = _TAG_PREFFIX + ".LocationRegion";
        private const string _TAG_LOCATION_STATION = _TAG_PREFFIX + ".LocationStation";
        private const string _TAG_LOCATION_UNIT_SYSTEM = _TAG_PREFFIX + ".LocationUnitSystem";
        private const string _TAG_LOCATION_DAYLIGHT_DESCRIPTION = _TAG_PREFFIX + ".LocationDaylightDescription";
        private const string _TAG_LOCATION_DAYLIGHT_START = _TAG_PREFFIX + ".LocationDaylightStart";
        private const string _TAG_LOCATION_DAYLIGHT_END = _TAG_PREFFIX + ".LocationDaylightEnd";
        private const string _TAG_LOCATION_TIME_ZONE_DESCRIPTION = _TAG_PREFFIX + ".LocationTimezoneDescription";
        private const string _TAG_LOCATION_CIVIL_TWILIGHT_MORNING_TIME = _TAG_PREFFIX + ".LocationCivilTwilightMorningTime";
        private const string _TAG_LOCATION_CIVIL_TWILIGHT_EVENING_TIME = _TAG_PREFFIX + ".LocationCivilTwilightEveningTime";
        private const string _TAG_LOCATION_NAUTICAL_TWILIGHT_MORNING_TIME = _TAG_PREFFIX + ".LocationNauticalTwilightMorningTime";
        private const string _TAG_LOCATION_NAUTICAL_TWILIGHT_EVENING_TIME = _TAG_PREFFIX + ".LocationNauticalTwilightEveningTime";
        private const string _TAG_LOCATION_ASTRONOMICAL_TWILIGHT_MORNING_TIME = _TAG_PREFFIX + ".LocationAstronomicalTwilightMorningTime";
        private const string _TAG_LOCATION_ASTRONOMICAL_TWILIGHT_EVENING_TIME = _TAG_PREFFIX + ".LocationAstronomicalTwilightEveningTime";

        private const string _TAG_LOCATION_MOON_PHASE = _TAG_PREFFIX + ".LocationMoonPhase";
        private const string _TAG_LOCATION_MOON_PHASE_IMAGE = _TAG_PREFFIX + ".LocationMoonPhaseImage";
        private const string _TAG_LOCATION_MOON_RISE_TIME = _TAG_PREFFIX + ".LocationMoonriseTime";
        private const string _TAG_LOCATION_MOON_SET_TIME = _TAG_PREFFIX + ".LocationMoonsetTime";
        private const string _TAG_LOCATION_MOON_CULMINATION_TIME = _TAG_PREFFIX + ".LocationMoonCulminationTime";
        private const string _TAG_LOCATION_MOON_ALTITUDE = _TAG_PREFFIX + ".LocationMoonAltitude";
        private const string _TAG_LOCATION_MOON_AZIMUTH = _TAG_PREFFIX + ".LocationMoonAzimuth";
        private const string _TAG_LOCATION_MOON_DIAMETER = _TAG_PREFFIX + ".LocationMoonDiameter";
        private const string _TAG_LOCATION_MOON_DISTANCE = _TAG_PREFFIX + ".LocationMoonDistance";

        private const string _TAG_LOCATION_SUNSHINE_DURATION = _TAG_PREFFIX + ".LocationSunshineDuration";
        private const string _TAG_LOCATION_SUN_RISE_TIME = _TAG_PREFFIX + ".LocationSunriseTime";
        private const string _TAG_LOCATION_SUN_SET_TIME = _TAG_PREFFIX + ".LocationSunsetTime";
        private const string _TAG_LOCATION_SUN_CULMINATION_TIME = _TAG_PREFFIX + ".LocationSunCulminationTime";
        private const string _TAG_LOCATION_SUN_ALTITUDE = _TAG_PREFFIX + ".LocationSunAltitude";
        private const string _TAG_LOCATION_SUN_AZIMUTH = _TAG_PREFFIX + ".LocationSunAzimuth";
        private const string _TAG_LOCATION_SUN_DIAMETER = _TAG_PREFFIX + ".LocationSunDiameter";
        private const string _TAG_LOCATION_SUN_DISTANCE = _TAG_PREFFIX + ".LocationSunDistance";

        private const string _TAG_LOCATION_FEED_0_TEXT = _TAG_PREFFIX + ".Feed0Text";
        private const string _TAG_LOCATION_FEED_0_DESCRIPTION = _TAG_PREFFIX + ".Feed0Description";
        private const string _TAG_LOCATION_FEED_1_TEXT = _TAG_PREFFIX + ".Feed1Text";
        private const string _TAG_LOCATION_FEED_1_DESCRIPTION = _TAG_PREFFIX + ".Feed1Description";
        private const string _TAG_LOCATION_FEED_2_TEXT = _TAG_PREFFIX + ".Feed2Text";
        private const string _TAG_LOCATION_FEED_2_DESCRIPTION = _TAG_PREFFIX + ".Feed2Description";

        #endregion

        #region Calendar

        private const string _TAG_CALENDAR_ENABLED = _TAG_PREFFIX + ".CalendarEnabled";

        private const string _TAG_CALENDAR_NEW_YEAR = _TAG_PREFFIX + ".CalendarNewYear";
        private const string _TAG_CALENDAR_NEW_YEAR_WEEKDAY = _TAG_PREFFIX + ".CalendarNewYearWeekDay";
        private const string _TAG_CALENDAR_EPIPHANY = _TAG_PREFFIX + ".CalendarEpiphany";
        private const string _TAG_CALENDAR_EPIPHANY_WEEK_DAY = _TAG_PREFFIX + ".CalendarEpiphanyWeekDay";
        private const string _TAG_CALENDAR_ASSUMPTION_DAY = _TAG_PREFFIX + ".CalendarAssumptionDay";
        private const string _TAG_CALENDAR_ASSUMPTION_DAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarAssumptionDayWeekDay";
        private const string _TAG_CALENDAR_REFORMATION_DAY = _TAG_PREFFIX + ".CalendarReformationDay";
        private const string _TAG_CALENDAR_REFORMATION_DAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarReformationDayWeekDay";
        private const string _TAG_CALENDAR_ALL_SAINTS_DAY = _TAG_PREFFIX + ".CalendarAllSaintsDay";
        private const string _TAG_CALENDAR_ALL_SAINTS_DAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarAllSaintsDayWeekDay";
        private const string _TAG_CALENDAR_EASTER = _TAG_PREFFIX + ".CalendarEasterSunday";
        private const string _TAG_CALENDAR_EASTER_WEEK_DAY = _TAG_PREFFIX + ".CalendarEasterSundayWeekDay";
        private const string _TAG_CALENDAR_HOLY_THURSDAY = _TAG_PREFFIX + ".CalendarHolyThursday";
        private const string _TAG_CALENDAR_HOLY_THURSDAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarHolyThursdayWeekDay";
        private const string _TAG_CALENDAR_GOOD_FRIDAY = _TAG_PREFFIX + ".CalendarGoodFriday";
        private const string _TAG_CALENDAR_GOOD_FRIDAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarGoodFridayWeekDay";
        private const string _TAG_CALENDAR_ASCENSION_DAY = _TAG_PREFFIX + ".CalendarAscensionDay";
        private const string _TAG_CALENDAR_ASCENSION_DAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarAscensionDayWeekDay";
        private const string _TAG_CALENDAR_WHIT_SUNDAY = _TAG_PREFFIX + ".CalendarWhitSunday";
        private const string _TAG_CALENDAR_WHIT_SUNDAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarWhitSundayWeekDay";
        private const string _TAG_CALENDAR_CORPUS_CHRISTI = _TAG_PREFFIX + ".CalendarCorpusChristi";
        private const string _TAG_CALENDAR_CORPUS_CHRISTI_WEEK_DAY = _TAG_PREFFIX + ".CalendarCorpusChristiWeekDay";
        private const string _TAG_CALENDAR_CHRISTMAS_DAY = _TAG_PREFFIX + ".CalendarChristmasDay";
        private const string _TAG_CALENDAR_CHRISTMAS_DAY_WEEK_DAY = _TAG_PREFFIX + ".CalendarChristmasDayWeekDay";

        private const string _TAG_CALENDAR_SPRING_SEASON = _TAG_PREFFIX + ".CalendarSpringSeason";
        private const string _TAG_CALENDAR_SPRING_SEASON_WEEK_DAY = _TAG_PREFFIX + ".CalendarSpringSeasonWeekDay";
        private const string _TAG_CALENDAR_SUMMER_SEASON = _TAG_PREFFIX + ".CalendarSummerSeason";
        private const string _TAG_CALENDAR_SUMMER_SEASON_WEEK_DAY = _TAG_PREFFIX + ".CalendarSummerSeasonWeekDay";
        private const string _TAG_CALENDAR_AUTUMN_SEASON = _TAG_PREFFIX + ".CalendarAutumnSeason";
        private const string _TAG_CALENDAR_AUTUMN_SEASON_WEEK_DAY = _TAG_PREFFIX + ".CalendarAutumnSeasonWeekDay";
        private const string _TAG_CALENDAR_WINTER_SEASON = _TAG_PREFFIX + ".CalendarWinterSeason";
        private const string _TAG_CALENDAR_WINTER_SEASON_WEEK_DAY = _TAG_PREFFIX + ".CalendarWinterSeasonWeekDay";

        private const string _TAG_CALENDAR_JULIAN_DATE = _TAG_PREFFIX + ".CalendarJulianDate";
        private const string _TAG_CALENDAR_CALENDAR_DAY = _TAG_PREFFIX + ".CalendarDay";
        private const string _TAG_CALENDAR_DAY_COUNT = _TAG_PREFFIX + ".CalendarDayCount";
        private const string _TAG_CALENDAR_WEEK = _TAG_PREFFIX + ".CalendarWeek";
        private const string _TAG_CALENDAR_WEEK_COUNT = _TAG_PREFFIX + ".CalendarWeekCount";

        private const string _TAG_CALENDAR_SELF_DEFINED_DAY_DESCRIPTION = _TAG_PREFFIX + ".CalendarSelfDefined{0}DayDescription";
        private const string _TAG_CALENDAR_SELF_DEFINED_DAY = _TAG_PREFFIX + ".CalendarSelfDefined{0}Day";
        private const string _TAG_CALENDAR_SELF_DEFINED_WEEK_DAY = _TAG_PREFFIX + ".CalendarSelfDefined{0}WeekDay";

        #endregion

        #region Translation
        private const string _TAG_TRANSLATION_PROVIDER = _TAG_PREFFIX + ".TranslationProvider";
        private const string _TAG_TRANSLATION_NOPROVIDER = _TAG_PREFFIX + ".TranslationNoProvider";
        private const string _TAG_TRANSLATION_REFRESH = _TAG_PREFFIX + ".TranslationRefresh";
        private const string _TAG_TRANSLATION_FEED = _TAG_PREFFIX + ".TranslationFeed";
        private const string _TAG_TRANSLATION_CURRENT_CONDITION = _TAG_PREFFIX + ".TranslationCurrentCondition";
        private const string _TAG_TRANSLATION_FORECAST_CONDITION = _TAG_PREFFIX + ".TranslationForecastCondition";
        private const string _TAG_TRANSLATION_REFRESH_DATETIME = _TAG_PREFFIX + ".TranslationRefreshDateTime";
        private const string _TAG_TRANSLATION_DAY = _TAG_PREFFIX + ".TranslationDay";
        private const string _TAG_TRANSLATION_WEEK = _TAG_PREFFIX + ".TranslationWeek";
        private const string _TAG_TRANSLATION_GEOCLOCK = _TAG_PREFFIX + ".TranslationGeoClock";

        private const string _TAG_TRANSLATION_MOON_PHASE = _TAG_PREFFIX + ".TranslationMoonPhase";
        private const string _TAG_TRANSLATION_MOON_SET_TIME = _TAG_PREFFIX + ".TranslationMoonset";
        private const string _TAG_TRANSLATION_MOON_RISE_TIME = _TAG_PREFFIX + ".TranslationMoonrise";
        private const string _TAG_TRANSLATION_MOON_CULMINATION = _TAG_PREFFIX + ".TranslationMoonCulmination";
        private const string _TAG_TRANSLATION_MOON_ALTITUDE = _TAG_PREFFIX + ".TranslationMoonAltitude";
        private const string _TAG_TRANSLATION_MOON_AZIMUTH = _TAG_PREFFIX + ".TranslationMoonAzimuth";
        private const string _TAG_TRANSLATION_MOON_DIAMETER = _TAG_PREFFIX + ".TranslationMoonDiameter";
        private const string _TAG_TRANSLATION_MOON_DISTANCE = _TAG_PREFFIX + ".TranslationMoonDistance";

        private const string _TAG_TRANSLATION_SUN_CULMINATION = _TAG_PREFFIX + ".TranslationSunCulmination";
        private const string _TAG_TRANSLATION_SUN_ALTITUDE = _TAG_PREFFIX + ".TranslationSunAltitude";
        private const string _TAG_TRANSLATION_SUN_AZIMUTH = _TAG_PREFFIX + ".TranslationSunAzimuth";
        private const string _TAG_TRANSLATION_SUN_DIAMETER = _TAG_PREFFIX + ".TranslationSunDiameter";
        private const string _TAG_TRANSLATION_SUN_DISTANCE = _TAG_PREFFIX + ".TranslationSunDistance";

        private const string _TAG_TRANSLATION_CIVIL_TWILIGHT_MORNING = _TAG_PREFFIX + ".TranslationCivilTwilightMorning";
        private const string _TAG_TRANSLATION_CIVIL_TWILIGHT_EVENING = _TAG_PREFFIX + ".TranslationCivilTwilightEvening";

        private const string _TAG_TRANSLATION_VISIBILITY = _TAG_PREFFIX + ".TranslationVisibility";
        private const string _TAG_TRANSLATION_PRECIPITATION = _TAG_PREFFIX + ".TranslationPrecipitation";
        private const string _TAG_TRANSLATION_HUMIDITY = _TAG_PREFFIX + ".TranslationHumidity";
        private const string _TAG_TRANSLATION_PRESSURE = _TAG_PREFFIX + ".TranslationPressure";
        private const string _TAG_TRANSLATION_SUNRISE = _TAG_PREFFIX + ".TranslationSunrise";
        private const string _TAG_TRANSLATION_SUNSET = _TAG_PREFFIX + ".TranslationSunset";
        private const string _TAG_TRANSLATION_DEW_POINT = _TAG_PREFFIX + ".TranslationDewPoint";
        private const string _TAG_TRANSLATION_WIND = _TAG_PREFFIX + ".TranslationWind";
        private const string _TAG_TRANSLATION_WIND_SPEED = _TAG_PREFFIX + ".TranslationWindSpeed";
        private const string _TAG_TRANSLATION_CONDITION = _TAG_PREFFIX + ".TranslationCondition";
        private const string _TAG_TRANSLATION_TEMPERATURE = _TAG_PREFFIX + ".TranslationTemperature";
        private const string _TAG_TRANSLATION_TEMPERATURE_RECORD = _TAG_PREFFIX + ".TranslationTemperatureRecord";
        private const string _TAG_TRANSLATION_TEMPERATURE_AVERAGE = _TAG_PREFFIX + ".TranslationTemperatureAverage";
        private const string _TAG_TRANSLATION_TEMPERATURE_FEELS_LIKE = _TAG_PREFFIX + ".TranslationTemperatureFeelsLike";
        private const string _TAG_TRANSLATION_TEMPERATURE_LOW = _TAG_PREFFIX + ".TranslationTemperatureLow";
        private const string _TAG_TRANSLATION_TEMPERATURE_HIGH = _TAG_PREFFIX + ".TranslationTemperatureHigh";
        private const string _TAG_TRANSLATION_CLOUD_COVERAGE = _TAG_PREFFIX + ".TranslationCloudCoverage";
        private const string _TAG_TRANSLATION_FOG_COVERAGE = _TAG_PREFFIX + ".TranslationFogCoverage";
        private const string _TAG_TRANSLATION_BAROMETRIC_PRESSURE = _TAG_PREFFIX + ".TranslationBarometricPressure";
        private const string _TAG_TRANSLATION_UV_INDEX = _TAG_PREFFIX + ".TranslationUVIndex";
        private const string _TAG_TRANSLATION_HEAT_INDEX = _TAG_PREFFIX + ".TranslationHeatIndex";
        private const string _TAG_TRANSLATION_DAYLIGHT = _TAG_PREFFIX + ".TranslationDaylight";
        private const string _TAG_TRANSLATION_SUNSHINE_DURATION = _TAG_PREFFIX + ".TranslationSunshineDuration";
        private const string _TAG_TRANSLATION_HISTORY_YEAR_CONDITION = _TAG_PREFFIX + ".TranslationHistoryYearCondition";
        private const string _TAG_TRANSLATION_HISTORY_DAY_CONDITION = _TAG_PREFFIX + ".TranslationHistoryDayCondition";

        private const string _TAG_TRANSLATION_WORLD_IMAGE = _TAG_PREFFIX + ".TranslationWorldImage";
        private const string _TAG_TRANSLATION_SATELLITE_IMAGE = _TAG_PREFFIX + ".TranslationSatelliteImage";
        private const string _TAG_TRANSLATION_TEMPERATURE_IMAGE = _TAG_PREFFIX + ".TranslationTemperatureImage";
        private const string _TAG_TRANSLATION_UV_INDEX_IMAGE = _TAG_PREFFIX + ".TranslationUVIndexImage";
        private const string _TAG_TRANSLATION_UV_WIND_IMAGE = _TAG_PREFFIX + ".TranslationWindImage";
        private const string _TAG_TRANSLATION_PRECIPITATION_IMAGE = _TAG_PREFFIX + ".TranslationPrecipitationImage";
        private const string _TAG_TRANSLATION_HUMIDITY_IMAGE = _TAG_PREFFIX + ".TranslationHumidityImage";
        private const string _TAG_TRANSLATION_SUNTIME_IMAGE = _TAG_PREFFIX + ".TranslationSuntimeImage";
        private const string _TAG_TRANSLATION_POLLEN_COUNT_IMAGE = _TAG_PREFFIX + ".TranslationPollenCountImage";
        private const string _TAG_TRANSLATION_WEBCAM_IMAGE = _TAG_PREFFIX + ".TranslationWebcamImage";
        private const string _TAG_TRANSLATION_SELF_DEFINED_IMAGE = _TAG_PREFFIX + ".TranslationSelfDefinedImage";

        private const string _TAG_TRANSLATION_PICTURE_OF_DAY = _TAG_PREFFIX + ".TranslationPictureOfDay";
        private const string _TAG_TRANSLATION_CHART = _TAG_PREFFIX + ".TranslationChart";
        private const string _TAG_TRANSLATION_ZODIAC = _TAG_PREFFIX + ".TranslationZodiac";
        private const string _TAG_TRANSLATION_STARRY_SKY = _TAG_PREFFIX + ".TranslationStarrySky";
        private const string _TAG_TRANSLATION_HOLIDAY = _TAG_PREFFIX + ".TranslationHoliday";
        private const string _TAG_TRANSLATION_SEASON = _TAG_PREFFIX + ".TranslationSeason";
        private const string _TAG_TRANSLATION_NEW_YEAR = _TAG_PREFFIX + ".TranslationNewYear";
        private const string _TAG_TRANSLATION_EPIPHANY = _TAG_PREFFIX + ".TranslationEpiphany";
        private const string _TAG_TRANSLATION_HOLY_THURSDAY = _TAG_PREFFIX + ".TranslationHolyThursday";
        private const string _TAG_TRANSLATION_GOOD_FRIDAY = _TAG_PREFFIX + ".TranslationGoodFriday";
        private const string _TAG_TRANSLATION_EASTER_SUNDAY = _TAG_PREFFIX + ".TranslationEasterSunday";
        private const string _TAG_TRANSLATION_ASCENSION_DAY = _TAG_PREFFIX + ".TranslationAscensionDay";
        private const string _TAG_TRANSLATION_WHIT_SUNDAY = _TAG_PREFFIX + ".TranslationWhitSunday";
        private const string _TAG_TRANSLATION_CORPUS_CHRISTI = _TAG_PREFFIX + ".TranslationCorpusChristi";
        private const string _TAG_TRANSLATION_ASSUMPTION_DAY = _TAG_PREFFIX + ".TranslationAssumptionDay";
        private const string _TAG_TRANSLATION_REFORMATION_DAY = _TAG_PREFFIX + ".TranslationReformationDay";
        private const string _TAG_TRANSLATION_ALL_SAINTS_DAY = _TAG_PREFFIX + ".TranslationAllSaintsDay";
        private const string _TAG_TRANSLATION_CHRISTMAS_DAY = _TAG_PREFFIX + ".TranslationChristmasDay";
        private const string _TAG_TRANSLATION_SPRING_SEASON = _TAG_PREFFIX + ".TranslationSpringSeason";
        private const string _TAG_TRANSLATION_SUMMER_SEASON = _TAG_PREFFIX + ".TranslationSummerSeason";
        private const string _TAG_TRANSLATION_AUTUMN_SEASON = _TAG_PREFFIX + ".TranslationAutumnSeason";
        private const string _TAG_TRANSLATION_WINTER_SEASON = _TAG_PREFFIX + ".TranslationWinterSeason";

        #endregion

        #endregion

        //0-4
        //#WorldWeather.HistoryYear0Year
        //#WorldWeather.HistoryYear0Day
        //#WorldWeather.HistoryYear0Date
        //#WorldWeather.HistoryYear0IconImage
        //#WorldWeather.HistoryYear0Condition
        //#WorldWeather.HistoryYear0MinTemperature
        //#WorldWeather.HistoryYear0MaxTemperature
        //#WorldWeather.HistoryYear0MinWindSpeed
        //#WorldWeather.HistoryYear0MaxWindSpeed
        //#WorldWeather.HistoryYear0MinPressure
        //#WorldWeather.HistoryYear0MaxPressure
        //#WorldWeather.HistoryYear0Precipitation

        //#WorldWeather.HistoryYearTemperatureChart
        //#WorldWeather.HistoryYearPressureChart
        //#WorldWeather.HistoryYearHumidityChart
        //#WorldWeather.HistoryProviderImage

        #endregion

        #region Types
        private enum ViewMode
        {
            Condition,
            Image,
            GeoClock,
            Astronomy,
            Calendar
        }
        #endregion

        #region Private Fields

        private static NLog.Logger _Logger;

        private Database.dbSettings _Settings;

        //private Dictionary<string, string> _Translation = new Dictionary<string, string>();

        private int _WeatherIsRefreshing = 0;
        private int _LocationIsRefreshing = 0;
        
        private System.Timers.Timer _TimerRefreshWeather;
        private System.Timers.Timer _TimerRefreshLocation;
        
        private bool _SystemSuspended = false;

        private Utils.GeoClock _GeoClock;
        private DateTime _GeoClockLastRefresh = DateTime.MinValue;
        private string _GeoClockImagePath = null;

        private DateTime _MoonImageLastRefresh = DateTime.MinValue;
        private string _MoonImagePath = null;

        private ViewMode _ViewMode = ViewMode.Condition;
        private ImageViewModeEnum _ImageViewMode = ImageViewModeEnum.Flat;

        private GUI.GUIWeatherImage[] _WaeatherImages = new GUI.GUIWeatherImage[11];

        private MediaPortal.Pbk.Tasks.TaskQueue _ImageRefreshPool = new Pbk.Tasks.TaskQueue(
            PLUGIN_NAME,
            () => new Pbk.Net.Http.HttpUserWebRequest() { BeforeDownload = cbHttpBeforeDownload, ResponseTimeout = 30000 },
            (o) => ((Pbk.Net.Http.HttpUserWebRequest)o).Close()
            );

        private bool _FullScreenMediaViewMode = false;
        private GUI.GUIWeatherImage _FullScreenMediaImage = null;


        private Database.dbWeatherLoaction _WeatherLocation;
        private List<Providers.IWeatherProvider> _Providers = new List<Providers.IWeatherProvider>();

        private LocalisationProvider _Localisation;

        private static readonly string[] _WindDirections = new string[]
        {
            "north",
            "northnortheast",
            "northeast",
            "eastnortheast",
            "east",
            "eastsoutheast",
            "southeast",
            "southsoutheast",
            "south",
            "southsouthwest",
            "southwest",
            "westsouthwest",
            "west",
            "westnorthwest",
            "northwest",
            "northnorthwest"
        };

        [SkinControl(2)]
        protected GUIButtonControl _GUIbuttonDisplay = null;

        [SkinControl(3)]
        protected GUIButtonControl _GUIbuttonLocation = null;

        [SkinControl(4)]
        protected GUIButtonControl _GUIbuttonBrowserMap = null;

        [SkinControl(5)]
        protected GUIButtonControl _GUIbuttonView = null;

        [SkinControl(9)]
        protected GUIButtonControl _GUIbuttonRefresh = null;

        [SkinControl(200005)]
        protected GUIButtonControl _GUIbuttonFeedView = null;

        [SkinControl(50)]
        protected GUIFacadeControl _GUIfacadeList = null;

        #endregion

        #region ctor
        public GUIWorldWeaterLite()
        {
            MediaPortal.Pbk.Logging.Log.Init();
        }
        #endregion

        #region Overrides

        public override bool Init()
        {
            Logging.Log.Init();
            _Logger = LogManager.GetCurrentClassLogger();

            _Logger.Info("Plugin has starded. v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            //Init localisation provider
            this._Localisation = Language.Translation.GetLocalisationProvider(PLUGIN_NAME);

            //Load settings
            this._Settings = Database.dbSettings.Instance;

            this._WeatherLocation = Database.dbWeatherLoaction.Get(this._Settings.ProfileID);
            if (this._Settings.ProfileID != this._WeatherLocation.ID)
                this._Settings.ProfileID = (int)this._WeatherLocation.ID;


            this._Providers.Add(new Providers.ProviderMsn());
            this._Providers.Add(new Providers.ProviderForeca());
            this._Providers.Add(new Providers.ProviderAccuWeather());

            _Logger.Debug("[init] Provider:{4}  Name:'{0}' ID:'{1}' LAT:{2} LON:{3}",
                this._WeatherLocation.Name,
                this._WeatherLocation.LocationID,
                this._WeatherLocation.Latitude,
                this._WeatherLocation.Longitude,
                this._WeatherLocation.Provider);

            if (this._Settings.DatabaseVersion < Database.dbSettings.DATABASE_VERSION_CURRENT)
            {
                Database.dbWeatherImage.Manager.Get<Database.dbWeatherImage>(null).ForEach(o =>
                    {
                        if (!string.IsNullOrWhiteSpace(o.Url))
                        {
                            o.Enable = true;
                            o.CommitNeeded = true;
                            o.Commit();
                        }
                    });
                this._Settings.DatabaseVersion = Database.dbSettings.DATABASE_VERSION_CURRENT;
                this._Settings.CommitNeeded = true;
                this._Settings.Commit();
            }

            this._ImageViewMode = this._Settings.ImageViewMode;

            //Hook to the window mode change
            GUIGraphicsContext.OnVideoWindowChanged += this.cbVideoWindowChanged;

            //GeoClock init
            this._GeoClock = new Utils.GeoClock(Config.GetFolder(Config.Dir.Skin) + '\\' + Config.SkinName);

            #region Init weather imgaes
            List<Database.dbWeatherImage> list = Database.dbWeatherImage.GetAll();
            for (int i = 0; i < list.Count; i++)
            {
                Database.dbWeatherImage dbImg = list[i];
                if (dbImg.Enable && !string.IsNullOrWhiteSpace(dbImg.Url))
                {
                    GUI.GUIWeatherImage wi = new GUI.GUIWeatherImage(
                        dbImg,
                        "[" + PLUGIN_NAME + ":MediaPortalWorldWeatherImage" + i + "]",
                        _TAG_PREFFIX + ".ImageWeather" + i);

                    this._WaeatherImages[i] = wi;

                    _Logger.Debug("[init] MediaWeatherImage:{0} '{1}'\r\nUrl: {2}\r\nUrlBck: {3}\r\nUrlOver: {4}\r\nPeriod: {5}\r\nPeriodSafe: {6}\r\nMultiimage: {7}\r\nDate&Time Watermark: {8}\r\n",
                        wi.Id,
                        dbImg.Description,
                        dbImg.Url,
                        dbImg.UrlBackground,
                        dbImg.UrlOverlay,
                        dbImg.Period,
                        dbImg.PeriodSafe,
                        dbImg.MultiImage,
                        dbImg.MultiImageDateImeWatermark);
                }
            }
            #endregion

            //Init tags
            tagsInit();
            
            //Refresh timer init
            this._TimerRefreshWeather = new System.Timers.Timer();
            this._TimerRefreshWeather.Interval = 1000;
            this._TimerRefreshWeather.AutoReset = false;
            this._TimerRefreshWeather.Elapsed += this.cbTimerRefreshWeather;
            this._TimerRefreshWeather.Enabled = true;

            //Refresh location timer init
            this._TimerRefreshLocation = new System.Timers.Timer();
            this._TimerRefreshLocation.Interval = 60000;
            this._TimerRefreshLocation.AutoReset = true;
            this._TimerRefreshLocation.Elapsed += this.cbTimerRefreshLocation;
            this._TimerRefreshLocation.Enabled = false;

            this.doLocationRefresh(false);

            return Load(GUIGraphicsContext.Skin + "\\WorldWeather.xml");
        }

        public override void DeInit()
        {
            //Commit settings
            this._Settings.ImageViewMode = this._ImageViewMode;

            this._Settings.CommitNeeded = true;
            this._Settings.Commit();


            GUIGraphicsContext.OnVideoWindowChanged -= new VideoWindowChangedHandler(this.cbVideoWindowChanged);

            //Close the database
            Database.dbTable.Manager.Close();

            this.geoClockDestroyImage();

            this.imagesDestroy();

            base.DeInit();
        }

        public override int GetID
        {
            get { return (PLUGIN_ID); }
            set { }
        }

        protected override void OnPageLoad()
        {
            this.buttonsInit();

            //Init view mode
            this._FullScreenMediaViewMode = false;
            this.setViewMode(ViewMode.Condition);

            this.setImageViewMode(this._ImageViewMode);

            this.doLocationRefresh(false);
            tagsSetCalendar(this._Settings, this._WeatherLocation, DateTime.Today, this._Localisation, Database.dbHoliday.GetAll());

            this.moonImageRefresh();
            this.imagesRefresh();

            this._TimerRefreshLocation.Enabled = true;
        }

        protected override void OnClicked(int controlId, GUIControl control, MediaPortal.GUI.Library.Action.ActionType actionType)
        {
            if (control == this._GUIbuttonRefresh)
            {
                this.doWeatherRefresh(false);

                switch (this._ViewMode)
                {
                    case ViewMode.Condition:
                    case ViewMode.Image:
                        this.imagesRefresh();
                        break;

                    case ViewMode.GeoClock:
                        this.geoClockRefresh(this._WeatherLocation, false);
                        break;
                }
            }
            else if (control == this._GUIbuttonDisplay)
            {
                switch (this._ViewMode)
                {
                    case ViewMode.Condition:
                        this.setViewMode(ViewMode.Image);
                        break;

                    case ViewMode.Image:
                        this.setViewMode(ViewMode.GeoClock);
                        break;

                    case ViewMode.GeoClock:
                        if (this._Settings.GUICalendarEnable)
                            this.setViewMode(ViewMode.Calendar);
                        else
                            this.setViewMode(ViewMode.Condition);
                        break;

                    case ViewMode.Calendar:
                        this.setViewMode(ViewMode.Condition);
                        break;
                }
            }
            else if (control == this._GUIbuttonView)
            {
                switch (this._ViewMode)
                {
                    case ViewMode.Image:
                        switch (this._ImageViewMode)
                        {
                            case ImageViewModeEnum.Flat:
                                this.setImageViewMode(ImageViewModeEnum.Coverflow);
                                break;

                            case ImageViewModeEnum.Coverflow:
                                this.setImageViewMode(ImageViewModeEnum.Filmstrip);
                                break;

                            case ImageViewModeEnum.Filmstrip:
                                this.setImageViewMode(ImageViewModeEnum.Thumbnail);
                                break;

                            case ImageViewModeEnum.Thumbnail:
                                this.setImageViewMode(ImageViewModeEnum.Flat);
                                break;
                        }
                        break;

                }
            }
            else if (control == this._GUIfacadeList)
            {
                if (actionType == MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM)
                {
                    if (!this._FullScreenMediaViewMode)
                    {
                        this.onClick((GUI.GUIWeatherImage)this._GUIfacadeList.SelectedListItem);
                    }
                    else
                        this._FullScreenMediaViewMode = false;
                }
            }
            else if (control == this._GUIbuttonLocation)
                this.dialogShowLocationSelection();
        }

        public override bool OnMessage(GUIMessage message)
        {
            switch (message.Message)
            {
                case GUIMessage.MessageType.GUI_MSG_ITEM_FOCUS_CHANGED:
                    if (message.TargetWindowId == PLUGIN_ID)
                    {
                        if (message.SenderControlId == this._GUIfacadeList.GetID)
                        {
                            if (this._GUIfacadeList.SelectedListItem != null)
                                GUIPropertyManager.SetProperty(_TAG_IMAGE_SELECTED_LABEL, this._GUIfacadeList.SelectedListItem.Label);
                            else
                                GUIPropertyManager.SetProperty(_TAG_IMAGE_SELECTED_LABEL, string.Empty);
                        }
                    }
                    break;
                case GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT:
                    if (message.TargetWindowId == PLUGIN_ID)
                    {
                        if (this._GUIfacadeList != null)
                            this._GUIfacadeList.Clear();

                        this.geoClockDestroyImage();

                        this.imagesDestroy();

                        this._FullScreenMediaViewMode = false;
                        this._FullScreenMediaImage = null;

                        this._TimerRefreshLocation.Enabled = false;
                    }

                    break;
            }
            return base.OnMessage(message);
        }

        public override void OnAction(MediaPortal.GUI.Library.Action action)
        {
            switch ((action.wID))
            {
                case MediaPortal.GUI.Library.Action.ActionType.ACTION_PREVIOUS_MENU:
                    if (this._FullScreenMediaViewMode)
                    {
                        this._FullScreenMediaViewMode = false;
                        return;
                    }
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_LEFT:
                    if (this.setNextGuiImage(true))
                        return;
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_MOVE_RIGHT:
                    if (this.setNextGuiImage(false))
                        return;
                    break;

                case MediaPortal.GUI.Library.Action.ActionType.ACTION_SELECT_ITEM:
                    break;

                default:
                    if (this._FullScreenMediaViewMode)
                        return;
                    break;
            }

            base.OnAction(action);
        }

        protected override void OnShowContextMenu()
        {
            GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
            if (dlg == null)
                return;

            dlg.Reset();
            dlg.SetHeading("World Weather - " + Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.headerMenu));

            dlg.Add(Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.contextAddLocation));
            dlg.Add(Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.contextChangeCurrentConditionProvider));
                        

            //Show dialog
            dlg.DoModal(GUIWindowManager.ActiveWindow);

            if (dlg.SelectedId == 1)
            {
                Database.dbWeatherLoaction loc = this.dialogShowAddLocation();
                if (loc != null)
                {
                    loc.CommitNeeded = true;
                    loc.Commit();

                    this.setProfile(loc);

                    if (this._GUIbuttonLocation != null && !this._GUIbuttonLocation.IsEnabled)
                        this._GUIbuttonLocation.IsEnabled = true;
                }
            }
            else if (dlg.SelectedId == 2)
                this.dialogShowLocationSelection();
            
            base.OnShowContextMenu();
        }

        public override void Render(float timePassed)
        {
            if (this._FullScreenMediaViewMode)
            {
                GUI.GUIWeatherImage wi = this._FullScreenMediaImage;
                if (wi != null)
                {
                    Texture tx = wi.CurrentTexture;

                    Size size = wi.ImageSize;
                    if (tx != null && size != System.Drawing.Size.Empty && tx != null)
                    {
                        //Render the texture
                        RectangleF rect = wi.FullscreenRectangle;
                        Util.Picture.RenderImage(tx, rect.X, rect.Y, rect.Width, rect.Height, size.Width, size.Height, 0, 0, false);
                        return;
                    }
                }

                this._FullScreenMediaViewMode = false;
            }

            //Standart gui render
            base.Render(timePassed);
        }

        #endregion

        #region ISetupForm

        //Returns the name of the plugin which is shown in the plugin menu
        public string PluginName()
        {
            return PLUGIN_TITLE;
        }

        //Returns the description of the plugin is shown in the plugin menu
        public string Description()
        {
            return "World Weather Lite." + " (" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + ")";
        }

        //Returns the author of the plugin which is shown in the plugin menu
        public string Author()
        {
            return "Pbk";
        }

        //Show the setup dialog
        public void ShowPlugin()
        {
            ConfigurationForm cfg = new ConfigurationForm(Database.dbSettings.Instance);
            cfg.ShowDialog();
        }

        //Indicates whether plugin can be enabled/disabled
        public bool CanEnable()
        {
            return (true);
        }

        //Get Windows-ID
        public int GetWindowId()
        {
            //WindowID of windowplugin belonging to this setup
            //enter your own unique code
            return (PLUGIN_ID);
        }

        //Indicates if plugin is enabled by default;
        public bool DefaultEnabled()
        {
            return (true);
        }

        //Indicates if a plugin has it's own setup screen
        public bool HasSetup()
        {
            return (true);
        }

        public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
        {
            strButtonText = this.PluginName();
            strButtonImage = String.Empty;
            strButtonImageFocus = String.Empty;
            strPictureImage = String.Empty;
            return true;
        }


        #endregion

        #region IPluginReceiver
        public void Start()
        { }

        public void Stop()
        { }

        public bool WndProc(ref System.Windows.Forms.Message msg)
        {
            if (msg.Msg == WM_POWERBROADCAST)
            {
                switch (msg.WParam.ToInt32())
                {
                    case PBT_APMSTANDBY:
                        _Logger.Debug("[WndProc] Windows is going to standby");
                        this.onSuspend();
                        break;
                    case PBT_APMSUSPEND:
                        _Logger.Debug("[WndProc] Windows is suspending");
                        this.onSuspend();
                        break;
                    case PBT_APMQUERYSUSPEND:
                    case PBT_APMQUERYSTANDBY:
                        _Logger.Debug("[WndProc] Windows is going into powerstate (hibernation/standby)");
                        break;
                    case PBT_APMRESUMESUSPEND:
                    case PBT_APMRESUMEAUTOMATIC:
                        _Logger.Debug("[WndProc] Windows has resumed from hibernate mode");
                        this.onResume();
                        break;
                    case PBT_APMRESUMESTANDBY:
                        _Logger.Debug("[WndProc] Windows has resumed from standby mode");
                        this.onResume();
                        break;
                }
            }
            return false; // false = all other processes will handle the msg
        }
        #endregion

        #region Tags
        private void tagsInit()
        {
            GUIPropertyManager.SetProperty(_TAG_FEED_ENABLED, "false");

            GUIPropertyManager.SetProperty(_TAG_VIEW, "Condition");

            GUIPropertyManager.SetProperty(_TAG_VIEW_CONDITION, "Normal");

            GUIPropertyManager.SetProperty(_TAG_IMAGE_SELECTED_LABEL, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_LOCATION_TIME_ZONE_DESCRIPTION, string.Empty);



            #region Translations
            object[] translationTable = new object[][]
            {
                new object[]{_TAG_TRANSLATION_PROVIDER, (int)Language.TranslationEnum.translationProviderText },
                new object[]{_TAG_TRANSLATION_NOPROVIDER, (int)Language.TranslationEnum.translationNoProvider },
                new object[]{_TAG_TRANSLATION_REFRESH, (int)Language.TranslationEnum.buttonRefresh },
                new object[]{_TAG_TRANSLATION_FEED, (int)Language.TranslationEnum.translationFeed },
                new object[]{_TAG_TRANSLATION_CURRENT_CONDITION, (int)Language.TranslationEnum.translationCurrentCondition },
                new object[]{_TAG_TRANSLATION_FORECAST_CONDITION, (int)Language.TranslationEnum.translationForecastCondition },
                new object[]{_TAG_TRANSLATION_REFRESH_DATETIME, (int)Language.TranslationEnum.translationRefreshDateTime },
                new object[]{_TAG_TRANSLATION_GEOCLOCK, (int)Language.TranslationEnum.translationGeoClock },
                new object[]{_TAG_TRANSLATION_MOON_PHASE, (int)Language.TranslationEnum.translationMoonPhase },
                new object[]{_TAG_TRANSLATION_MOON_RISE_TIME, (int)Language.TranslationEnum.translationMoonrise },
                new object[]{_TAG_TRANSLATION_MOON_SET_TIME, (int)Language.TranslationEnum.translationMoonset },
                new object[]{_TAG_TRANSLATION_VISIBILITY, (int)Language.TranslationEnum.translationVisibility },
                new object[]{_TAG_TRANSLATION_PRECIPITATION, (int)Language.TranslationEnum.translationPrecipitation },
                new object[]{_TAG_TRANSLATION_HUMIDITY, (int)Language.TranslationEnum.translationHumidity },
                new object[]{_TAG_TRANSLATION_PRESSURE, (int)Language.TranslationEnum.translationPressure },
                new object[]{_TAG_TRANSLATION_SUNRISE, (int)Language.TranslationEnum.translationSunrise },
                new object[]{_TAG_TRANSLATION_SUNSET, (int)Language.TranslationEnum.translationSunset },
                new object[]{_TAG_TRANSLATION_WIND, (int)Language.TranslationEnum.translationWind },
                new object[]{_TAG_TRANSLATION_WIND_SPEED, (int)Language.TranslationEnum.translationWindSpeed },
                new object[]{_TAG_TRANSLATION_TEMPERATURE, (int)Language.TranslationEnum.translationTemperature },
                new object[]{_TAG_TRANSLATION_CONDITION, (int)Language.TranslationEnum.translationCondition },
                new object[]{_TAG_TRANSLATION_DEW_POINT, (int)Language.TranslationEnum.translationDewPoint },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_FEELS_LIKE, (int)Language.TranslationEnum.translationTemperatureFeelsLike },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_RECORD, (int)Language.TranslationEnum.translationTemperatureRecord },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_AVERAGE, (int)Language.TranslationEnum.translationTemperatureAverage },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_LOW, (int)Language.TranslationEnum.translationTemperatureLow },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_HIGH, (int)Language.TranslationEnum.translationTemperatureHigh },
                new object[]{_TAG_TRANSLATION_CLOUD_COVERAGE, (int)Language.TranslationEnum.translationCloudCoverage },
                new object[]{_TAG_TRANSLATION_FOG_COVERAGE, (int)Language.TranslationEnum.translationFogCoverage },
                new object[]{_TAG_TRANSLATION_BAROMETRIC_PRESSURE, (int)Language.TranslationEnum.translationBarometricPressure },
                new object[]{_TAG_TRANSLATION_UV_INDEX, (int)Language.TranslationEnum.translationUVIndex },
                new object[]{_TAG_TRANSLATION_HEAT_INDEX, (int)Language.TranslationEnum.translationHeatIndex },
                new object[]{_TAG_TRANSLATION_DAYLIGHT, (int)Language.TranslationEnum.translationDaylight },
                new object[]{_TAG_TRANSLATION_SUNSHINE_DURATION, (int)Language.TranslationEnum.translationSunshineDuration },
                new object[]{_TAG_TRANSLATION_HISTORY_YEAR_CONDITION, (int)Language.TranslationEnum.translationHistoryYearCondition },
                new object[]{_TAG_TRANSLATION_HISTORY_DAY_CONDITION, (int)Language.TranslationEnum.translationHistoryDayCondition },
                new object[]{_TAG_TRANSLATION_WORLD_IMAGE, (int)Language.TranslationEnum.translationWorldMedia },
                new object[]{_TAG_TRANSLATION_SATELLITE_IMAGE, (int)Language.TranslationEnum.translationSatelliteMedia },
                new object[]{_TAG_TRANSLATION_TEMPERATURE_IMAGE, (int)Language.TranslationEnum.translationTemperatureMedia },
                new object[]{_TAG_TRANSLATION_UV_INDEX_IMAGE, (int)Language.TranslationEnum.translationUVIndexMedia },
                new object[]{_TAG_TRANSLATION_UV_WIND_IMAGE, (int)Language.TranslationEnum.translationWindMedia },
                new object[]{_TAG_TRANSLATION_PRECIPITATION_IMAGE, (int)Language.TranslationEnum.translationPrecipitationMedia },
                new object[]{_TAG_TRANSLATION_HUMIDITY_IMAGE, (int)Language.TranslationEnum.translationHumidityMedia },
                new object[]{_TAG_TRANSLATION_SUNTIME_IMAGE, (int)Language.TranslationEnum.translationSuntimeMedia },
                new object[]{_TAG_TRANSLATION_POLLEN_COUNT_IMAGE, (int)Language.TranslationEnum.translationPollenCountMedia },
                new object[]{_TAG_TRANSLATION_WEBCAM_IMAGE, (int)Language.TranslationEnum.translationWebcamMedia },
                new object[]{_TAG_TRANSLATION_SELF_DEFINED_IMAGE, (int)Language.TranslationEnum.translationSelfDefinedMedia },
                new object[]{_TAG_TRANSLATION_MOON_CULMINATION, (int)Language.TranslationEnum.translationMoonCulmination },
                new object[]{_TAG_TRANSLATION_MOON_ALTITUDE, (int)Language.TranslationEnum.translationMoonAltitude },
                new object[]{_TAG_TRANSLATION_MOON_AZIMUTH, (int)Language.TranslationEnum.translationMoonAzimuth },
                new object[]{_TAG_TRANSLATION_MOON_DIAMETER, (int)Language.TranslationEnum.translationMoonDiameter },
                new object[]{_TAG_TRANSLATION_MOON_DISTANCE, (int)Language.TranslationEnum.translationMoonDistance },
                new object[]{_TAG_TRANSLATION_SUN_CULMINATION, (int)Language.TranslationEnum.translationSunCulmination },
                new object[]{_TAG_TRANSLATION_SUN_ALTITUDE, (int)Language.TranslationEnum.translationSunAltitude },
                new object[]{_TAG_TRANSLATION_SUN_AZIMUTH, (int)Language.TranslationEnum.translationSunAzimuth },
                new object[]{_TAG_TRANSLATION_SUN_DIAMETER, (int)Language.TranslationEnum.translationSunDiameter },
                new object[]{_TAG_TRANSLATION_SUN_DISTANCE, (int)Language.TranslationEnum.translationSunDistance },
                new object[]{_TAG_TRANSLATION_CIVIL_TWILIGHT_MORNING, (int)Language.TranslationEnum.translationCivilTwilightMorning },
                new object[]{_TAG_TRANSLATION_CIVIL_TWILIGHT_EVENING, (int)Language.TranslationEnum.translationCivilTwilightEvening },
                new object[]{_TAG_TRANSLATION_PICTURE_OF_DAY, (int)Language.TranslationEnum.translationPictureOfDay },
                new object[]{_TAG_TRANSLATION_CHART, (int)Language.TranslationEnum.translationChart },
                new object[]{_TAG_TRANSLATION_ZODIAC, (int)Language.TranslationEnum.translationZodiac },
                new object[]{_TAG_TRANSLATION_STARRY_SKY, (int)Language.TranslationEnum.translationStarrySky },
                new object[]{_TAG_TRANSLATION_HOLIDAY, (int)Language.TranslationEnum.translationHoliday },
                new object[]{_TAG_TRANSLATION_SEASON, (int)Language.TranslationEnum.translationSeason },
                new object[]{_TAG_TRANSLATION_NEW_YEAR, (int)Language.TranslationEnum.translationNewYear },
                new object[]{_TAG_TRANSLATION_EPIPHANY, (int)Language.TranslationEnum.translationEpiphany },
                new object[]{_TAG_TRANSLATION_HOLY_THURSDAY, (int)Language.TranslationEnum.translationHolyThursday },
                new object[]{_TAG_TRANSLATION_GOOD_FRIDAY, (int)Language.TranslationEnum.translationGoodFriday },
                new object[]{_TAG_TRANSLATION_EASTER_SUNDAY, (int)Language.TranslationEnum.translationEasterSunday },
                new object[]{_TAG_TRANSLATION_ASCENSION_DAY, (int)Language.TranslationEnum.translationAscensionDay },
                new object[]{_TAG_TRANSLATION_WHIT_SUNDAY, (int)Language.TranslationEnum.translationWhitSunday },
                new object[]{_TAG_TRANSLATION_CORPUS_CHRISTI, (int)Language.TranslationEnum.translationCorpusChristi },
                new object[]{_TAG_TRANSLATION_ASSUMPTION_DAY, (int)Language.TranslationEnum.translationAssumptionDay },
                new object[]{_TAG_TRANSLATION_REFORMATION_DAY, (int)Language.TranslationEnum.translationReformationDay },
                new object[]{_TAG_TRANSLATION_ALL_SAINTS_DAY, (int)Language.TranslationEnum.translationAllSaintsDay },
                new object[]{_TAG_TRANSLATION_CHRISTMAS_DAY, (int)Language.TranslationEnum.translationChristmasDay },
                new object[]{_TAG_TRANSLATION_SPRING_SEASON, (int)Language.TranslationEnum.translationSpringSeason },
                new object[]{_TAG_TRANSLATION_SUMMER_SEASON, (int)Language.TranslationEnum.translationSummerSeason },
                new object[]{_TAG_TRANSLATION_AUTUMN_SEASON, (int)Language.TranslationEnum.translationAutumnSeason },
                new object[]{_TAG_TRANSLATION_WINTER_SEASON, (int)Language.TranslationEnum.translationWinterSeason },
                new object[]{_TAG_TRANSLATION_DAY, (int)Language.TranslationEnum.translationDay },
                new object[]{_TAG_TRANSLATION_WEEK, (int)Language.TranslationEnum.translationWeek },
            };

            for (int i = 0; i < translationTable.GetLength(0); i++)
            {
                object[] item = (object[])translationTable[i];
                GUIPropertyManager.SetProperty((string)item[0], Language.Translation.GetLanguageString(this._Localisation, (int)item[1]));
            }
            #endregion

            //Clear weather tags
            tagsClearWeather();

            //Clear loacation tags
            tagsClearLocation();

            //Media tag initialization
            //0: World Image
            //1: Satellite Image
            //2: Temperature Image
            //3: UV Index Image
            //4: Wind Image
            //5: Precipitation Image
            //6: Humidity Image
            //7: Sun Times Image
            //8: 
            //9: Webcam Image
            //10:Self Defined Image
            for (int i = 0; i < this._WaeatherImages.Length; i++)
            {
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ImageWeather" + i, string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ImageWeather" + i + "Description",
                    this._WaeatherImages[i] != null ? this._WaeatherImages[i].WeatherImage.Description : string.Empty);
            }
        }

        private static void tagSetLocation(Database.dbWeatherLoaction loc, DateTime dt, Database.dbSettings set, Providers.IWeatherProvider prov)
        {
            tagSetLocation(loc, dt, set, prov, null, null, null);
        }
        private static void tagSetLocation(Database.dbWeatherLoaction loc, DateTime dt, Database.dbSettings set, Providers.IWeatherProvider prov,
            CoordinateSharp.Coordinate coord, CoordinateSharp.Celestial celSunRise, CoordinateSharp.Celestial celSunSet)
        {
            try
            {
                //Provider
                string strProvider = prov.Name;
                string strProviderImage;

                string strWeatherDir = Configuration.Config.GetSubFolder(Config.Dir.Skin, Configuration.Config.SkinName) + "\\Media\\WorldWeather\\Provider\\";
                if (!Directory.Exists(strWeatherDir))
                    strWeatherDir = Configuration.Config.GetSubFolder(Config.Dir.Weather, "Provider\\");

                strProviderImage = strWeatherDir + strProvider + ".png";

                GUIPropertyManager.SetProperty(_TAG_PROVIDER, strProvider);
                GUIPropertyManager.SetProperty(_TAG_PROVIDER_IMAGE, strProviderImage);

                GUIPropertyManager.SetProperty(_TAG_FORECAST_PROVIDER, strProvider);
                GUIPropertyManager.SetProperty(_TAG_FORECAST_PROVIDER_IMAGE, strProviderImage);

                DateTime dtLocal = dt.ToLocalTime();

                if (coord == null)
                    coord = getCoordinate(loc, dt, out celSunRise, out celSunSet);

                CoordinateSharp.Celestial celLocal = dt.ToLocalTime().Day == ((DateTime)celSunRise.SunRise).Day ? celSunRise : celSunSet;
                CoordinateSharp.Celestial celUtc = coord.CelestialInfo;

                //Location
                GUIPropertyManager.SetProperty(_TAG_LOCATION, loc.Name);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_COORDINATES, loc.Latitude.ToString() + ", " + loc.Longitude.ToString());
                GUIPropertyManager.SetProperty(_TAG_LOCATION_COUNTRY, loc.Country);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_DESCRIPTION, ">" + loc.Name + (!string.IsNullOrWhiteSpace(loc.Country) ? (" (" + loc.Country + ")") : null));
                GUIPropertyManager.SetProperty(_TAG_LOCATION_TIME_ZONE_DESCRIPTION, TimeZoneInfo.Local.DisplayName);

                GUIPropertyManager.SetProperty(_TAG_LOCATION_COORDINATES_DEGREE,
                    string.Format("{0}° {1}' {2}\" {3}, {4}° {5}' {6}\" {7}",
                    coord.Latitude.Degrees, coord.Latitude.Minutes, Math.Round(coord.Latitude.Seconds), loc.Latitude < 0 ? 'S' : 'N',
                    coord.Longitude.Degrees, coord.Longitude.Minutes, Math.Round(coord.Longitude.Seconds), loc.Latitude < 0 ? 'W' : 'E'
                    ));


                GUIPropertyManager.SetProperty(_TAG_LOCATION_CIVIL_TWILIGHT_MORNING_TIME,
                    celSunRise.AdditionalSolarTimes.CivilDawn != null ? ((DateTime)celSunRise.AdditionalSolarTimes.CivilDawn).ToLocalTime().ToShortTimeString() : string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_CIVIL_TWILIGHT_EVENING_TIME,
                    celSunSet.AdditionalSolarTimes.CivilDusk != null ? ((DateTime)celSunSet.AdditionalSolarTimes.CivilDusk).ToLocalTime().ToShortTimeString() : string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_NAUTICAL_TWILIGHT_MORNING_TIME,
                    celSunRise.AdditionalSolarTimes.NauticalDawn != null ? ((DateTime)celSunRise.AdditionalSolarTimes.NauticalDawn).ToLocalTime().ToShortTimeString() : string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_NAUTICAL_TWILIGHT_EVENING_TIME,
                    celSunSet.AdditionalSolarTimes.NauticalDusk != null ? ((DateTime)celSunSet.AdditionalSolarTimes.NauticalDusk).ToLocalTime().ToShortTimeString() : string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_ASTRONOMICAL_TWILIGHT_MORNING_TIME,
                    celSunRise.AdditionalSolarTimes.AstronomicalDawn != null ? ((DateTime)celSunRise.AdditionalSolarTimes.AstronomicalDawn).ToLocalTime().ToShortTimeString() : string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_ASTRONOMICAL_TWILIGHT_EVENING_TIME,
                    celSunSet.AdditionalSolarTimes.AstronomicalDusk != null ? ((DateTime)celSunSet.AdditionalSolarTimes.AstronomicalDusk).ToLocalTime().ToShortTimeString() : string.Empty);

                //Sun
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_RISE_TIME, ((DateTime)celSunRise.SunRise).ToLocalTime().ToShortTimeString());
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_SET_TIME, ((DateTime)celSunSet.SunSet).ToLocalTime().ToShortTimeString());
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_CULMINATION_TIME, ((DateTime)celLocal.SolarNoon).ToLocalTime().ToShortTimeString());

                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUNSHINE_DURATION, DateTime.MinValue.Add((DateTime)celSunSet.SunSet - (DateTime)celSunRise.SunRise).ToShortTimeString());


                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_AZIMUTH, celUtc.SunAzimuth.ToString("0") + "°");
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_DISTANCE, string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_DIAMETER, string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_ALTITUDE, celUtc.SunAltitude.ToString("0") + "°");

                //Moon
                DateTime? dtMoonRise = celLocal.MoonRise;
                DateTime? dtMoonSet = celLocal.MoonSet;
                if (dtMoonSet == null)
                {
                    //Following day
                    CoordinateSharp.Coordinate coordNext = new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dt.AddDays(1));
                    dtMoonSet = coordNext.CelestialInfo.MoonSet;
                }

                string strText;
                DateTime dtTemp;
                if (dtMoonRise != null)
                {
                    dtTemp = ((DateTime)dtMoonRise).ToLocalTime();
                    strText = dtTemp.ToShortTimeString() + (dtTemp.Date != dtLocal.Date ? (" (" + dtTemp.ToShortDateString() + ")") : null);
                }
                else
                    strText = string.Empty;
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_RISE_TIME, strText);


                if (dtMoonSet != null)
                {
                    dtTemp = ((DateTime)dtMoonSet).ToLocalTime();
                    if (dtTemp.Date < dtLocal.Date)
                    {
                        dtMoonSet = new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dtLocal.AddDays(1)).CelestialInfo.MoonSet;
                        if (dtMoonSet != null)
                            dtTemp = ((DateTime)dtMoonSet).ToLocalTime();
                        else
                        {
                            strText = string.Empty;
                            goto mset;
                        }
                    }

                    strText = dtTemp.ToShortTimeString() + (dtTemp.Date != dtLocal.Date ? (" (" + dtTemp.ToShortDateString() + ")") : null);
                }
                else
                    strText = string.Empty;
            mset:
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_SET_TIME, strText);


                //GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_CULMINATION_TIME, dtMoonRise != null && dtMoonSet != null ?
                //    (((DateTime)dtMoonRise).AddSeconds(((DateTime)dtMoonSet - (DateTime)dtMoonRise).TotalSeconds / 2)).ToShortTimeString() : string.Empty);

                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_AZIMUTH, celUtc.MoonAzimuth.ToString("0") + "°");
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_DISTANCE, Utils.UnitHelper.GetDistanceStringFromKiloMeters(set.GUIDistanceUnit, celUtc.MoonDistance.Kilometers));
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_DIAMETER, string.Empty);
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_ALTITUDE, celUtc.MoonAltitude.ToString("0") + "°");

                //DayLigh saving
                if (TimeZoneInfo.Local.SupportsDaylightSavingTime)
                {
                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_DESCRIPTION, TimeZoneInfo.Local.DaylightName);

                    System.Globalization.DaylightTime dlt = TimeZone.CurrentTimeZone.GetDaylightChanges(dt.ToLocalTime().Year);

                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_START, dlt.Start.ToString());
                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_END, dlt.End.ToString());
                }
                else
                {
                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_DESCRIPTION, string.Empty);
                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_START, string.Empty);
                    GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_END, string.Empty);
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[tagSetLocation] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }
        }

        private static void tagsClearWeather()
        {

            GUIPropertyManager.SetProperty(_TAG_REFRESH_DATE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_REFRESH_TIME, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_CONDITION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_NUMBER, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_IMAGE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_NUMBER2, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_IMAGE2, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_TEMPERATURE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_TEMPERATURE_FEELSLIKE, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_HUMIDITY, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_SPEED, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_DEGREE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_IMAGE, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_DEWPOINT, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_CLOUD_COVERAGE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_VISIBILITY, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_PRESSURE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_PRESSURE_BAROMETRIC, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_PRECIPITATION, string.Empty); //????

            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_0_TEXT, string.Empty); //????
            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_1_TEXT, string.Empty); //????
            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_2_TEXT, string.Empty); //????

            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_0_DESCRIPTION, string.Empty); //????
            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_1_DESCRIPTION, string.Empty); //????
            GUIPropertyManager.SetProperty(_TAG_LOCATION_FEED_2_DESCRIPTION, string.Empty); //????

            GUIPropertyManager.SetProperty(_TAG_TODAY_UV_INDEX, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_RECORD_MAX_TEMPERATURE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_RECORD_MIN_TEMPERATURE, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_RECORD_MAX_TEMPERATURE_YEAR, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_RECORD_MIN_TEMPERATURE_YEAR, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_AVERAGE_MAX_TEMPERATURE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_TODAY_HISTORY_DAY_AVERAGE_MIN_TEMPERATURE, string.Empty);


            for (int i = 0; i < 10; i++)
            {
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Low", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "High", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Condition", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconImage", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconNumber", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconImageV2", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconNumberV2", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Day", string.Empty);
                GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Date", string.Empty);
            }
        }

        private static void tagsClearLocation()
        {
            GUIPropertyManager.SetProperty(_TAG_LOCATION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_COUNTRY, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_DESCRIPTION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_CITY_CODE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_POSTAL_CODE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_REGION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_STATION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_UNIT_SYSTEM, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_DESCRIPTION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_START, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_DAYLIGHT_END, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_TIME_ZONE_DESCRIPTION, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_CIVIL_TWILIGHT_MORNING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_CIVIL_TWILIGHT_EVENING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_NAUTICAL_TWILIGHT_MORNING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_NAUTICAL_TWILIGHT_EVENING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_ASTRONOMICAL_TWILIGHT_MORNING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_ASTRONOMICAL_TWILIGHT_EVENING_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUNSHINE_DURATION, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_RISE_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_SET_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_AZIMUTH, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_DISTANCE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_DIAMETER, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_ALTITUDE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_SUN_CULMINATION_TIME, string.Empty);

            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE_IMAGE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_CULMINATION_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_RISE_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_SET_TIME, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_AZIMUTH, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_DISTANCE, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_DIAMETER, string.Empty);
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_ALTITUDE, string.Empty);
        }

        private static void tagsSetCalendar(Database.dbSettings set, Database.dbWeatherLoaction loc, DateTime dt, LocalisationProvider language, List<Database.dbHoliday> holidays)
        {
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_ENABLED, set.GUICalendarEnable ? "1" : string.Empty);

            DateTime dtEasterSunday = Utils.Calendar.GetEasterSundayDate(dt);
            DateTime dtTemp;

            int iYear = dt.ToLocalTime().Year;
            string str;
            Database.dbHoliday hol;
            for (int i = 0; i < holidays.Count; i++)
            {
                hol = holidays[i];
                str = string.Empty;

                #region Get date & descripton
                switch (hol.HolidayType)
                {
                    default:
                        dtTemp = DateTime.MinValue;
                        break;

                    case Utils.HolidayTypeEnum.NewYear:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationNewYear);
                        dtTemp = Utils.Calendar.GetNewYearDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.Epiphany:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationEpiphany);
                        dtTemp = Utils.Calendar.GetEpiphanyDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.HolyThurstday:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationHolyThursday);
                        dtTemp = Utils.Calendar.GetHolyThursdayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.GoodFriday:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationGoodFriday);
                        dtTemp = Utils.Calendar.GetGoodFridayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.EasterSunday:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationEasterSunday);
                        dtTemp = dtEasterSunday;
                        break;

                    case Utils.HolidayTypeEnum.AscensionDay:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationAscensionDay);
                        dtTemp = Utils.Calendar.GetAscensionDayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.WhitSunday:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationWhitSunday);
                        dtTemp = Utils.Calendar.GetWhitSundayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.CorpusChristi:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationCorpusChristi);
                        dtTemp = Utils.Calendar.GetCorpusChristiDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.AssumptionDay:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationAssumptionDay);
                        dtTemp = Utils.Calendar.GetAssumptionDayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.ReformationDay:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationReformationDay);
                        dtTemp = Utils.Calendar.GetReformationDayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.AllSaintsDay:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationAllSaintsDay);
                        dtTemp = Utils.Calendar.GetAllSaintsDayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.ChristmasDay:
                        str = Language.Translation.GetLanguageString(language, (int)Language.TranslationEnum.translationChristmasDay);
                        dtTemp = Utils.Calendar.GetChristmasDayDate(dt);
                        break;

                    case Utils.HolidayTypeEnum.Custom:
                        str = hol.Description;
                        dtTemp = new DateTime(iYear, hol.Month, hol.Day);
                        break;

                    case Utils.HolidayTypeEnum.EasterMonday:
                        str = hol.Description;
                        dtTemp = dtEasterSunday.AddDays(1);
                        break;
                }
                #endregion

                #region Assign date & description to the tags
                Utils.HolidayTypeEnum hType = (Utils.HolidayTypeEnum)(i + 1);

                switch (hType)
                {
                    case Utils.HolidayTypeEnum.NewYear:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_NEW_YEAR, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_NEW_YEAR, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_NEW_YEAR_WEEKDAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.Epiphany:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_EPIPHANY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_EPIPHANY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_EPIPHANY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.HolyThurstday:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_HOLY_THURSDAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_HOLY_THURSDAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_HOLY_THURSDAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.GoodFriday:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_GOOD_FRIDAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_GOOD_FRIDAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_GOOD_FRIDAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.EasterSunday:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_EASTER_SUNDAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_EASTER, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_EASTER_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.AscensionDay:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_ASCENSION_DAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ASCENSION_DAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ASCENSION_DAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.WhitSunday:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_WHIT_SUNDAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_WHIT_SUNDAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_WHIT_SUNDAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.CorpusChristi:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_CORPUS_CHRISTI, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_CORPUS_CHRISTI, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_CORPUS_CHRISTI_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.AssumptionDay:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_ASSUMPTION_DAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ASSUMPTION_DAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ASSUMPTION_DAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.ReformationDay:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_REFORMATION_DAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_REFORMATION_DAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_REFORMATION_DAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.AllSaintsDay:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_ALL_SAINTS_DAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ALL_SAINTS_DAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_ALL_SAINTS_DAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    case Utils.HolidayTypeEnum.ChristmasDay:
                        GUIPropertyManager.SetProperty(_TAG_TRANSLATION_CHRISTMAS_DAY, str);
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_CHRISTMAS_DAY, dtTemp.ToShortDateString());
                        GUIPropertyManager.SetProperty(_TAG_CALENDAR_CHRISTMAS_DAY_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));
                        break;

                    default:
                        //Custom specific
                        if (dtTemp > DateTime.MinValue)
                        {
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_DAY_DESCRIPTION, i - 12), str);
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_DAY, i - 12), dtTemp.ToShortDateString());
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_WEEK_DAY, i - 12), getTranslatedWeekDay(language, dtTemp));
                        }
                        else
                        {
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_DAY_DESCRIPTION, i - 12), string.Empty);
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_DAY, i - 12), string.Empty);
                            GUIPropertyManager.SetProperty(string.Format(_TAG_CALENDAR_SELF_DEFINED_WEEK_DAY, i - 12), string.Empty);
                        }
                        break;
                }
                #endregion
            }

            Utils.HemisphereTypeEnum type = loc.Latitude >= 0.0 ? Utils.HemisphereTypeEnum.NorthernHemisphere : Utils.HemisphereTypeEnum.SouthernHemisphere;
            dtTemp = Utils.Calendar.GetSpringStartDate(dt, type);
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_SPRING_SEASON, dtTemp.ToShortDateString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_SPRING_SEASON_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));

            dtTemp = Utils.Calendar.GetSummerStartDate(dt, type);
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_SUMMER_SEASON, dtTemp.ToShortDateString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_SUMMER_SEASON_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));

            dtTemp = Utils.Calendar.GeAutumnStartDate(dt, type);
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_AUTUMN_SEASON, dtTemp.ToShortDateString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_AUTUMN_SEASON_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));

            dtTemp = Utils.Calendar.GeWinterStartDate(dt, type);
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_WINTER_SEASON, dtTemp.ToShortDateString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_WINTER_SEASON_WEEK_DAY, getTranslatedWeekDay(language, dtTemp));

            GUIPropertyManager.SetProperty(_TAG_CALENDAR_JULIAN_DATE, "");
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_CALENDAR_DAY, Utils.Calendar.GetDayNumber(dt).ToString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_DAY_COUNT, Utils.Calendar.GetDayCount(dt).ToString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_WEEK, Utils.Calendar.GetWeekNumber(dt).ToString());
            GUIPropertyManager.SetProperty(_TAG_CALENDAR_WEEK_COUNT, Utils.Calendar.GetWeekCount(dt).ToString());
        }

        private static string getTranslatedWeekDay(LocalisationProvider language, DateTime dt)
        {
            Language.TranslationEnum id;

            switch (dt.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    id = Language.TranslationEnum.labelWeekdayMonday;
                    break;

                case DayOfWeek.Tuesday:
                    id = Language.TranslationEnum.labelWeekdayTuesday;
                    break;

                case DayOfWeek.Wednesday:
                    id = Language.TranslationEnum.labelWeekdayWednesday;
                    break;

                case DayOfWeek.Thursday:
                    id = Language.TranslationEnum.labelWeekdayThursday;
                    break;

                case DayOfWeek.Friday:
                    id = Language.TranslationEnum.labelWeekdayFriday;
                    break;

                case DayOfWeek.Saturday:
                    id = Language.TranslationEnum.labelWeekdaySaturday;
                    break;

                case DayOfWeek.Sunday:
                    id = Language.TranslationEnum.labelWeekdaySunday;
                    break;

                default:
                    return string.Empty; //" "
            }

            return Language.Translation.GetLanguageString(language, (int)id);
        }
        #endregion

        #region FullScreenMode methods


        private bool setNextGuiImage(bool bBackward)
        {
            if (this._FullScreenMediaViewMode)
            {
                if (this._GUIfacadeList.Count > 1)
                {
                    int iIdxLast = this._GUIfacadeList.SelectedListItemIndex;
                    int iIdxCurrent = iIdxLast;
                    while (true)
                    {
                        if (bBackward)
                        {
                            iIdxCurrent--;
                            if (iIdxCurrent < 0)
                                iIdxCurrent = this._GUIfacadeList.Count - 1;
                        }
                        else
                        {
                            iIdxCurrent++;
                            if (iIdxCurrent >= this._GUIfacadeList.Count)
                                iIdxCurrent = 0;
                        }

                        if (iIdxCurrent == iIdxLast)
                            return false;

                        GUI.GUIWeatherImage wi = (GUI.GUIWeatherImage)this._GUIfacadeList[iIdxCurrent];
                        if (wi.FramesAvailable)
                        {
                            this._GUIfacadeList.SelectedListItemIndex = iIdxCurrent;
                            this._FullScreenMediaImage = wi;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void onClick(GUI.GUIWeatherImage image)
        {
            if (image != null && image.CurrentTexture != null)
            {
                this._FullScreenMediaImage = image;
                this._FullScreenMediaViewMode = true;
            }
        }
        #endregion

        private void setProfile(Database.dbWeatherLoaction loc)
        {
            lock (this._TimerRefreshWeather)
            {
                //location change
                this._WeatherLocation = loc;

                _Logger.Debug("[setProfile] Location changed to: {0}", this._WeatherLocation.Name);

                //Wetaher refresh
                this.doWeatherRefresh(true);
            }

            //Refresh location tags
            this.doLocationRefresh(true);

            //Update profile ID
            this._Settings.ProfileID = (int)this._WeatherLocation.ID;
            this._Settings.CommitNeeded = true;
            this._Settings.Commit();

            //Update geoclock if needed
            if (this._ViewMode == ViewMode.GeoClock)
                this.geoClockRefresh(this._WeatherLocation, true);
        }

        private void setViewMode(ViewMode mode)
        {
            this._ViewMode = mode;

            switch (mode)
            {
                case ViewMode.Condition:
                    this._GUIbuttonDisplay.Label =
                        Language.Translation.GetLanguageString(
                            this._Localisation,
                            (int)Language.TranslationEnum.buttonAction,
                            "Show Media",
                            Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelMedia)
                            );

                    this._GUIbuttonView.IsEnabled = false;
                    this._GUIbuttonRefresh.IsEnabled = true;
                    break;

                case ViewMode.Image:
                    if (this._GUIfacadeList != null && this._GUIfacadeList.Count == 0)
                    {
                        for (int i = 0; i < this._WaeatherImages.Length; i++)
                        {
                            GUI.GUIWeatherImage wi = this._WaeatherImages[i];
                            if (wi != null)
                                this._GUIfacadeList.Add(wi);
                        }
                    }

                    //this.imagesRefresh();
                    this._GUIbuttonDisplay.Label =
                        Language.Translation.GetLanguageString(
                            this._Localisation,
                            (int)Language.TranslationEnum.buttonAction,
                            "Show GeoClock",
                            Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelGeoClock)
                            );

                    this._GUIbuttonView.IsEnabled = true;
                    this._GUIbuttonRefresh.IsEnabled = true;
                    break;

                case ViewMode.GeoClock:
                    this.geoClockRefresh(this._WeatherLocation, false);

                    if (this._Settings.GUICalendarEnable)
                    {
                        this._GUIbuttonDisplay.Label = Language.Translation.GetLanguageString(
                                this._Localisation,
                                (int)Language.TranslationEnum.buttonAction,
                                "Show Calendar",
                                Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelCalendar)
                                );
                    }
                    else
                    {
                        this._GUIbuttonDisplay.Label = Language.Translation.GetLanguageString(
                           this._Localisation,
                           (int)Language.TranslationEnum.buttonAction,
                           "Show Condition",
                           Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelCondition)
                           );
                    }

                    this._GUIbuttonView.IsEnabled = false;
                    this._GUIbuttonRefresh.IsEnabled = true;
                    break;

                case ViewMode.Calendar:
                    this._GUIbuttonDisplay.Label =
                        Language.Translation.GetLanguageString(
                            this._Localisation,
                            (int)Language.TranslationEnum.buttonAction,
                            "Show Condition",
                            Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelCondition)
                            );

                    this._GUIbuttonView.IsEnabled = false;
                    this._GUIbuttonRefresh.IsEnabled = false;
                    break;
            }

            GUIPropertyManager.SetProperty(_TAG_VIEW, mode.ToString());
        }

        private void setImageViewMode(ImageViewModeEnum mode)
        {
            this._ImageViewMode = mode;
            GUIPropertyManager.SetProperty(_TAG_VIEW_IMAGE, mode.ToString());
            if (this._GUIfacadeList != null)
            {
                switch (mode)
                {
                    default:
                        this._GUIfacadeList.CurrentLayout = GUIFacadeControl.Layout.List;
                        break;

                    case ImageViewModeEnum.Coverflow:
                        this._GUIfacadeList.CurrentLayout = GUIFacadeControl.Layout.CoverFlow;
                        break;

                    case ImageViewModeEnum.Filmstrip:
                        this._GUIfacadeList.CurrentLayout = GUIFacadeControl.Layout.Filmstrip;
                        break;

                    case ImageViewModeEnum.Thumbnail:
                        this._GUIfacadeList.CurrentLayout = GUIFacadeControl.Layout.LargeIcons;
                        break;
                }
            }
        }

        private void buttonsInit()
        {
            if (this._GUIbuttonDisplay != null)
                this._GUIbuttonDisplay.Label =
                    Language.Translation.GetLanguageString(
                            this._Localisation,
                            (int)Language.TranslationEnum.buttonAction,
                            "Show GeoClock",
                            Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelGeoClock)
                            );

            if (this._GUIbuttonLocation != null)
            {
                this._GUIbuttonLocation.Label = Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.buttonLocation);
                this._GUIbuttonLocation.IsEnabled = Database.dbWeatherLoaction.GetAll().Count > 1;
            }

            if (this._GUIbuttonBrowserMap != null)
            {
                this._GUIbuttonBrowserMap.Label = Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.buttonBrowserMap);
                this._GUIbuttonBrowserMap.IsEnabled = false;
            }

            if (this._GUIbuttonView != null)
            {
                this._GUIbuttonView.Label = Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.buttonView);
                this._GUIbuttonView.IsEnabled = false;
            }

            if (this._GUIbuttonRefresh != null)
                this._GUIbuttonRefresh.Label = Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.buttonRefresh);
        }

        private bool doWeatherRefresh(bool bForce)
        {
            Database.dbWeatherLoaction loc;
            Providers.IWeatherProvider provider;

            lock (this._TimerRefreshWeather)
            {
                while (true)
                {
                    loc = this._WeatherLocation;
                    provider = this._Providers.Find(p => p.Type == loc.Provider);

                    if (provider == null)
                        return false;

                    if (this._WeatherIsRefreshing != 0)
                    {
                        if (!bForce)
                            return false;
                        else
                            Monitor.Wait(this._TimerRefreshWeather); //ongoing refresh; wait for finish
                    }
                    else
                        break;
                }

                this._WeatherIsRefreshing = 1;
            }

            bool bResult = false;
            try
            {
                _Logger.Debug("[doWeatherRefresh][{0}] Run ...", loc.Name);

                DateTime dtUtcNow = DateTime.UtcNow;
                CoordinateSharp.Celestial celSunRise, celSunSet;
                CoordinateSharp.Coordinate coord = getCoordinate(loc, dtUtcNow, out celSunRise, out celSunSet);

                bool bIsDay = dtUtcNow >= celSunRise.AdditionalSolarTimes.CivilDawn && dtUtcNow <= celSunSet.AdditionalSolarTimes.CivilDusk;

                try
                {
                    //Get current condition from selected provider
                    Providers.WeatherData data = provider.GetCurrentWeatherData(loc, _REFRESH_INTERVAL);

                    if (data != null)
                    {
                        #region Apply data to tags

                        string strText, strCode;

                        string strWeatherIconDir = Configuration.Config.GetSubFolder(Config.Dir.Skin, Configuration.Config.SkinName) + "\\Media\\WorldWeather\\Condition\\Default\\high\\";
                        if (!Directory.Exists(strWeatherIconDir))
                            strWeatherIconDir = Configuration.Config.GetSubFolder(Config.Dir.Weather, "128x128\\");

                        //Time
                        if (data.RefreshedAt.Kind == DateTimeKind.Utc)
                            GUIPropertyManager.SetProperty(_TAG_REFRESH_TIME, TimeZone.CurrentTimeZone.ToLocalTime(data.RefreshedAt).ToString());
                        else
                            GUIPropertyManager.SetProperty(_TAG_REFRESH_TIME, data.RefreshedAt.ToString());

                        //Condition
                        strCode = getIconCodeString(data.ConditionIconCode, bIsDay);
                        GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_NUMBER, strCode);
                        GUIPropertyManager.SetProperty(_TAG_TODAY_ICON_IMAGE, strWeatherIconDir + strCode + ".png");
                        GUIPropertyManager.SetProperty(_TAG_TODAY_CONDITION, data.ConditionText != null ? data.ConditionText : Language.Translation.GetLanguageString(this._Localisation, (int)data.ConditionTranslationCode));

                        //Temperature
                        GUIPropertyManager.SetProperty(_TAG_TODAY_TEMPERATURE,
                            Utils.UnitHelper.GetTemperatureStringFromCelsius(this._Settings.GUITemperatureUnit, data.Temperature));
                        GUIPropertyManager.SetProperty(_TAG_TODAY_TEMPERATURE_FEELSLIKE,
                            data.TemperatureFeelsLike > int.MinValue ? Utils.UnitHelper.GetTemperatureStringFromCelsius(this._Settings.GUITemperatureUnit, data.TemperatureFeelsLike) : "n/a");


                        //Humidity
                        GUIPropertyManager.SetProperty(_TAG_TODAY_HUMIDITY, data.Humidity >= 0 ? data.Humidity.ToString() + '%' : "n/a");

                        //Wind
                        GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_SPEED, data.Wind >= 0 ? Utils.UnitHelper.GetWindStringFromMeterPerSecond(this._Settings.GUIWindUnit, data.Wind) : "n/a");

                        //Wind direction
                        float fWindDir = data.WindDirection;
                        if (fWindDir < 0 || fWindDir > 360)
                        {
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION, string.Empty);
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_DEGREE, string.Empty);
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_IMAGE, string.Empty);
                        }
                        else
                        {
                            int iIdx = (int)Math.Round(fWindDir / 22.5);
                            if (iIdx == 16)
                                iIdx = 0;

                            strText = _WindDirections[iIdx];
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_IMAGE, "WorldWeather\\Condition\\" + strText + ".png");
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION, Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.labelWindDirectionNorth + iIdx, strText));
                            GUIPropertyManager.SetProperty(_TAG_TODAY_WIND_DIRECTION_DEGREE, fWindDir.ToString("0") + "°");
                        }

                        //Pressure
                        GUIPropertyManager.SetProperty(_TAG_TODAY_PRESSURE, data.Pressure >= 0 ? Utils.UnitHelper.GetPressureStringFromHPa(this._Settings.GUIPressureUnit, data.Pressure) : string.Empty);

                        //Visibility
                        GUIPropertyManager.SetProperty(_TAG_TODAY_VISIBILITY, data.Visibility >= 0 ? (data.Visibility / 1000).ToString() + " km" : string.Empty);

                        //Cloud coverage
                        GUIPropertyManager.SetProperty(_TAG_TODAY_CLOUD_COVERAGE, data.CloudCoverage >= 0 ? data.CloudCoverage.ToString() + '%' : string.Empty);

                        //Dew point
                        double dDP = double.NaN;
                        if (data.DewPoint == int.MinValue && data.Humidity >= 0)
                        {
                            //This equation is accurate for humidity values above 50 percent.
                            //Deqpoint:  Td = T - ((100 - RH)/5)

                            //August-Roche-Magnus Estimation
                            //TD(Dew Point) = 243.04*(ln(RH/100)+((17.625*T)/(243.04+T)))/(17.625-ln(RH/100)-((17.625*T)/(243.04+T)))
                            double dLog = Math.Log((double)data.Humidity / 100);
                            double dTmp = (double)data.Temperature;
                            double d = (17.625D * dTmp) / (243.04D + dTmp);
                            dDP = 243.04D * (dLog + d) / (17.625D - dLog - d);
                        }
                        else
                            dDP = data.DewPoint;
                        GUIPropertyManager.SetProperty(_TAG_TODAY_DEWPOINT,
                            !dDP.Equals(double.NaN) ? Utils.UnitHelper.GetTemperatureStringFromCelsius(this._Settings.GUITemperatureUnit, (int)Math.Round(dDP)) : string.Empty);

                        //UV Index
                        GUIPropertyManager.SetProperty(_TAG_TODAY_UV_INDEX, data.UVIndex > int.MinValue ? data.UVIndex.ToString() : string.Empty);

                        //Rain precipitation
                        GUIPropertyManager.SetProperty(_TAG_TODAY_PRECIPITATION,
                            !data.RainPrecipitation.Equals(float.NaN) ? Utils.UnitHelper.GetPrecipitationStringFromMillimeter(this._Settings.GUIPrecipitationUnit, data.RainPrecipitation) : string.Empty);

                        //Forecast
                        for (int i = 0; i < 10; i++)
                        {
                            Providers.ForecastDay day = data.ForecastDays != null && i < data.ForecastDays.Count ? data.ForecastDays[i] : null;

                            //Temperature MIN
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Low",
                                day != null ? Utils.UnitHelper.GetTemperatureStringFromCelsius(this._Settings.GUITemperatureUnit, day.TemperatureMin) : string.Empty);

                            //Temperature MAX
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "High",
                                day != null ? Utils.UnitHelper.GetTemperatureStringFromCelsius(this._Settings.GUITemperatureUnit, day.TemperatureMax) : string.Empty);

                            //Condition
                            if (day != null)
                            {
                                strCode = getIconCodeString(day.ConditionIconCode, true);
                                strText = day.ConditionText != null ? day.ConditionText : Language.Translation.GetLanguageString(this._Localisation, (int)day.ConditionTranslationCode);
                            }
                            else
                            {
                                strText = string.Empty;
                                strCode = string.Empty;
                            }

                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Condition", strText);

                            //Image
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconImage", strWeatherIconDir + strCode + ".png");

                            //Image code
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "IconNumber", strCode);

                            //Day
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Day", day != null ? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day.Date.DayOfWeek) : string.Empty);

                            //Date
                            GUIPropertyManager.SetProperty(_TAG_PREFFIX + ".ForecastDay" + i + "Date", day != null ? day.Date.ToString("dd.MM.") : string.Empty);
                        }
                        #endregion

                        bResult = true;
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Error("[doWeatherRefresh] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    tagsClearWeather();
                }

                //Next refresh time
                lock (this._TimerRefreshWeather)
                {
                    if (!bResult)
                    {
                        double dTime = Math.Min(_REFRESH_INTERVAL, Math.Pow(2, loc.WeatherRefreshAttempts) * 60);
                        this._TimerRefreshWeather.Interval = dTime * 1000;
                        loc.WeatherRefreshAttempts++;
                        _Logger.Error("[doWeatherRefresh][{0}] Failed. Next refresh attempt in {1:0} seconds.", loc.Name, dTime);
                    }
                    else
                    {
                        loc.RefreshLast = DateTime.Now;
                        this._TimerRefreshWeather.Interval = _REFRESH_INTERVAL * 1000;
                        loc.WeatherRefreshAttempts = 0;
                        _Logger.Debug("[doWeatherRefresh][{0}] Complete. Next refresh in {1} seconds.", loc.Name, _REFRESH_INTERVAL);
                    }
                }

                //Moon phase
                this.moonImageRefresh();

                //Location
                //tagSetLocation(this._WeatherLocation, dtUtcNow, this._Settings, this._Provider, coord, celSunRise, celSunSet);

                //Set timer
                this._TimerRefreshWeather.Enabled = true;
            }
            finally
            {
                lock (this._TimerRefreshWeather)
                {
                    this._WeatherIsRefreshing = 0;
                    Monitor.PulseAll(this._TimerRefreshWeather);
                }
            }

            return bResult;
        }

        private void doLocationRefresh(bool bForce)
        {
            lock (this._TimerRefreshLocation)
            {
                while (true)
                {
                    if (this._LocationIsRefreshing != 0)
                    {
                        if (!bForce)
                            return;
                        else
                            Monitor.Wait(this._TimerRefreshLocation); //ongoing refresh; wait for finish
                    }
                    else
                        break;
                }

                this._LocationIsRefreshing = 1;
            }

            Database.dbWeatherLoaction loc = this._WeatherLocation;
            tagSetLocation(loc, DateTime.UtcNow, this._Settings, this._Providers.Find(p => p.Type == loc.Provider));

            lock (this._TimerRefreshLocation)
            {
                this._LocationIsRefreshing = 0;
                Monitor.PulseAll(this._TimerRefreshLocation);
            }

        }

        private void setWeatherRefresh(bool bEnable)
        {
            lock (this._TimerRefreshWeather)
            {
                if (bEnable || this._TimerRefreshWeather.Enabled)
                {
                    //Check next weather refresh time
                    int iTime = this._WeatherLocation.RefreshLast > DateTime.MinValue ? (int)(DateTime.Now - this._WeatherLocation.RefreshLast).TotalSeconds : _REFRESH_INTERVAL;

                    if (iTime >= _REFRESH_INTERVAL - 5)
                    {
                        _Logger.Debug("[setWeatherRefresh] Weather refresh is in due.");
                        iTime = 2; //refresh is in due now
                    }
                    else
                    {
                        iTime = _REFRESH_INTERVAL - iTime;
                        _Logger.Debug("[setWeatherRefresh] Next refresh in {0} seconds.", iTime);
                    }

                    this._TimerRefreshWeather.Interval = iTime * 1000;

                    this._TimerRefreshWeather.Enabled = true;
                }
                else
                    _Logger.Debug("[setWeatherRefresh] Weather refresh is currently suspended.");
            }
        }

        private static string getIconCodeString(int iCode, bool bIsDay)
        {
            if (iCode < 0 || iCode > 47)
                return "na";
            else if (!bIsDay)
            {
                //Night icon fix
                switch (iCode)
                {
                    case 34:
                        return "33"; //"day_fair", 34

                    case 32:
                        return "31"; //"clear_sunny", 32

                    case 30:
                        return "29"; //"partly_cloudy", 30

                    case 28:
                        return "27"; //"mostly_cloudy", 28

                }
            }

            return iCode.ToString();
        }

        private static CoordinateSharp.Coordinate getCoordinate(Database.dbWeatherLoaction loc, DateTime dtUtc, out CoordinateSharp.Celestial celSunRise, out CoordinateSharp.Celestial celSunSet)
        {
            CoordinateSharp.Coordinate coord = new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dtUtc);

            DateTime dtLocalDate = dtUtc.ToLocalTime().Date;

            CoordinateSharp.Coordinate coordLocal = dtUtc.Date == dtLocalDate ? coord : new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dtLocalDate);

            if (coordLocal.CelestialInfo.SunRise > coordLocal.CelestialInfo.SunSet)
            {
                if (loc.Longitude >= 0)
                {
                    //East
                    CoordinateSharp.Coordinate c = new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dtLocalDate.AddDays(-1));

                    celSunRise = c.CelestialInfo;
                    celSunSet = coordLocal.CelestialInfo;
                }
                else
                {
                    //West

                    CoordinateSharp.Coordinate c = new CoordinateSharp.Coordinate(loc.Latitude, loc.Longitude, dtLocalDate.AddDays(1));

                    celSunRise = coordLocal.CelestialInfo;
                    celSunSet = c.CelestialInfo;
                }
            }
            else
            {
                celSunRise = coordLocal.CelestialInfo;
                celSunSet = coordLocal.CelestialInfo;
            }


            return coord;
        }

        #region Dialogs
        private Database.dbWeatherLoaction dialogShowAddLocation()
        {
            VirtualKeyboard keyBoard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_VIRTUAL_KEYBOARD);

            if (keyBoard == null)
                return null;

            keyBoard.Reset();
            
            //Enter location to search
            keyBoard.DoModal(GUIWindowManager.ActiveWindow);

            if (keyBoard.IsConfirmed && !string.IsNullOrWhiteSpace(keyBoard.Text))
            {
                string strQuery = keyBoard.Text.Trim();
                _Logger.Debug("[dialogShowAddLocation] Searching for:  '{0}'", strQuery);
                List<Database.dbWeatherLoaction> result = new List<Database.dbWeatherLoaction>();

                //Search the location by each provider
                this._Providers.ForEach(prov =>
                {
                    try
                    {
                        //Search
                        result.AddRange(prov.Search(strQuery));
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[dialogShowAddLocation][{3}][search] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace, prov.Name);
                    }
                });

                //Show the result
                if (result.Count > 0)
                    return this.dialogShowLocationSelection(result, null, Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.notificationDetectedLocation), true);
                else
                    this.dialogShowNotify(Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.errorFailedLocationDetection));
            }

            return null;
        }

        private void dialogShowLocationSelection()
        {
            List<Database.dbWeatherLoaction> list = Database.dbWeatherLoaction.GetAll();
            list.Sort((p1, p2)=> ((int)p1.ID).CompareTo((int)p2.ID));

            Database.dbWeatherLoaction loc = this.dialogShowLocationSelection(list, this._WeatherLocation, null, false);

            if (loc != null && this._WeatherLocation != loc)
                this.setProfile(loc);

        }
        private Database.dbWeatherLoaction dialogShowLocationSelection(List<Database.dbWeatherLoaction> list, Database.dbWeatherLoaction preselectedItem, string strLabel, bool bPrintDetail)
        {
            GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)Window.WINDOW_DIALOG_MENU);
            if (dlg == null)
                return null;

            if (strLabel == null)
                strLabel = "World Weather - " + Language.Translation.GetLanguageString(this._Localisation, (int)Language.TranslationEnum.headerLocation);

            dlg.Reset();
            dlg.SetHeading(strLabel);

            int iSel = -1;
            for (int i = 0; i < list.Count; i++)
            {
                Database.dbWeatherLoaction loc = list[i];
                dlg.Add(bPrintDetail ? string.Format("{1} [{2}][{0}]", loc.Provider, loc.Name, loc.ObservationLocation) : loc.Name);

                if (loc == preselectedItem)
                    iSel = i;
            }

            //Preselection
            if (iSel >= 0)
                dlg.selectOption((iSel + 1).ToString());

            //Show dialog
            dlg.DoModal(GUIWindowManager.ActiveWindow);

            if (dlg.SelectedId > 0)
                return list[dlg.SelectedId - 1];
            else
                return null;
        }

        private void dialogShowNotify(string strMessage)
        {
            GUIDialogNotify dlg = (GUIDialogNotify)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_NOTIFY);
            if (dlg != null)
            {
                dlg.Reset();
                dlg.SetHeading("World Weather");
                dlg.SetText(strMessage);
                dlg.DoModal(GUIWindowManager.ActiveWindow);
            }
        }
        #endregion

        #region Images methods

        private static string memoryImageBuild(Image image, string strId, Size size)
        {
            try
            {
                // we don't have to try first, if name already exists mp will not do anything with the image
                //resize
                if ((size.Height > 0 && (size.Height != image.Size.Height || size.Width != image.Size.Width)))
                    image = new Bitmap(image, size);

                GUITextureManager.LoadFromMemory(image, strId, 0, size.Width, size.Height);
            }
            catch { return string.Empty; }
            return strId;
        }

        private static void memoryImageDestroy(string strId)
        {
            GUITextureManager.ReleaseTexture(strId);
        }

        private void moonImageRefresh()
        {
            DateTime dtNow = DateTime.UtcNow;

            if (this._MoonImagePath != null)
            {
                if ((dtNow - this._MoonImageLastRefresh).TotalHours < 3)
                    return;

                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE_IMAGE, string.Empty);
                GUITextureManager.ReleaseTexture(this._MoonImagePath);
            }

            //Moon phase
            double dPeriod;
            Utils.MoonPhaseEnum mp = Utils.Moon.GetMoonPhase(dtNow, out dPeriod);

            //Moon phase translation
            GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE, Language.Translation.GetLanguageString(this._Localisation,
                (int)mp + (int)Language.TranslationEnum.labelMoonPhaseNewMoon, mp.ToString()));

            //Moon phase current image
            Image img = Utils.Moon.GetMoonImage(dPeriod, Config.GetFolder(Config.Dir.Skin) + '\\' + Config.SkinName + @"\Media\WorldWeather\Moon\high\4.png", 0.925D);
            if (img != null)
            {
                this._MoonImagePath = memoryImageBuild(img, _MOON_IMAGE_ID, img.Size);
                img.Dispose();

                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE_IMAGE, this._MoonImagePath);

                this._MoonImageLastRefresh = dtNow;
            }
            else
                GUIPropertyManager.SetProperty(_TAG_LOCATION_MOON_PHASE_IMAGE, Configuration.Config.GetSubFolder(Config.Dir.Weather, "Moon\\" + ((int)mp).ToString() + ".png"));
        }


        private bool geoClockRefresh(Database.dbWeatherLoaction loc, bool bForce)
        {
            DateTime dtNow = DateTime.Now;
            DateTime dt = new DateTime(dtNow.Year, dtNow.Month, dtNow.Day, dtNow.Hour, dtNow.Minute, 0);

            if (this._GeoClockImagePath != null && (!bForce && this._GeoClockLastRefresh == dt))
                return true;
            else
                this.geoClockDestroyImage();

            bool bResult = false;

            //System.Diagnostics.Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();

            Image img = this._GeoClock.GetGeoClockImage(DateTime.UtcNow);

            //stopWatch.Stop();

            //_Logger.Debug("[geoClockRefresh] Time: " + stopWatch.Elapsed.ToString());

            if (img != null)
            {
                if (!string.IsNullOrWhiteSpace(loc.Name))
                {
                    this._GeoClock.ClearGeoClockLocation();
                    this._GeoClock.AddGeoClockLocation(loc.Name, loc.Longitude, loc.Latitude);
                    this._GeoClock.SetGeoClockLocation(new System.Drawing.Font("Arial", 24f, FontStyle.Bold, GraphicsUnit.Pixel), Color.White, img, true, true, false);
                }

                //bResult = this._GeoClock.SaveGeoClockImage(img, strPath);

                this._GeoClockImagePath = memoryImageBuild(img, _GEOCLOCK_IMAGE_ID, img.Size);
                img.Dispose();

                this._GeoClockLastRefresh = dt;
                GUIPropertyManager.SetProperty(_TAG_GEOCLOCK_IMAGE, this._GeoClockImagePath);
            }

            return bResult;
        }

        private void geoClockDestroyImage()
        {
            if (this._GeoClockImagePath != null)
            {
                GUIPropertyManager.SetProperty(_TAG_GEOCLOCK_IMAGE, string.Empty);
                memoryImageDestroy(this._GeoClockImagePath);
                this._GeoClockImagePath = null;
            }
        }


        private void imagesDestroy()
        {
            for (int i = 0; i < this._WaeatherImages.Length; i++)
            {
                if (this._WaeatherImages[i] != null)
                    this._WaeatherImages[i].Destroy();
            }
        }

        private void imagesRefresh()
        {
            DateTime dtNow = DateTime.Now;

            for (int i = 0; i < this._WaeatherImages.Length; i++)
            {
                GUI.GUIWeatherImage wi = this._WaeatherImages[i];
                if (wi != null && (dtNow - wi.LastRefresh).TotalMinutes >= wi.WeatherImage.Period)
                    this._ImageRefreshPool.Add((o, state) => this.imageRefresh((GUI.GUIWeatherImage)o, (Pbk.Net.Http.HttpUserWebRequest)state), wi, wi.Label);
            }
        }

        private Pbk.Tasks.TaskActionResultEnum imageRefresh(GUI.GUIWeatherImage wi, Pbk.Net.Http.HttpUserWebRequest wr)
        {
            //Test to avoid multiple refresh of the same image
            if (!wi.RefreshBegin())
                return Pbk.Tasks.TaskActionResultEnum.Complete;

            Image img = null;
            Image imgBck = null;
            Image imgOver = null;

            try
            {
                if (wi.FramesAvailable && (DateTime.Now - wi.LastRefresh).TotalMinutes < wi.WeatherImage.Period)
                    return Pbk.Tasks.TaskActionResultEnum.Complete;

                //Destroy old image
                wi.SetEmptyImage();

                //Latest time
                DateTime dt = DateTime.UtcNow.AddMinutes(wi.WeatherImage.PeriodSafe * -1);

                //Rounding
                int iMin = (int)(dt.Minute / wi.WeatherImage.Period) * wi.WeatherImage.Period;
                if (iMin != dt.Minute)
                    dt = dt.AddMinutes((dt.Minute - iMin) * -1);
                if (dt.Second > 0)
                    dt = dt.AddSeconds(dt.Second * -1);

                //Max download attempts
                int iAttempts = 3;

                #region Background image
                if (!string.IsNullOrWhiteSpace(wi.WeatherImage.UrlBackground))
                {
                    wr.Url = wi.WeatherImage.UrlBackground;
                    while (iAttempts-- > 0)
                    {
                        imgBck = Pbk.Net.Http.Caching.Instance.DownloadFile<Image>(null, iLifeTime: 3600, wr: wr);
                        if (imgBck != null)
                            goto step1;

                        //Try again
                        Thread.Sleep(1000);
                    }

                    //Failed
                    return Pbk.Tasks.TaskActionResultEnum.Complete;
                }
                #endregion

            step1:

                #region Overlay image
                if (!string.IsNullOrWhiteSpace(wi.WeatherImage.UrlOverlay))
                {
                    iAttempts = 3;
                    wr.Url = wi.WeatherImage.UrlOverlay;
                    while (iAttempts-- > 0)
                    {
                        imgOver = Pbk.Net.Http.Caching.Instance.DownloadFile<Image>(null, iLifeTime: 3600, wr: wr);
                        if (imgOver != null)
                            goto step2;

                        //Try again
                        Thread.Sleep(1000);
                    }

                    //Failed
                    return Pbk.Tasks.TaskActionResultEnum.Complete;
                }
                #endregion

            step2:

                if (wi.WeatherImage.MultiImage)
                {
                    #region MultiImage

                    List<Image> images = new List<Image>();
                    List<int> durations = new List<int>();
                    int iFailedImages = 0;

                    //Max total time
                    int iTime = wi.WeatherImage.MultiImageMaxPeriod;

                    while (iTime >= 0 && images.Count < wi.WeatherImage.MultiImageMaxImages)
                    {
                        iAttempts = 3;

                        while (iAttempts-- > 0)
                        {
                            wr.Url = wi.FormatUrl(dt);
                            img = Pbk.Net.Http.Caching.Instance.DownloadFile<Image>(null, iLifeTime: 3600, wr: wr);
                            if (img != null)
                                goto mi3;
                            else if (wr.HttpResponseCode == HttpStatusCode.NotFound)
                                break;

                            //Try again
                            Thread.Sleep(1000);
                        }

                        //Failed

                        //Sometimes the most recent image is not available yet; proceed to the next image(older)
                        if (++iFailedImages <= 2 && images.Count == 0)
                            goto mi_nxt;

                        //Destroy all images
                        images.ForEach(im => im.Dispose());
                        images = null;
                        return Pbk.Tasks.TaskActionResultEnum.Complete;

                    mi3:
                        if (imgBck != null || imgOver != null)
                        {
                            if (imgBck != null)
                            {
                                //Merge with background
                                Bitmap bmp = new Bitmap(imgBck.Width, imgBck.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                                using (Graphics g = Graphics.FromImage(bmp))
                                {
                                    g.DrawImage(imgBck, 0, 0, imgBck.Width, imgBck.Height);
                                    g.DrawImage(img, 0, 0, imgBck.Width, imgBck.Height);

                                    //Merge with overlayer
                                    if (imgOver != null)
                                        g.DrawImage(imgOver, 0, 0, img.Width, img.Height);

                                    //DateTime watermark
                                    if (wi.WeatherImage.MultiImageDateImeWatermark != DateImeWatermarkEnum.None)
                                        printDateTimeWatermark(g, img.Size, wi.WeatherImage.MultiImageDateImeWatermark, dt);
                                }

                                img = bmp;

                            }
                            else
                            {
                                //Merge with overlayer
                                Bitmap bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                                using (Graphics g = Graphics.FromImage(bmp))
                                {
                                    g.DrawImage(img, 0, 0, img.Width, img.Height);
                                    g.DrawImage(imgOver, 0, 0, img.Width, img.Height);

                                    //DateTime watermark
                                    if (wi.WeatherImage.MultiImageDateImeWatermark != DateImeWatermarkEnum.None)
                                        printDateTimeWatermark(g, img.Size, wi.WeatherImage.MultiImageDateImeWatermark, dt);
                                }

                                img = bmp;

                            }
                        }
                        else
                        {
                            //DateTime watermark
                            if (wi.WeatherImage.MultiImageDateImeWatermark != DateImeWatermarkEnum.None)
                            {
                                using (Graphics g = Graphics.FromImage(img))
                                {
                                    printDateTimeWatermark(g, img.Size, wi.WeatherImage.MultiImageDateImeWatermark, dt);
                                }
                            }
                        }


                        images.Insert(0, img);
                        durations.Add(wi.WeatherImage.GUIMediaImageFrameDuration);

                        //New time (history)
                        iTime -= wi.WeatherImage.Period;

                    mi_nxt:
                        //Update target datetime
                        dt = dt.AddMinutes(wi.WeatherImage.Period * -1);
                    }

                    //Add aditional time for last frame duration
                    if (durations.Count > 0)
                        durations[durations.Count - 1] += wi.WeatherImage.GUIMediaImageLastFrameAddTime;

                    //OK
                    wi.SetImage(images.ToArray(), durations.ToArray());

                    images.ForEach(im => im.Dispose());
                    images = null;

                    #endregion
                }
                else
                {
                    #region Single image
                    Bitmap bmp = null;
                    Image[] images = null;
                    
                    try
                    {
                        //Download the main image
                        iAttempts = 3;
                        while (iAttempts-- > 0)
                        {
                            wr.Url = wi.FormatUrl(dt);
                            img = Pbk.Net.Http.Caching.Instance.DownloadFile<Image>(null, iLifeTime: wi.WeatherImage.Period, wr: wr);
                            if (img != null)
                                break;
                            else if (wr.HttpResponseCode == HttpStatusCode.NotFound)
                                break;

                            //Try again
                            Thread.Sleep(1000);
                        }

                        if (img != null)
                        {
                            //Frames count
                            System.Drawing.Imaging.FrameDimension oDimension = new System.Drawing.Imaging.FrameDimension(img.FrameDimensionsList[0]);
                            int iFrames = img.GetFrameCount(oDimension);

                            //Load frame durations
                            const int PROPERTY_ID_DELAY = 20736;
                            System.Drawing.Imaging.PropertyItem item = iFrames > 1 ? img.GetPropertyItem(PROPERTY_ID_DELAY) : null;
                            byte[] data = item != null ? data = item.Value : null;

                            images = new Image[iFrames];
                            int[] durations = new int[iFrames];

                            //Load all frames
                            for (int i = 0; i < iFrames; ++i)
                            {
                                //Select active frame
                                img.SelectActiveFrame(oDimension, i);

                                if (imgBck != null) //background + overlayer
                                {
                                    bmp = new Bitmap(imgBck.Width, imgBck.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                                    using (Graphics g = Graphics.FromImage(bmp))
                                    {
                                        g.DrawImage(imgBck, 0, 0, imgBck.Width, imgBck.Height);
                                        g.DrawImage(img, 0, 0, imgBck.Width, imgBck.Height);

                                        //Merge with overlayer
                                        if (imgOver != null)
                                            g.DrawImage(imgOver, 0, 0, imgBck.Width, imgBck.Height);
                                    }
                                }
                                else if (imgOver != null) // overlayer only
                                {
                                    //Merge with overlayer
                                    bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                                    using (Graphics g = Graphics.FromImage(bmp))
                                    {
                                        g.DrawImage(img, 0, 0, img.Width, img.Height);
                                        g.DrawImage(imgOver, 0, 0, img.Width, img.Height);
                                    }

                                }
                                else
                                    bmp = new Bitmap(img);

                                //Duration
                                if (data == null || wi.WeatherImage.GUIGifFrameDurationOverride)
                                    durations[i] = wi.WeatherImage.GUIMediaImageFrameDuration + (i == iFrames - 1 ? wi.WeatherImage.GUIMediaImageLastFrameAddTime : 0);
                                else
                                    durations[i] = BitConverter.ToInt16(data, i * 4) * 10; //base is 10ms

                                
                                images[i] = bmp;
                                bmp = null;
                            }

                            //Sucess
                            wi.SetImage(images, durations);
                        }
                    }
                    catch (Exception ex)
                    {
                        _Logger.Error("[imageRefresh] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                    }
                    finally
                    {
                        if (img != null)
                        {
                            img.Dispose();
                            img = null;
                        }

                        if (bmp != null)
                        {
                            bmp.Dispose();
                            bmp = null;
                        }

                        if (images != null)
                        {
                            for (int i = 0; i < images.Length; i++ )
                            {
                                Image im = images[i];
                                if (im != null)
                                    im.Dispose();
                            }

                            images = null;
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                _Logger.Error("[imageRefresh] Error: " + ex.Message);
            }
            finally
            {
                if (img != null)
                {
                    img.Dispose();
                    img = null;
                }

                if (imgBck != null)
                {
                    imgBck.Dispose();
                    imgBck = null;
                }

                if (imgOver != null)
                {
                    imgOver.Dispose();
                    imgOver = null;
                }

                wr.Close();
                wr.BeforeSaveToFile = null;
                wr.Tag = null;

                wi.RefreshEnd();
            }

            return Pbk.Tasks.TaskActionResultEnum.Complete;
        }

        private static void printDateTimeWatermark(Graphics g, Size sizeImage, DateImeWatermarkEnum type, DateTime dt)
        {
            //Date&Time watermark
            string strText = TimeZone.CurrentTimeZone.ToLocalTime(dt).ToString();
            float fSize = Math.Max(9.0F, (int)(sizeImage.Width * 0.025F / 0.75F) * 0.75F);
            System.Drawing.Font font = new System.Drawing.Font("Arial", fSize);
            float fX = fSize;
            float fY = type == DateImeWatermarkEnum.UpperLeft ? fSize : sizeImage.Height - (2 * fSize);
            SizeF sizeText = g.MeasureString(strText, font);
            g.FillRectangle(new SolidBrush(Color.FromArgb(128, Color.White)), fX, fY, sizeText.Width, sizeText.Height);
            g.DrawString(strText, font, Brushes.Black, fX, fY);
        }

        #endregion

        #region Callbacks

        /// <summary>
        /// Callback from GUIGraphicsContext.OnVideoWindowChanged
        /// </summary>
        private void cbVideoWindowChanged()
        {
            //_Logger.Debug("[cbVideoWindowChanged] FullScreen:{0} TV:{1}", GUIGraphicsContext.IsFullScreenVideo, g_Player.IsTV);

            lock (this._TimerRefreshWeather)
            {
                if (GUIGraphicsContext.IsFullScreenVideo)
                {
                    //Going to fullscreen mode

                    switch (Database.dbSettings.Instance.FullscreenVideoBehavior)
                    {
                        case FullscreenVideoBehaviorEnum.Sleep:
                            this._TimerRefreshWeather.Enabled = false;
                            _Logger.Debug("[cbVideoWindowChanged] Suspending weather refresh.");
                            return;

                        case FullscreenVideoBehaviorEnum.RunWhenTvPlayback:
                            if (!g_Player.IsTV)
                            {
                                this._TimerRefreshWeather.Enabled = false;
                                _Logger.Debug("[cbVideoWindowChanged] Suspending weather refresh.");
                                return;
                            }
                            break;

                        case FullscreenVideoBehaviorEnum.RunAlways:
                            break;
                    }
                }


                if (!this._TimerRefreshWeather.Enabled)
                {
                    _Logger.Debug("[cbVideoWindowChanged] Resuming weather refresh.");

                    this.setWeatherRefresh(true);
                }
            }
        }

        private void cbTimerRefreshWeather(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.doWeatherRefresh(false);
        }

        private void cbTimerRefreshLocation(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.doLocationRefresh(false);
        }

        private static void cbHttpBeforeDownload(object sender, MediaPortal.Pbk.Net.Http.HttpUserWebBeforeDownloadEventArgs e)
        {
            MediaPortal.Pbk.Net.Http.HttpUserWebRequest rq = (MediaPortal.Pbk.Net.Http.HttpUserWebRequest)sender;
            string strExt = null;
            string strContentType;
            if (rq.HttpResponseFields.TryGetValue(MediaPortal.Pbk.Net.Http.HttpHeaderField.HTTP_FIELD_CONTENT_TYPE, out strContentType))
            {
                if (strContentType.IndexOf(MediaPortal.Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_IMAGE_GIFF, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    strExt = ".gif";
                else if (strContentType.IndexOf(MediaPortal.Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_IMAGE_PNG, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    strExt = ".png";
                else if (strContentType.IndexOf(MediaPortal.Pbk.Net.Http.HttpHeaderField.HTTP_CONTENT_TYPE_IMAGE_JPG, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    strExt = ".jpg";
            }

            if (strExt == null)
            {
                _Logger.Error("[cbHttpBeforeDownload] Invalid image content type: {0}", strContentType);
                e.Abort = true;
            }
        }

        private static void cbHttpBeforeSaveToFile(object sender, MediaPortal.Pbk.Net.Http.HttpUserWebBeforeSaveToFileEventArgs e)
        {
        }

        #endregion

        #region Power events
        private void onResume()
        {
            if (this._SystemSuspended)
            {
                this._SystemSuspended = false;

                this.setWeatherRefresh(false);   

                //Refresh location tags
                this.doLocationRefresh(false);
            }
        }

        private void onSuspend()
        {
            this._SystemSuspended = true;
        }
        #endregion
    }
}
