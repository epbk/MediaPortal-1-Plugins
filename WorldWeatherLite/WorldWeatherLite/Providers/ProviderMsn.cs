using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Globalization;
using NLog;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class ProviderMsn : ProviderBase, IWeatherProvider
    {
        //https://weather.codes/czech-republic/
        //http://weather.service.msn.com/data.aspx?weasearchstr=Holesov&src=current
        //http://weather.service.msn.com/data.aspx?weadegreetype=C&culture=en-US&wealocations=wc:EZXX0028&src=current
        //http://weather.service.msn.com/data.aspx?weadegreetype=C&culture=cs-CZ&wealocations=wc:EZXX0028&src=current
        //string strUrl = "http://weather.service.msn.com/data.aspx?weadegreetype=C&culture=en-US&wealocations=wc:" + this._Settings.Location + "&src=current";

        private const string _URL_BASE = "http://weather.service.msn.com";
        private const string _URL_RECENT = _URL_BASE + "/data.aspx?weadegreetype=C&culture=en-US&wealocations={0}&src=current";
        private const string _URL_SEARCH = _URL_BASE + "/data.aspx?weasearchstr={0}&src=current";

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();

        public override ProviderTypeEnum Type
        { get { return ProviderTypeEnum.MSN; } }

        public override string Name
        {
            get { return "msn.com"; }
        }

        public WeatherData GetCurrentWeatherData(Database.dbProfile loc, int iRefreshInterval)
        {
            if (loc == null || string.IsNullOrWhiteSpace(loc.LocationID))
                return null;

            try
            {
                string strUrl = string.Format(_URL_RECENT, loc.LocationID);

                string strContent;
                using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(strUrl))
                {
                    wr.ResponseTimeout = 30000;
                    strContent = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<string>(null, wr: wr, iLifeTime: iRefreshInterval / 60);
                }

                if (strContent != null)
                {
                    XmlDocument xml = MediaPortal.Pbk.Net.Http.WebTools.LoadXmlAndRemoveNamespace(strContent);

                    int iCode;
                    WeatherData result = new WeatherData();

                    XmlNode nodeWeather = xml.SelectSingleNode("//weatherdata/weather");

                    System.Globalization.CultureInfo ciEn = System.Globalization.CultureInfo.GetCultureInfo("en-US");

                    //Lat, Long
                    double dLat = double.Parse(nodeWeather.Attributes["lat"].Value, ciEn);
                    double dLong = double.Parse(nodeWeather.Attributes["long"].Value, ciEn);

                    //Current weather
                    XmlNode nodeWeatherCurrent = nodeWeather.SelectSingleNode("./current");

                    //Time
                    result.RefreshedAt = DateTime.ParseExact(nodeWeatherCurrent.Attributes["date"].Value, "yyyy-MM-dd", null)
                        + TimeSpan.ParseExact(nodeWeatherCurrent.Attributes["observationtime"].Value, "hh\\:mm\\:ss", null);

                    //Location
                    result.Location = nodeWeatherCurrent.Attributes["observationpoint"].Value;

                    //Sunrise, Sunset
                    DateTime dt = DateTime.Today;
                    double dSunRise, dSunSet;
                    Utils.Sun.SunriseSunset(dt.Year, dt.Month, dt.Day, dLat, dLong, out dSunRise, out dSunSet);
                    TimeSpan utcOffset = System.TimeZone.CurrentTimeZone.GetUtcOffset(dt);
                    dSunRise += utcOffset.Hours;
                    dSunSet += utcOffset.Hours;
                    result.SunRise = dt.AddHours(dSunRise);
                    result.SunSet = dt.AddHours(dSunSet);

                    //Condition
                    //result.ConditionText = nodeWeatherCurrent.Attributes["skytext"].Value;
                    iCode = int.Parse(nodeWeatherCurrent.Attributes["skycode"].Value);
                    result.ConditionIconCode = iCode;
                    result.ConditionTranslationCode = GetTranslationCode(iCode);

                    //Temperature
                    result.Temperature = int.Parse(nodeWeatherCurrent.Attributes["temperature"].Value);
                    result.TemperatureFeelsLike = (int.Parse(nodeWeatherCurrent.Attributes["feelslike"].Value));

                    //Humidity
                    result.Humidity = int.Parse(nodeWeatherCurrent.Attributes["humidity"].Value);

                    //Wind
                    string strText = nodeWeatherCurrent.Attributes["windspeed"].Value;
                    double d = (double)int.Parse(nodeWeatherCurrent.Attributes["windspeed"].Value.Split(' ')[0]);
                    if (strText.EndsWith("mph", StringComparison.CurrentCultureIgnoreCase))
                        result.Wind = (float)(d * 1609 / 3600);
                    else
                        result.Wind = (float)(d * 1000 / 3600);

                    //Wind direction
                    string strWindDir = nodeWeatherCurrent.Attributes["winddisplay"].Value.Split(' ')[2].ToLowerInvariant();
                    switch (strWindDir)
                    {
                        case "north":
                            result.WindDirection = 0;
                            break;

                        case "northeast":
                            result.WindDirection = 45;
                            break;

                        case "east":
                            result.WindDirection = 90;
                            break;

                        case "southeast":
                            result.WindDirection = 135;
                            break;

                        case "south":
                            result.WindDirection = 180;
                            break;

                        case "southwest":
                            result.WindDirection = 225;
                            break;

                        case "west":
                            result.WindDirection = 270;
                            break;

                        case "northwest":
                            result.WindDirection = 315;
                            break;
                    }

                    //Forecast
                    result.ForecastDays = new List<ForecastDay>();
                    XmlNodeList nodesWeatherForecast = nodeWeather.SelectNodes("./forecast");
                    for (int i = 0; i < nodesWeatherForecast.Count; i++)
                    {
                        ForecastDay day = new ForecastDay();

                        XmlNode node = nodesWeatherForecast[i];

                        //Temperature MIN
                        day.TemperatureMin = int.Parse(node.Attributes["low"].Value);

                        //Temperature MAX
                        day.TemperatureMax = int.Parse(node.Attributes["high"].Value);

                        //Condition
                        //day.ConditionText = node.Attributes["skytextday"].Value;

                        //Image
                        day.ConditionIconCode = int.Parse(node.Attributes["skycodeday"].Value);
                        day.ConditionTranslationCode = GetTranslationCode(iCode);

                        //Date
                        day.Date = DateTime.ParseExact(node.Attributes["date"].Value, "yyyy-MM-dd", null);


                        result.ForecastDays.Add(day);
                    }

                    result.Longitude = dLong;
                    result.Latitude = dLat;

                    //OK
                    return result;
                }
                else
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
            if (string.IsNullOrWhiteSpace(strQuery))
                yield break;;

            string strUrl = string.Format(_URL_SEARCH, System.Web.HttpUtility.UrlEncode(strQuery));
            string strContent = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<string>(strUrl, iResponseTimout: 30000 );
            if (strContent != null)
            {
                XmlDocument xml = MediaPortal.Pbk.Net.Http.WebTools.LoadXmlAndRemoveNamespace(strContent);
                CultureInfo ciEn = CultureInfo.GetCultureInfo("en-US");
                Database.dbProfile location;
                XmlNodeList nodes = xml.SelectNodes("//weatherdata/weather");
                for (int i = 0; i < nodes.Count; i++)
                {
                    XmlNode node = nodes[i];

                    string strName, strCountry;
                    string strLocation = node.Attributes["weatherlocationname"].Value;
                    int iIdx = strLocation.IndexOf(',');
                    if (iIdx > 0)
                    {
                        strName = strLocation.Substring(0, iIdx);
                        strCountry = strLocation.Substring(iIdx + 1).Trim();
                    }
                    else
                    {
                        strName = strLocation;
                        strCountry = string.Empty;
                    }

                    location = new Database.dbProfile()
                    {
                        LocationID = node.Attributes["weatherlocationcode"].Value,
                        ObservationLocation = strLocation,
                        Name = strName,
                        Country = strCountry,
                        Longitude = double.Parse(node.Attributes["long"].Value, ciEn),
                        Latitude = double.Parse(node.Attributes["lat"].Value, ciEn),
                        Provider = ProviderTypeEnum.MSN
                    };

                    yield return location;
                }
            }
        }

        public void FinalizeLocationData(Database.dbProfile profile)
        { }

       
    }
}
