using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using NLog;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class ProviderAccuWeather : ProviderBase, IWeatherProvider
    {
        private const string _USER_AGENT = "SAMSUNG-Android";
        private const string _URL_BASE = "http://samsungmobile.accu-weather.com/widget/samsungmobile/";
        private const string _URL_RECENT = _URL_BASE + "weather-data.asp?slat={0}&slon={1}&metric=1&langid=0";
        private const string _URL_SEARCH = _URL_BASE + "city-find.asp?returnGeoPosition=1&location={0}";

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public ProviderTypeEnum Type
        { get { return ProviderTypeEnum.ACCU_WEATHER; } }

        public string Name
        {
            get { return "AccuWeather"; }
        }

        public WeatherData GetCurrentWeatherData(Database.dbWeatherLoaction loc, int iRefreshInterval)
        {
            if (loc == null)
                return null;

            try
            {
                System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");

                string strUrl = string.Format(_URL_RECENT, loc.Latitude.ToString(ciEn), loc.Longitude.ToString(ciEn));

                using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(strUrl))
                {
                    wr.UserAgent = _USER_AGENT;
                    wr.ResponseTimeout = 30000;
                    string strContent = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<string>(null, wr: wr, iLifeTime: iRefreshInterval / 60);

                    if (strContent != null)
                    {
                        XmlDocument xml = MediaPortal.Pbk.Net.Http.WebTools.LoadXmlAndRemoveNamespace(strContent);

                        XmlNode nodeCond = xml.SelectSingleNode("//adc_database/currentconditions");
                        if (nodeCond != null)
                        {
                            DateTime dtToday = DateTime.Today;

                            int iCode;
                            WeatherData result = new WeatherData();

                            //Lat, Long
                            double dLat = loc.Latitude;
                            double dLong = loc.Longitude;

                            //DayLight saving
                            //bool bIsDayLightSaving = xml.SelectSingleNode("//adc_database/local/obsdaylight/text()").Value.Contains('1');

                            //DayLight
                            //DateTime dtNow = DateTime.Now;
                            //bool bIsDayLight = DateTime.Parse(xml.SelectSingleNode("//adc_database/planets/sun/@set").Value) >= dtNow &&
                            //    DateTime.Parse(xml.SelectSingleNode("//adc_database/planets/sun/@rise").Value) <= dtNow;

                            //Time
                            result.RefreshedAt = DateTime.Parse(nodeCond.SelectSingleNode("./observationtime/text()").Value);

                            //Location
                            result.Location = loc.Name;

                            //Condition
                            //result.ConditionText = nodeCond.SelectSingleNode("./weathertext/text()").Value;
                            iCode = remapConditionCode(int.Parse(nodeCond.SelectSingleNode("./weathericon/text()").Value));
                            result.ConditionIconCode = iCode;
                            result.ConditionTranslationCode = GetTranslationCode(iCode);

                            //Temperature
                            result.Temperature = int.Parse(nodeCond.SelectSingleNode("./temperature/text()").Value);
                            result.TemperatureFeelsLike = int.Parse(nodeCond.SelectSingleNode("./realfeel/text()").Value);

                            //Humidity
                            result.Humidity = int.Parse(nodeCond.SelectSingleNode("./humidity/text()").Value.TrimEnd('%'));

                            //Wind m/s
                            result.Wind = float.Parse(nodeCond.SelectSingleNode("./windspeed/text()").Value, ciEn) / 3.6F;

                            #region Wind direction
                            switch (nodeCond.SelectSingleNode("./winddirection/text()").Value)
                            {
                                default:
                                case "N":
                                    result.WindDirection = 0;
                                    break;

                                case "NNE":
                                    result.WindDirection = 22.5F;
                                    break;

                                case "NE":
                                    result.WindDirection = 45F;
                                    break;

                                case "ENE":
                                    result.WindDirection = 67.5F;
                                    break;

                                case "E":
                                    result.WindDirection = 90F;
                                    break;

                                case "ESE":
                                    result.WindDirection = 112.5F;
                                    break;

                                case "SE":
                                    result.WindDirection = 135F;
                                    break;

                                case "SSE":
                                    result.WindDirection = 157.5F;
                                    break;

                                case "S":
                                    result.WindDirection = 180F;
                                    break;

                                case "SSW":
                                    result.WindDirection = 202.5F;
                                    break;

                                case "SW":
                                    result.WindDirection = 225F;
                                    break;

                                case "WSW":
                                    result.WindDirection = 247.5F;
                                    break;

                                case "W":
                                    result.WindDirection = 270F;
                                    break;

                                case "WNW":
                                    result.WindDirection = 292.5F;
                                    break;

                                case "NW":
                                    result.WindDirection = 315F;
                                    break;

                                case "NNW":
                                    result.WindDirection = 337.5F;
                                    break;
                            }
                            #endregion

                            //Pressure hPa
                            result.Pressure = int.Parse(nodeCond.SelectSingleNode("./pressure/text()").Value) * 10;

                            //UV index
                            result.UVIndex = int.Parse(nodeCond.SelectSingleNode("./uvindex/@index").Value, ciEn);

                            //Rain precipitation
                            result.RainPrecipitation = float.Parse(nodeCond.SelectSingleNode("./precip/text()").Value, ciEn);

                            //Dewpoint
                            result.DewPoint = int.Parse(nodeCond.SelectSingleNode("./dewpoint/text()").Value);

                            //Visibility
                            result.Visibility = int.Parse(nodeCond.SelectSingleNode("./visibility/text()").Value) * 1000;

                            //Cloud coverage
                            result.CloudCoverage = int.Parse(nodeCond.SelectSingleNode("./cloudcover/text()").Value.TrimEnd('%'));

                            //Forecast
                            XmlNode nodeFore = xml.SelectSingleNode("//adc_database/forecast");
                            result.ForecastDays = new List<ForecastDay>();
                            foreach (XmlNode nodeDay in nodeFore.SelectNodes(".//day"))
                            {
                                ForecastDay day = new ForecastDay();

                                //Date
                                day.Date = DateTime.ParseExact(nodeDay.SelectSingleNode("./obsdate/text()").Value, "M/d/yyyy", null);

                                if (day.Date < dtToday)
                                    continue;


                                XmlNode nodeDayTime = nodeDay.SelectSingleNode("./daytime");

                                //Temperature MIN
                                day.TemperatureMin = int.Parse(nodeDayTime.SelectSingleNode("./lowtemperature/text()").Value);

                                //Temperature MAX
                                day.TemperatureMax = int.Parse(nodeDayTime.SelectSingleNode("./hightemperature/text()").Value);

                                //Condition
                                //day.ConditionText = modeDayTime.SelectSingleNode(".//txtshort/text()").Value;

                                //Image
                                day.ConditionIconCode = remapConditionCode(int.Parse(nodeDayTime.SelectSingleNode("./weathericon/text()").Value));
                                day.ConditionTranslationCode = GetTranslationCode(day.ConditionIconCode);

                                result.ForecastDays.Add(day);
                            }

                            result.Longitude = dLong;
                            result.Latitude = dLat;

                            return result;
                        }
                    }
                }

                _Logger.Error("[GetCurrentWeatherData] Failed to downloadad weather data.");
            }
            catch (Exception ex)
            {
                _Logger.Error("[GetCurrentWeatherData] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
            }

            return null;
        }

        public IEnumerable<Database.dbWeatherLoaction> Search(string strQuery)
        {
            if (string.IsNullOrWhiteSpace(strQuery))
                yield break;

            string strUrl = string.Format(_URL_SEARCH, System.Web.HttpUtility.UrlEncode(strQuery));
            string strContent = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<string>(strUrl, iResponseTimout: 30000, strUserAgent: _USER_AGENT);
            if (strContent != null)
            {
                System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                XmlDocument xml = MediaPortal.Pbk.Net.Http.WebTools.LoadXmlAndRemoveNamespace(strContent);

                Database.dbWeatherLoaction location;
                foreach (XmlNode nodeLoc in xml.SelectNodes("//adc_database/citylist/location"))
                {
                    location = new Database.dbWeatherLoaction()
                    {
                        LocationID = nodeLoc.Attributes["location"].Value.Replace("cityId:", ""),
                        ObservationLocation = nodeLoc.Attributes["state"].Value,
                        Name = nodeLoc.Attributes["city"].Value,
                        Country = nodeLoc.Attributes["state"].Value,
                        Longitude = double.Parse(nodeLoc.Attributes["longitude"].Value, ciEn),
                        Latitude = double.Parse(nodeLoc.Attributes["latitude"].Value, ciEn),
                        Provider = ProviderTypeEnum.FORECA
                    };

                    int i = location.Country.IndexOf(' ');
                    if (i > 0)
                        location.Country = location.Country.Substring(0, i);

                    yield return location;
                }
            }
        }


        private static int remapConditionCode(int iCode)
        {
            #region Info
            //03: Partly sunny
            //04: Warmer, Clouds and sun, Clouds giving way to some sun
            //06: Variable cloudiness
            //07: Perhaps a shower
            //12: A little rain, A couple of showers, Perhaps a shower, Occasional rain and drizzle, A brief shower or two
            //14: A couple of showers
            //15: A couple of showers and thunderstorms
            //18: Periods of rain, Rain
            //34: Mainly clear
            //35: Partly cloudy
            //36: Partly to mostly cloudy
            //38: Mostly cloudy
            //39: A couple of showers

            //Code:Day:Night:Text
            //1  Yes  No Sunny 
            //2  Yes  No Mostly Sunny 
            //3  Yes  No Partly Sunny 
            //4  Yes  No Intermittent Clouds 
            //5  Yes  No Hazy Sunshine 
            //6  Yes  No Mostly Cloudy 
            //7  Yes Yes Cloudy 
            //8  Yes Yes Dreary (Overcast) 
            //11 Yes Yes Fog 
            //12 Yes Yes Showers 
            //13 Yes No  Mostly Cloudy w/ Showers 
            //14 Yes No  Partly Sunny w/ Showers 
            //15 Yes Yes T-Storms 
            //16 Yes No  Mostly Cloudy w/ T-Storms 
            //17 Yes No  Partly Sunny w/ T-Storms 
            //18 Yes Yes Rain 
            //19 Yes Yes Flurries 
            //20 Yes No  Mostly Cloudy w/ Flurries 
            //21 Yes No  Partly Sunny w/ Flurries 
            //22 Yes Yes Snow 
            //23 Yes No  Mostly Cloudy w/ Snow 
            //24 Yes Yes Ice 
            //25 Yes Yes Sleet 
            //26 Yes Yes Freezing Rain 
            //29 Yes Yes Rain and Snow 
            //30 Yes Yes Hot 
            //31 Yes Yes Cold 
            //32 Yes Yes Windy 
            //33 No  Yes Clear 
            //34 No  Yes Mostly Clear 
            //35 No  Yes Partly Cloudy 
            //36 No  Yes Intermittent Clouds 
            //37 No  Yes Hazy Moonlight 
            //38 No  Yes Mostly Cloudy 
            //39 No  Yes Partly Cloudy w/ Showers 
            //40 No  Yes Mostly Cloudy w/ Showers 
            //41 No  Yes Partly Cloudy w/ T-Storms 
            //42 No  Yes Mostly Cloudy w/ T-Storms 
            //43 No  Yes Mostly Cloudy w/ Flurries 
            //44 No  Yes Mostly Cloudy w/ Snow
            #endregion

            switch (iCode - 1)
            {
                case 0:
                case 32:
                    return 32; //"clear_sunny", 32
                case 1:
                case 2:
                case 33:
                case 34:
                    return 30; //"partly_cloudy", 30
                case 3:
                case 6:
                case 35:
                    return 26; //"cloudy", 26
                case 4:
                case 36:
                    return 11; //"haze", 11
                case 5:
                case 37:
                    return 28; //"mostly_cloudy", 28
                case 7:
                    return 19; //"dust", 19
                case 10:
                    return 20; //"fog", 20
                case 11:
                case 12:
                case 13:
                case 38:
                case 39:
                    return 11; //"showers", 11
                case 14:
                    return 38; //"thunderstorms", 38
                case 15:
                case 16:
                case 40:
                case 41:
                    return 38; //"thundershowers", 38
                case 17:
                    return 11; //"rain", 11
                case 18:
                case 19:
                case 20:
                case 42:
                    return 13; //"snow_flurries", 13
                case 21:
                case 22:
                case 43:
                    return 16; //"snow", 16
                case 23:
                    return 16; //"ice", 16
                case 24:
                    return 18; //"sleet", 18
                case 25:
                    return 10; //"freezing_rain", 10
                case 28:
                    return 10; //"mixed_rain_and_snow", 10
                case 29:
                    return 36; //"hot", 36
                case 30:
                    return 7; //"cold", 7
                case 31:
                    return 23; //"windy", 23
                default:
                    return int.MinValue;
            }
        }
    }
}
