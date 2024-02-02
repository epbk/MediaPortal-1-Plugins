using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using Newtonsoft.Json.Linq;
using NLog;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class ProviderAccuWeather : ProviderBase, IWeatherProvider
    {
        //https://www.accuweather.com/en/search-locations?query=prague
        //https://www.accuweather.com/en/cz/vsetín/126701/current-weather/126701
        //https://www.accuweather.com/en/cz/vsetín/126701/weather-forecast/126701
        //https://www.accuweather.com/en/cz/prague/125594/daily-weather-forecast/125594


        private const string _URL_API_BASE = "https://dataservice.accuweather.com";
        private const string _URL_API_SEARCH = _URL_API_BASE + "/locations/v1/cities/search?apikey={0}&q={1}";
        private const string _URL_API_WEATHER_CURRENT = _URL_API_BASE + "/currentconditions/v1/{1}?apikey={0}&details=true&metric=true";
        private const string _URL_API_WEATHER_FORECAST = _URL_API_BASE + "/forecasts/v1/daily/1day/{1}?apikey={0}&details=true&metric=true";
        private const string _URL_API_WEATHER_FORECAST5 = _URL_API_BASE + "/forecasts/v1/daily/5day/{1}?apikey={0}&details=true&metric=true";
        private const string _URL_API_WEATHER_FORECAST10 = _URL_API_BASE + "/forecasts/v1/daily/10day/{1}?apikey={0}&details=true&metric=true";
        

        private const string _URL_BASE = "https://www.accuweather.com";
        private const string _URL_SEARCH = _URL_BASE + "/en/search-locations?query={0}";

        private const string _REQUEST_WEATHER_CURRENT = "current-weather";
        private const string _REQUEST_WEATHER_FORECAST = "daily-weather-forecast";

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public override ProviderTypeEnum Type
        { get { return ProviderTypeEnum.ACCU_WEATHER; } }

        public override string Name
        {
            get { return "AccuWeather"; }
        }

        public WeatherData GetCurrentWeatherData(Database.dbProfile loc, int iRefreshInterval)
        {
            if (loc == null)
                return null;

            try
            {
                //API
                if (!string.IsNullOrWhiteSpace(loc.ProviderApiKey))
                    return this.apiGetCurrentWeatherData(loc, iRefreshInterval);

                System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");

                string strUrl = _URL_BASE + string.Format(loc.LocationID, _REQUEST_WEATHER_CURRENT);
                using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(strUrl))
                {
                    wr.ResponseTimeout = 30000;
                    wr.AcceptLanguage = null;
                    XmlDocument xml = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<MediaPortal.Pbk.Net.Http.HtmlDocument>(null, wr: wr, iLifeTime: iRefreshInterval / 60);

                    if (xml != null)
                    {
                        XmlNode nodeCond = xml.SelectSingleNode("//*[@class[contains(.,'current-weather-card')]]");
                        if (nodeCond != null)
                        {
                            XmlNode nodeInfo = nodeCond.SelectSingleNode("//*[@class[contains(.,'current-weather-info')]]");
                            XmlNode nodeDetails = nodeCond.SelectSingleNode("//*[@class[contains(.,'current-weather-details')]]");
                            if (nodeInfo != null && nodeDetails != null)
                            {
                                XmlNode node;
                                Match m;

                                DateTime dtToday = DateTime.Today;

                                int iCode;
                                WeatherData result = new WeatherData();

                                //Time
                                result.RefreshedAt = DateTime.Now;

                                //Location
                                result.Location = loc.Name;

                                //Condition
                                //result.ConditionText = nodeCond.SelectSingleNode("./weathertext/text()").Value;
                                Regex regexIcon = new Regex("weathericons/(?<id>\\d+)\\.svg");
                                iCode = remapConditionCode(int.Parse(regexIcon.Match(nodeInfo.SelectSingleNode(".//svg/@data-src").Value).Groups["id"].Value));
                                result.ConditionIconCode = iCode;
                                result.ConditionTranslationCode = GetTranslationCode(iCode);

                                //Temperature
                                result.Temperature = parseTemperature(nodeInfo.SelectSingleNode(".//div[@class='display-temp']/text()").Value);
                                result.TemperatureFeelsLike = parseTemperature(nodeDetails.SelectSingleNode(".//div[./div[1][text()[starts-with(.,'RealFeel')]]]/div[2]/text()").Value);

                                

                                //Humidity
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Humidity']]/div[2]/text()")) != null)
                                    result.Humidity = int.Parse(node.Value.TrimEnd('%'));

                                //Wind
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Wind']]/div[2]/text()")) != null)
                                {
                                    m = Regex.Match(node.Value, "(?<dir>[^\\s]+)\\s(?<val>\\d+)");

                                    //Wind m/s
                                    result.Wind = float.Parse(m.Groups["val"].Value, ciEn) / 3.6F;

                                    #region Wind direction
                                    switch (m.Groups["dir"].Value)
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
                                }
                                //Pressure hPa
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Pressure']]/div[2]/text()")) != null)
                                    result.Pressure = int.Parse(Regex.Match(node.Value, "(?<val>\\d+)\\s+mb").Groups["val"].Value);

                                //UV index
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Max UV Index']]/div[2]/text()")) != null)
                                    result.UVIndex = int.Parse(Regex.Match(node.Value, "(?<val>\\d+)").Groups["val"].Value);

                                //Rain precipitation
                                //result.RainPrecipitation = float.Parse(nodeCond.SelectSingleNode("./precip/text()").Value, ciEn);

                                //Dewpoint
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Dew Point']]/div[2]/text()")) != null)
                                    result.DewPoint = parseTemperature(node.Value);

                                //Visibility
                                if((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Visibility']]/div[2]/text()")) != null)
                                    result.Visibility = int.Parse(Regex.Match(node.Value, "(?<val>\\d+)").Groups["val"].Value) * 1000;

                                //Cloud coverage
                                if ((node = nodeDetails.SelectSingleNode(".//div[./div[1][text()='Cloud Cover']]/div[2]/text()")) != null)
                                    result.CloudCoverage = int.Parse(node.Value.TrimEnd('%'));

                                //Forecast
                                wr.Url = _URL_BASE + string.Format(loc.LocationID, _REQUEST_WEATHER_FORECAST);
                                wr.ResponseTimeout = 30000;
                                wr.AcceptLanguage = null;
                                xml = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<MediaPortal.Pbk.Net.Http.HtmlDocument>(null, wr: wr, iLifeTime: iRefreshInterval / 60);
                                if (xml != null)
                                {
                                    string strToday = dtToday.ToString("M\\/d");
                                    result.ForecastDays = new List<ForecastDay>();
                                    foreach (XmlNode nodeDay in xml.SelectNodes(".//a[@class[contains(.,'daily-forecast-card')]]"))
                                    {
                                        ForecastDay day = new ForecastDay();

                                        //Date
                                        //string strDate = nodeDay.SelectSingleNode(".//*[@class='date']//*[@class[contains(.,'sub date')]]/text()").Value;
                                        //if (strDate.Equals(strToday))
                                        //{
                                        //    result.ForecastDays.Clear();
                                        //    continue;
                                        //}

                                        day.Date = dtToday.AddDays(result.ForecastDays.Count + 1);

                                        //Temperature MIN
                                        day.TemperatureMin = parseTemperature(nodeDay.SelectSingleNode(".//div[@class='temp']//*[@class='low']/text()").Value);

                                        //Temperature MAX
                                        day.TemperatureMax = parseTemperature(nodeDay.SelectSingleNode(".//div[@class='temp']//*[@class='high']/text()").Value);

                                        //Condition
                                        //day.ConditionText = modeDayTime.SelectSingleNode(".//txtshort/text()").Value;

                                        //Image
                                        day.ConditionIconCode = remapConditionCode(int.Parse(regexIcon.Match(nodeDay.SelectSingleNode(".//svg/@data-src").Value).Groups["id"].Value));
                                        day.ConditionTranslationCode = GetTranslationCode(day.ConditionIconCode);

                                        result.ForecastDays.Add(day);


                                        if (result.ForecastDays.Count == 10)
                                            break;
                                    }
                                }

                                return result;
                            }
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

        public IEnumerable<Database.dbProfile> Search(string strQuery, string strApiKey)
        {
            if (!string.IsNullOrWhiteSpace(strApiKey))
            {
                //API
                foreach (Database.dbProfile item in this.apiSearch(strApiKey, strQuery))
                    yield return item;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(strQuery))
                    yield break;

                string strUrl = string.Format(_URL_SEARCH, System.Web.HttpUtility.UrlEncode(strQuery));
                using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(strUrl))
                {
                    wr.ResponseTimeout = 30000;
                    wr.AcceptLanguage = null;
                    XmlDocument xml = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<MediaPortal.Pbk.Net.Http.HtmlDocument>(null, wr: wr, iLifeTime: 30);
                    if (xml != null)
                    {
                        Database.dbProfile location;
                        foreach (XmlNode nodeLoc in xml.SelectNodes("//*[@class[contains(.,'locations-list')]]//a[@href][./p[@class='location-name']][./p[@class='location-long-name']]"))
                        {
                            try
                            {
                                string str = nodeLoc.SelectSingleNode("./p[@class='location-long-name']").InnerText;
                                int iIdx = str.LastIndexOf(' ');
                                string strUrlLoc = nodeLoc.Attributes["href"].Value;
                                if (strUrlLoc.StartsWith("/"))
                                    strUrlLoc = _URL_BASE + strUrlLoc;

                                location = new Database.dbProfile()
                                {
                                    LocationID = strUrlLoc,
                                    ObservationLocation = str,
                                    Name = nodeLoc.SelectSingleNode("./p[@class='location-name']").InnerText,
                                    Country = iIdx > 0 ? str.Substring(iIdx + 1) : string.Empty,
                                    //Longitude = double.Parse(nodeLoc.Attributes["longitude"].Value, ciEn),
                                    //Latitude = double.Parse(nodeLoc.Attributes["latitude"].Value, ciEn),
                                    Provider = ProviderTypeEnum.ACCU_WEATHER
                                };
                            }
                            catch (Exception ex)
                            {
                                _Logger.Error("[Search] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
                                yield break;
                            }

                            yield return location;
                        }
                    }
                }
            }
        }

        public void FinalizeLocationData(Database.dbProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.ProviderApiKey))
            {
                //this.apiGetCurrentWeatherData(profile, 1800);
                return;
            }

            if (!profile.LocationID.StartsWith("/"))
            {
                try
                {
                    using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(profile.LocationID))
                    {
                        wr.ResponseTimeout = 30000;
                        wr.AcceptLanguage = null;
                        MediaPortal.Pbk.Net.Http.HtmlDocument xml = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<MediaPortal.Pbk.Net.Http.HtmlDocument>(null, wr: wr, iLifeTime: 30);

                        XmlNode n = xml.SelectSingleNode("//script[@type='application/ld+json']/text()[contains(.,'mainEntityOfPage')]");
                        JToken jWebSite = Newtonsoft.Json.JsonConvert.DeserializeObject<JToken>(n.Value);
                        string strWebUrl = (string)jWebSite["mainEntityOfPage"]["relatedLink"];

                        n = xml.SelectSingleNode("//script[@type='application/ld+json']/text()[contains(.,'latitude')]");
                        JToken jPlace = Newtonsoft.Json.JsonConvert.DeserializeObject<JToken>(n.Value);

                        Match m = Regex.Match(strWebUrl, "/(?<countryA>[^/]+)/(?<countryB>[^/]+)/(?<city>[^/]+)/(?<id1>[^/]+)/[^/]+/(?<id2>\\d+)");
                        if (m.Success)
                        {
                            profile.LocationID = string.Format("/{0}/{1}/{2}/{3}/{{0}}/{4}",
                                m.Groups["countryA"].Value,
                                m.Groups["countryB"].Value, 
                                m.Groups["city"].Value,
                                m.Groups["id1"].Value,
                                m.Groups["id2"].Value
                                );
                            profile.Latitude = (double)jPlace["geo"]["latitude"];
                            profile.Longitude = (double)jPlace["geo"]["longitude"];
                        }
                    }

                    //this.GetCurrentWeatherData(profile, 1800);
                }
                catch (Exception ex)
                {
                    _Logger.Error("[ValidateLocationData] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace);
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

        private int parseTemperature(string strText)
        {
            int iIdx = strText.IndexOf('°');
            bool bSlah = strText[0] == '/';
            return int.Parse(iIdx > 0 ? strText.Substring(bSlah ? 1 : 0, iIdx - (bSlah ? 1 : 0)) : strText);
        }


        private WeatherData apiGetCurrentWeatherData(Database.dbProfile loc, int iRefreshInterval)
        {
            string strUrl = string.Format(_URL_API_WEATHER_CURRENT, loc.ProviderApiKey, loc.LocationID);
            JToken j = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<JToken>(strUrl, iLifeTime: iRefreshInterval / 60);
            if (j != null)
            {
                JToken jData = j[0];

                DateTime dtToday = DateTime.Today;

                int iCode;
                WeatherData result = new WeatherData();

                //Time
                result.RefreshedAt = (DateTime)jData["LocalObservationDateTime"];

                //Location
                result.Location = loc.Name;

                //Condition
                //result.ConditionText = nodeCond.SelectSingleNode("./weathertext/text()").Value;
                iCode = (int)jData["WeatherIcon"];
                result.ConditionIconCode = remapConditionCode(iCode);
                result.ConditionTranslationCode = GetTranslationCode(iCode);

                //Temperature
                result.Temperature = (int)jData["Temperature"]["Metric"]["Value"];
                result.TemperatureFeelsLike = (int)jData["RealFeelTemperature"]["Metric"]["Value"];

                //Humidity
                result.Humidity = (int)jData["RelativeHumidity"];

                //Wind m/s
                result.Wind = (float)jData["Wind"]["Speed"]["Metric"]["Value"] / 3.6F;

                //Wind direction
                result.WindDirection = (int)jData["Wind"]["Direction"]["Degrees"];
               
                //Pressure hPa
                result.Pressure = (int)jData["Pressure"]["Metric"]["Value"];

                //UV index
                result.UVIndex = (int)jData["UVIndex"];

                //Rain precipitation
                result.RainPrecipitation = (float)jData["Precip1hr"]["Metric"]["Value"];

                //Dewpoint
                result.DewPoint = (int)jData["DewPoint"]["Metric"]["Value"];

                //Visibility
                result.Visibility = (int)((float)jData["Visibility"]["Metric"]["Value"] * 1000);

                //Cloud coverage
                result.CloudCoverage = (int)jData["CloudCover"];

                //Forecast
                strUrl = string.Format(_URL_API_WEATHER_FORECAST5, loc.ProviderApiKey, loc.LocationID);
                j = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<JToken>(strUrl, iLifeTime: 30);
                if (j != null)
                {
                    result.ForecastDays = new List<ForecastDay>();
                    foreach (JToken jDay in j["DailyForecasts"])
                    {
                        ForecastDay day = new ForecastDay();

                        //Date
                        day.Date = ((DateTime)jDay["Date"]).Date;
                        if (day.Date < dtToday)
                            continue;

                        //Temperature MIN
                        day.TemperatureMin = (int)jDay["Temperature"]["Minimum"]["Value"];

                        //Temperature MAX
                        day.TemperatureMax = (int)jDay["Temperature"]["Maximum"]["Value"];

                        //Condition
                        //day.ConditionText = modeDayTime.SelectSingleNode(".//txtshort/text()").Value;

                        //Image
                        day.ConditionIconCode = remapConditionCode((int)jDay["Day"]["Icon"]);
                        day.ConditionTranslationCode = GetTranslationCode(day.ConditionIconCode);

                        result.ForecastDays.Add(day);


                        if (result.ForecastDays.Count == 10)
                            break;
                    }
                }

                return result;
            }
            else
                _Logger.Error("[apiGetCurrentWeatherData] Failed connecto to the server.");

            return null;
        }

        private IEnumerable<Database.dbProfile> apiSearch(string strQuery, string strApiKey)
        {
            string strUrl = string.Format(_URL_API_SEARCH, strApiKey, System.Web.HttpUtility.UrlEncode(strQuery));
            JToken j = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<JToken>(strUrl, iLifeTime: 30);
            if (j != null)
            {
                Database.dbProfile location;
                foreach (JToken jItem in j)
                {
                    location = new Database.dbProfile()
                    {
                        LocationID = (string)jItem["Key"],
                        Name = (string)jItem["LocalizedName"],
                        Country = (string)jItem["Country"]["LocalizedName"],
                        Longitude = (double)jItem["GeoPosition"]["Longitude"],
                        Latitude = (double)jItem["GeoPosition"]["Latitude"],
                        Provider = ProviderTypeEnum.ACCU_WEATHER,
                        ProviderApiKey = strApiKey
                    };

                    location.ObservationLocation = location.Name + ", " + location.Country;

                    yield return location;
                }
            }
            else
                _Logger.Error("[apiSearch] Failed connecto to the server.");
        }
    }
}
