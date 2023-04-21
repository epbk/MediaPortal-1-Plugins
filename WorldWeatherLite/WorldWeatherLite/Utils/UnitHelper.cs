using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Utils
{
    public class UnitHelper
    {
        public static string GetTemperatureStringFromCelsius(GUI.GUITemperatureUnitEnum destUnit, int iValueCelsia)
        {
            switch (destUnit)
            {
                case GUI.GUITemperatureUnitEnum.Celsius:
                    return iValueCelsia.ToString() + "°C";

                case GUI.GUITemperatureUnitEnum.Fahrenheit:
                    return ((int)((float)iValueCelsia * 1.8F + 32)).ToString() + "°F";

                case GUI.GUITemperatureUnitEnum.Kelvin: //273.15 == 0°C
                    return (iValueCelsia + 273).ToString() + "°K";

                case GUI.GUITemperatureUnitEnum.Newton: //1°C = 0.33°N
                    return ((int)((float)iValueCelsia * 0.33)).ToString("0.0") + "°N";

                case GUI.GUITemperatureUnitEnum.Rankine: //°R = °C * 9/5 + 491.67
                    return ((int)((float)iValueCelsia * 1.8F + 491.67F)).ToString("0") + "°R";

                default:
                    return "n/a";
            }
        }

        public static string GetWindStringFromMeterPerSecond(GUI.GUIWindUnitEnum destUnit, float fValueMeterPerSecond)
        {
            switch (destUnit)
            {
                case GUI.GUIWindUnitEnum.MetersPerSecond:
                    return fValueMeterPerSecond.ToString("0") + " m/s";

                case GUI.GUIWindUnitEnum.KilometersPerHour:
                    return (fValueMeterPerSecond * 3.6F).ToString("0") + " km/h";

                case GUI.GUIWindUnitEnum.MilesPerHour:
                    return (fValueMeterPerSecond * 2.237F).ToString("0") + " mph";

                case GUI.GUIWindUnitEnum.Knote:
                    return (fValueMeterPerSecond * 1.944F).ToString("0") + " kts";

                case GUI.GUIWindUnitEnum.Beaufort:
                    float ms = Math.Abs(fValueMeterPerSecond);
                    if (ms <= 0.2)
                        return "0 bft";
                    else if (ms <= 1.5)
                        return "1 bft";
                    else if (ms <= 3.3)
                        return "2 bft";
                    else if (ms <= 5.4)
                        return "3 bft";
                    else if (ms <= 7.9)
                        return "4 bft";
                    else if (ms <= 10.7)
                        return "5 bft";
                    else if (ms <= 13.8)
                        return "6 bft";
                    else if (ms <= 17.1)
                        return "7 bft";
                    else if (ms <= 20.7)
                        return "8 bft";
                    else if (ms <= 24.4)
                        return "9 bft";
                    else if (ms <= 28.4)
                        return "10 bft";
                    else if (ms <= 32.6)
                        return "11 bft";
                    else 
                        return "12 bft";

                default:
                    return "n/a";
            }
        }

        public static string GetDistanceStringFromKiloMeters(GUI.GUIDistanceUnitEnum destUnit, double dValueKiloMeters)
        {
            switch (destUnit)
            {
                case GUI.GUIDistanceUnitEnum.Kilometer:
                    return dValueKiloMeters.ToString("0") + " km";

                case GUI.GUIDistanceUnitEnum.Miles:
                    return (dValueKiloMeters * 0.621371D).ToString("0") + " miles";

                default:
                    return "n/a";
            }
        }

        public static string GetPressureStringFromHPa(GUI.GUIPressureUnitEnum destUnit, float fValueHPa)
        {
            switch (destUnit)
            {
                case GUI.GUIPressureUnitEnum.Hectopascal:
                    return fValueHPa.ToString("0") + " hPa";

                case GUI.GUIPressureUnitEnum.Inch:
                    return (fValueHPa * 0.02953F).ToString("0.00") + " inHg"; //1 hPa = 0.02952998057228486 inHg

                case GUI.GUIPressureUnitEnum.Millibar: //One millibar is equal to one-thousandth of a bar
                    return fValueHPa.ToString("0") + " mbar";

                case GUI.GUIPressureUnitEnum.Torr: //1 hectopascals (hPa) is equal to 0.7500616827041697 torr (torr).
                    return (fValueHPa * 0.75F).ToString("0") + " torr";

                case GUI.GUIPressureUnitEnum.PoundsPerSquareInch: //1 hectopascals (hPa) is equal to 0.014503768077999999 pounds per square inch (psi
                    return (fValueHPa * 0.014503768F).ToString("0.00") + " psi";

                case GUI.GUIPressureUnitEnum.MillimetreOfMercury:
                    return (fValueHPa * 0.75F).ToString("0") + " mmHg"; //1 hPa = 0.75006375541921 mmHg

                default:
                    return "n/a";
            }
        }

        public static string GetPrecipitationStringFromMillimeter(GUI.GUIPrecipitationUnitEnum destUnit, float fValueMM)
        {
            switch (destUnit)
            {
                case GUI.GUIPrecipitationUnitEnum.Millimeter:
                    return fValueMM.ToString("0.00") + " mm";

                case GUI.GUIPrecipitationUnitEnum.Inch:
                    return (fValueMM * 0.03937F).ToString("0.00") + " in"; //1 millimetre is equal to 0.03937008 inches

                default:
                    return "n/a";
            }
        }
    }
}
