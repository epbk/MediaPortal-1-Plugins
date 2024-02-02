using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaPortal.Plugins.WorldWeatherLite.Providers
{
    public abstract class ProviderBase
    {
        public abstract ProviderTypeEnum Type { get; }

        public abstract string Name { get; }

        /// <summary>
        /// Standart MSN translation
        /// </summary>
        /// <param name="iCode">MSN weather code</param>
        /// <returns></returns>
        public static Language.TranslationEnum GetTranslationCode(int iCode)
        {
            switch (iCode)
            {
                case 32:
                    return Language.TranslationEnum.labelConditionClearSunny;

                case 31:
                    return Language.TranslationEnum.labelConditionClear;

                case 27:
                case 28:
                case 29:
                case 30:
                case 33:
                case 34:
                    return Language.TranslationEnum.labelConditionPartlyCloudy;

                case 26:
                case 44:
                    return Language.TranslationEnum.labelConditionCloudy;

                case 19:
                    return Language.TranslationEnum.labelConditionDust;

                case 20:
                    return Language.TranslationEnum.labelConditionFog;

                case 21:
                    return Language.TranslationEnum.labelConditionHaze;

                case 22:
                    return Language.TranslationEnum.labelConditionSmoke;

                case 23:
                case 24:
                    return Language.TranslationEnum.labelConditionWindy;

                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 17:
                case 35:
                    return Language.TranslationEnum.labelConditionThunderStorm;

                case 37:
                case 38:
                case 47:
                    return Language.TranslationEnum.labelConditionScatteredThunderStorm;

                case 15:
                    return Language.TranslationEnum.labelConditionBlizzard;

                case 9:
                case 11:
                    return Language.TranslationEnum.labelConditionLightRain;

                case 12:
                    return Language.TranslationEnum.labelConditionRain;

                case 18:
                case 40:
                    return Language.TranslationEnum.labelConditionShowers;

                case 39:
                case 45:
                    return Language.TranslationEnum.labelConditionScatteredShowers;

                case 41:
                case 46:
                    return Language.TranslationEnum.labelConditionScatteredSnowShowers;

                case 5:
                case 7:
                    return Language.TranslationEnum.labelConditionMixedRainAndSnow;

                case 10:
                    return Language.TranslationEnum.labelConditionMixedRainAndSleet;

                case 6:
                    return Language.TranslationEnum.labelConditionMixedSnowAndSleet;

                case 8:
                    return Language.TranslationEnum.labelConditionIce;

                case 13:
                    return Language.TranslationEnum.labelConditionLightSnow;

                case 14:
                case 16:
                case 42:
                case 43:
                    return Language.TranslationEnum.labelConditionSnow;

                case 25:
                    return Language.TranslationEnum.labelConditionCold;

                case 36:
                    return Language.TranslationEnum.labelConditionHot;

                default:
                    return Language.TranslationEnum.unknown;
            }
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
