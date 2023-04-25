using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NLog;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public class ProviderForeca : ProviderBase, IWeatherProvider
    {
        //{"symbols":{"100":"Mostly clear","110":"Mostly clear, slight possibility of rain","111":"Mostly clear, slight possibility of wet snow","112":"Mostly clear, slight possibility of snow","120":"Mostly clear, slight possibility of rain","121":"Mostly clear, slight possibility of wet snow","122":"Mostly clear, slight possibility of snow","130":"Mostly clear, slight possibility of rain","131":"Mostly clear, slight possibility of wet snow","132":"Mostly clear, slight possibility of snow","140":"Mostly clear, possible thunderstorms with rain","141":"Mostly clear, possible thunderstorms with wet snow","142":"Mostly clear, possible thunderstorms with snow","200":"Partly cloudy","210":"Partly cloudy and light rain","211":"Partly cloudy and light wet snow","212":"Partly cloudy and light snow","220":"Partly cloudy and showers","221":"Partly cloudy and wet snow showers","222":"Partly cloudy and snow showers","230":"Partly cloudy and rain","231":"Partly cloudy and wet snow\n","232":"Partly cloudy and snow","240":"Partly cloudy, possible thunderstorms with rain","241":"Partly cloudy, possible thunderstorms with wet snow","242":"Partly cloudy, possible thunderstorms with snow","300":"Cloudy","310":"Cloudy and light rain","311":"Cloudy and light wet snow","312":"Cloudy and light snow","320":"Cloudy and showers","321":"Cloudy and wet snow showers","322":"Cloudy and snow showers","330":"Cloudy and rain","331":"Cloudy and wet snow","332":"Cloudy and snow","340":"Cloudy, thunderstorms with rain","341":"Cloudy, thunderstorms with wet snow","342":"Cloudy, thunderstorms with snow","400":"Overcast","410":"Overcast and light rain","411":"Overcast and light wet snow","412":"Overcast and light snow","420":"Overcast and showers","421":"Overcast and wet snow showers","422":"Overcast and snow showers","430":"Overcast and rain","431":"Overcast and wet snow","432":"Overcast and snow","440":"Overcast, thunderstorms with rain","441":"Overcast, thunderstorms with wet snow","442":"Overcast, thunderstorms with snow","500":"Thin upper cloud","600":"Fog","000":"Clear"}}

        //https://www.foreca.com/103075606/Hole%C5%A1ov-Krom%C4%9B%C5%99%C3%AD%C5%BE-District-Czech-Republic
        //https://www.foreca.com/103062339/Vset%C3%ADn-Zl%C3%ADn-Region-Czech-Republic

        //https://api.foreca.net/data/recent/103075606.json
        //https://api.foreca.net/data/favorites/103075606.json

        /*
        https://api.foreca.net/locations/search/holesov.json?limit=30&format=legacy&lang=cs
        id: "103075606"

        https://api.foreca.net/data/daily/103075606.json
        https://api.foreca.net/data/favorites/103075606.json

        https://api.foreca.net/locations/17.9962,49.3387.json?accuracy=&legacyFormat=true&lang=cs

        vsetin
        https://api.foreca.net/data/daily/103062339.json
        */


        private const string _URL_BASE = "https://api.foreca.net";
        private const string _URL_RECENT = _URL_BASE + "/data/recent/{0}.json";
        private const string _URL_DAILY = _URL_BASE + "/data/daily/{0}.json";
        private const string _URL_SEARCH = _URL_BASE + "/locations/search/{0}.json?limit=30&lang=en";

        private static NLog.Logger _Logger = LogManager.GetCurrentClassLogger();


        public string Name
        {
            get { return "Foreca"; }
        }

        public WeatherData GetCurrentWeatherData(Database.dbWeatherLoaction loc, int iRefreshInterval)
        {
            if (loc == null || string.IsNullOrWhiteSpace(loc.LocationID))
                return null;

            try
            {
                string strUrl = string.Format(_URL_RECENT, loc.LocationID);

                JToken j;
                using (MediaPortal.Pbk.Net.Http.HttpUserWebRequest wr = new MediaPortal.Pbk.Net.Http.HttpUserWebRequest(strUrl))
                {
                    wr.ResponseTimeout = 30000;
                    j = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<JToken>(null, wr: wr, iLifeTime: iRefreshInterval / 60);

                    if (j != null)
                    {
                        JToken jresult = j[loc.LocationID];
                        if (jresult != null)
                        {
                            uint wCode;
                            Language.TranslationEnum transCode;
                            WeatherData result = new WeatherData();

                            //Lat, Long
                            double dLat = loc.Latitude;
                            double dLong = loc.Longitude;

                            //Time
                            result.RefreshedAt = (DateTime)jresult["updated"];

                            //Location
                            result.Location = loc.Name;

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
                            wCode = parseConditionSymbol((string)jresult["symb"]);
                            result.ConditionIconCode = getIconCode(wCode, out transCode);

                            //Translation
                            result.ConditionTranslationCode = transCode;

                            //Temperature
                            result.Temperature = (int)jresult["temp"];
                            result.TemperatureFeelsLike = (int)jresult["flike"]; ;

                            //Humidity
                            result.Humidity = (int)jresult["rhum"];

                            //Wind
                            result.Wind = (int)jresult["winds"]; // m/s

                            //Wind direction
                            result.WindDirection = (int)jresult["windd"];

                            //Pressure hPa
                            result.Pressure = (float)jresult["pres"];

                            //UV index
                            result.UVIndex = (int)jresult["uvi"];

                            //Rain precipitation
                            result.RainPrecipitation = (float)jresult["rain"];

                            //Forecast
                            wr.Url = string.Format(_URL_DAILY, loc.LocationID);
                            j = MediaPortal.Pbk.Net.Http.Caching.Instance.DownloadFile<JToken>(null, wr: wr, iLifeTime: iRefreshInterval / 60);
                            if (j != null)
                            {
                                result.ForecastDays = new List<ForecastDay>();

                                foreach (JToken jDay in j["data"])
                                {
                                    ForecastDay day = new ForecastDay();

                                    //Temperature MIN
                                    day.TemperatureMin = (int)jDay["tmin"];

                                    //Temperature MAX
                                    day.TemperatureMax = (int)jDay["tmax"];

                                    //Image
                                    wCode = parseConditionSymbol((string)jDay["symb"]);
                                    day.ConditionIconCode = getIconCode(wCode, out transCode);

                                    //Translation
                                    day.ConditionTranslationCode = transCode;

                                    //Date
                                    day.Date = (DateTime)jDay["date"];


                                    result.ForecastDays.Add(day);
                                }
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
                yield break; ;

            string strUrl = string.Format(_URL_SEARCH, System.Web.HttpUtility.UrlEncode(strQuery));
            JToken j = MediaPortal.Pbk.Net.Http.HttpUserWebRequest.Download<JToken>(strUrl, iResponseTimout: 30000);
            if (j != null)
            {
                Database.dbWeatherLoaction location;
                foreach (JToken jItem in j["results"])
                {
                    location = new Database.dbWeatherLoaction()
                    {
                        LocationID = jItem["id"].ToString(),
                        ObservationLocation = jItem["countryName"].ToString(),
                        Name = jItem["name"].ToString(),
                        Country = jItem["defaultCountryName"].ToString(),
                        Longitude = (double)jItem["lon"],
                        Latitude = (double)jItem["lat"],
                        Provider = ProviderTypeEnum.FORECA
                    };

                    yield return location;
                }
            }
        }


        private static uint parseConditionSymbol(string strSymbol)
        {
            bool bNight = strSymbol[0] == 'n';
            uint i = uint.Parse(strSymbol.Substring(1, 3));
            return bNight ? (i | 0x80000000) : i;
        }

        private int getIconCode(uint wCode, out Language.TranslationEnum translCode)
        {
            #region Info
            //DayTornado = 0
            //DayTropicalStorm = 1
            //DayHurricane = 2
            //DaySevereThunderstorms = 3
            //DayThunderstorms = 4
            //DayMixedRainAndSnow = 5
            //DayMixedRainAndSleet = 6
            //DayMixedSnowAndSleet = 7
            //DayFreezingDrizzle = 8
            //DayDrizzle = 9
            //DayFreezingRain = 10
            //DayShowers = 11   
            //DayShowers2 = 12
            //DaySnowFlurries = 13
            //DayLightSnowShowers = 14
            //DayBlowingSnow = 15
            //DaySnow = 16
            //DayHail = 17
            //DaySleet = 18
            //DayDust = 19
            //DayFog = 20
            //DayHaze = 21
            //DaySmoke = 22
            //DayBlustery = 23
            //DayWindy = 24
            //DayCold = 25
            //DayCloudy = 26
            //NightMostlyCloudy = 27
            //DayMostlyCloudy = 28
            //NightPartlyCloudy = 29
            //DayPartlyCloudy = 30
            //NightClear = 31
            //DaySunny = 32
            //NightFair = 33
            //DayFair = 34
            //DayMixedRainAndHail = 35
            //DayHot = 36
            //DayIsolatedThunderstorms = 37
            //DayScatteredThunderstorms = 38
            //DayScatteredThunderstorms2 = 39
            //DayHeavyRain = 40
            //DayScatteredSnowShowers = 41
            //DayScatteredSnowShowers2 = 42
            //DayHeavySnow = 43
            //DayPartlyCloudy2 = 44
            //NightThunderShowers = 45
            //NightSnowShowers = 46
            //NightIsolatedThundershowers = 47

            //  "100":"Mostly clear",
            //  "110":"Mostly clear, slight possibility of rain",
            //  "111":"Mostly clear, slight possibility of wet snow",
            //  "112":"Mostly clear, slight possibility of snow",
            //  "120":"Mostly clear, slight possibility of rain",
            //  "121":"Mostly clear, slight possibility of wet snow",
            //  "122":"Mostly clear, slight possibility of snow",
            //  "130":"Mostly clear, slight possibility of rain",
            //  "131":"Mostly clear, slight possibility of wet snow",
            //  "132":"Mostly clear, slight possibility of snow",
            //  "140":"Mostly clear, possible thunderstorms with rain",
            //  "141":"Mostly clear, possible thunderstorms with wet snow",
            //  "142":"Mostly clear, possible thunderstorms with snow",
            //  "200":"Partly cloudy",
            //  "210":"Partly cloudy and light rain",
            //  "211":"Partly cloudy and light wet snow",
            //  "212":"Partly cloudy and light snow",
            //  "220":"Partly cloudy and showers",
            //  "221":"Partly cloudy and wet snow showers",
            //  "222":"Partly cloudy and snow showers",
            //  "230":"Partly cloudy and rain",
            //  "231":"Partly cloudy and wet snow\n",
            //  "232":"Partly cloudy and snow",
            //  "240":"Partly cloudy, possible thunderstorms with rain",
            //  "241":"Partly cloudy, possible thunderstorms with wet snow",
            //  "242":"Partly cloudy, possible thunderstorms with snow",
            //  "300":"Cloudy",
            //  "310":"Cloudy and light rain",
            //  "311":"Cloudy and light wet snow",
            //  "312":"Cloudy and light snow",
            //  "320":"Cloudy and showers",
            //  "321":"Cloudy and wet snow showers",
            //  "322":"Cloudy and snow showers",
            //  "330":"Cloudy and rain",
            //  "331":"Cloudy and wet snow",
            //  "332":"Cloudy and snow",
            //  "340":"Cloudy, thunderstorms with rain",
            //  "341":"Cloudy, thunderstorms with wet snow",
            //  "342":"Cloudy, thunderstorms with snow",
            //  "400":"Overcast",
            //  "410":"Overcast and light rain",
            //  "411":"Overcast and light wet snow",
            //  "412":"Overcast and light snow",
            //  "420":"Overcast and showers",
            //  "421":"Overcast and wet snow showers",
            //  "422":"Overcast and snow showers",
            //  "430":"Overcast and rain",
            //  "431":"Overcast and wet snow",
            //  "432":"Overcast and snow",
            //  "440":"Overcast, thunderstorms with rain",
            //  "441":"Overcast, thunderstorms with wet snow",
            //  "442":"Overcast, thunderstorms with snow",
            //  "500":"Thin upper cloud",
            //  "600":"Fog",
            //  "000":"Clear"}}


            //x00: Clouds
            //0: Clear/Jasno
            //1: Moustly Clear/Skorojasno
            //2: Partly Cloudy/Polojasno
            //3: Cloudy/Oblačno
            //4: Overcast/Zataženo
            //5: ?? Slaba Mlha
            //6: ?? Mlha

            //0x0: Level
            //0: None/Žádný
            //1: Light/Slabý
            //2: Showers/Přeháňky
            //3: Rain,Snow/Déšť,Sněžení
            //4: Storm/Bouřky

            //00x: Type
            //0: Rain/Déšť
            //1: Wet Snow/Déšť se sněhem
            //2: Snow/Sněžení
            #endregion

            bool bNight = false;
            if ((wCode & 0x80000000) != 0)
            {
                bNight = true;
                wCode &= ~0x80000000;
            }

            switch (wCode)
            {
                case 000: //000:Clear //000 Jasno
                    translCode = Language.TranslationEnum.labelConditionClear;
                    return bNight ? 31 : 32;

                case 100: //100:Mostly clear //100 Skorojasno
                    translCode = Language.TranslationEnum.labelConditionMostlySunny;
                    return bNight ? 29 : 30;

                case 200: //200:Partly cloudy //200 Polojasno
                    translCode = Language.TranslationEnum.labelConditionPartlyCloudy;
                    return bNight ? 27 : 28;

                case 110: //110:Mostly clear, slight possibility of rain, //210 Polojasno, slabý déšť
                case 120: //120:Mostly clear, slight possibility of rain,
                case 130: //130:Mostly clear, slight possibility of rain,
                case 210: //210:Partly cloudy and light rain,
                    translCode = Language.TranslationEnum.labelConditionLightRain;
                    //return bNight ? 45 : 39;
                    return 11;

                case 111: //111:Mostly clear, slight possibility of wet snow
                case 121: //121:Mostly clear, slight possibility of wet snow,
                case 131: //131:Mostly clear, slight possibility of wet snow,
                case 211: //211:Partly cloudy and light wet snow //strCondition = Slabý déšť se sněhem;
                    translCode = Language.TranslationEnum.labelConditionLightSleet;
                    return 41;

                case 112: //112:Mostly clear, slight possibility of snow,
                case 122: //122:Mostly clear, slight possibility of snow,
                case 132: //132:Mostly clear, slight possibility of snow,
                case 212: //211:Partly cloudy and light wet snow //212 Polojasno, slabé sněžení
                    //strCondition = Slabé sněžení;
                    translCode = Language.TranslationEnum.labelConditionLightSnow;
                    return bNight ? 46 : 16;

                case 220: //220:Partly cloudy and showers, //220 Polojasno, přeháňky
                case 230: //230:Partly cloudy and rain,
                    //strCondition = Přeháňky;
                    translCode = Language.TranslationEnum.labelConditionScatteredShowers;
                    //return bNight ? 45 : 39;
                    return 11;

                case 221: //221:Partly cloudy and wet snow showers, //221 Polojasno, přeháňky, déšť se sněhem
                case 231: //231:Partly cloudy and wet snow\n,
                    //strCondition = Přeháňky se sněhem;
                    translCode = Language.TranslationEnum.labelConditionMixedRainAndSnow;
                    return 41;

                case 222: //222:Partly cloudy and snow showers, ///222 Polojasno, sněhové přeháňky
                case 232: //232:Partly cloudy and snow,
                    //strCondition = Sněhové přeháňky;
                    translCode = Language.TranslationEnum.labelConditionSnowFlurries;
                    return 41;

                case 240: //240:Partly cloudy, possible thunderstorms with rain, //240 Polojasno, možnost bouřek s deštěm
                    translCode = Language.TranslationEnum.labelConditionChanceOfStorm;
                    return bNight ? 47 : 37;


                case 300: //300:Cloudy //300 Oblačno
                    translCode = Language.TranslationEnum.labelConditionCloudy;
                    return bNight ? 27 : 28;

                case 310: //310:Cloudy and light rain, //310 Oblačno, slabý déšť
                case 320: //320:Cloudy and showers, //320 Oblačno, přeháňky
                    //strCondition = Přeháňky;
                    translCode = Language.TranslationEnum.labelConditionScatteredShowers;
                    //return bNight ? 45 : 39;
                    return 11;

                case 311: //311:Cloudy and light wet snow, //Oblačno, slabý déšť se sněhem
                    //strCondition = Slabý déšť se sněhem;
                    translCode = Language.TranslationEnum.labelConditionLightSleet;
                    return 6;

                case 312: //312:Cloudy and light snow, //Oblačno, slabé sněžení
                    //strCondition = Slabé sněžení;
                    translCode = Language.TranslationEnum.labelConditionLightSnow;
                    return 41;

                case 321: //321:Cloudy and wet snow showers, //321 Oblačno, přeháňky, déšť se sněhem
                    //strCondition = Přeháňky se sněhem;
                    translCode = Language.TranslationEnum.labelConditionSnowFlurries;
                    return 6;

                case 322: //322:Cloudy and snow showers, //322 Oblačno, sněhové přeháňky
                    //strCondition = Sněhové přeháňky;
                    translCode = Language.TranslationEnum.labelConditionSnowFlurries;
                    return 41;

                case 140: //140:Mostly clear, possible thunderstorms with rain,
                case 340: //340:Cloudy, thunderstorms with rain, //340 Oblačno, bouřky, déšť
                    translCode = Language.TranslationEnum.labelConditionThunderShower;
                    return 37;

                case 141: //141:Mostly clear, possible thunderstorms with wet snow,
                case 241: //241:Partly cloudy, possible thunderstorms with wet snow,
                case 341: //341:Cloudy, thunderstorms with wet snow,
                    translCode = Language.TranslationEnum.labelConditionChanceOfMixedRainAndSnow;
                    return 38;

                case 142: //142:Mostly clear, possible thunderstorms with snow,
                case 242: //242:Partly cloudy, possible thunderstorms with snow,
                case 342: //342:Cloudy, thunderstorms with snow,
                    translCode = Language.TranslationEnum.labelConditionMixedSnowAndThunderstorms;
                    return 38;

                case 400: //400:Overcast, //400 Zataženo
                    translCode = Language.TranslationEnum.labelConditionOvercast;
                    return 26;

                case 410: //410:Overcast and light rain, //410 Zataženo, slabý déšť
                    //strCondition = Slabý déšť;
                    translCode = Language.TranslationEnum.labelConditionLightRain;
                    return 11;

                case 411: //411:Overcast and light wet snow, //411 Zataženo, slabý déšť se sněhem
                    //strCondition = Déšť se sněhem;
                    translCode = Language.TranslationEnum.labelConditionLightSleet;
                    return 6;

                case 412: //412:Overcast and light snow, //412 Zataženo, slabé sněžení
                    //strCondition = Slabé sněžení;
                    translCode = Language.TranslationEnum.labelConditionLightSnow;
                    return 41;

                case 420: //420:Overcast and showers, //420 Zataženo, přeháňky
                    //strCondition = Přeháňky;
                    translCode = Language.TranslationEnum.labelConditionFlurries;
                    return 11;

                case 421: //421:Overcast and wet snow showers, //421 Zataženo, přeháňky, déšť se sněhem
                    //strCondition = Přeháňky se sněhem;
                    translCode = Language.TranslationEnum.labelConditionSnowFlurries;
                    return 6;

                case 422: //422:Overcast and snow showers, //422 Zataženo, sněhové přeháňky
                    //strCondition = Sněhové přeháňky;
                    translCode = Language.TranslationEnum.labelConditionSnowFlurries;
                    return 14;

                case 330: //330:Cloudy and rain,
                case 430: //430:Overcast and rain, //430 Zataženo, déšť
                    //strCondition = Déšť;
                    translCode = Language.TranslationEnum.labelConditionRain;
                    return 12;

                case 331: //331:Cloudy and wet snow,
                case 431: //431:Overcast and wet snow, //431 Zataženo, déšť se sněhem
                    //strCondition = Déšť se sněhem;
                    translCode = Language.TranslationEnum.labelConditionMixedRainAndSnow;
                    return 6;

                case 332: //332:Cloudy and snow,
                case 432: //432:Overcast and snow, //432 Zataženo, sněžení
                    //strCondition = Sněžení;
                    translCode = Language.TranslationEnum.labelConditionSnow;
                    return 16;

                case 440: //440:Overcast, thunderstorms with rain, //440 Zataženo, bouřky, déšť
                    translCode = Language.TranslationEnum.labelConditionStorm;
                    return 38;

                case 441: //441:Overcast, thunderstorms with wet snow,
                case 442: //442:Overcast, thunderstorms with snow,
                    translCode = Language.TranslationEnum.labelConditionMixedSnowAndThunderstorms;
                    return 0;

                case 500: //500:Thin upper cloud, //500 ??? castecne mlhy
                    translCode = Language.TranslationEnum.labelConditionFog;
                    return bNight ? 19 : 20;

                case 600: //600:Fog, //600 Mlha
                    translCode = Language.TranslationEnum.labelConditionFog;
                    return bNight ? 19 : 20;

                default:
                    translCode = Language.TranslationEnum.unknown;
                    return -1;
            }
        }
    }
}
