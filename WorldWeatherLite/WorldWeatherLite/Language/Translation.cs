using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Xml;
using MediaPortal.Configuration;
using MediaPortal.GUI.Library;
using MediaPortal.Localisation;
using MediaPortal.Profile;

namespace MediaPortal.Plugins.WorldWeatherLite.Language
{
    public class Translation
    {
        private static Dictionary<string, int> _Languages = null;

        private static Dictionary<int, string> _LanguageEn = new Dictionary<int,string>();


        public static LanguageEnum GetLanguage()
        {
            using (Settings set = new Settings(Config.GetFile(Config.Dir.Config, "mediaportal.xml")))
            {
                try
                {
                    string strKey;
                    if ((strKey = set.GetValueAsString("gui", "language", "English").ToLower()) != null)
                    {
                        if (_Languages == null)
                        {
                            _Languages = new Dictionary<string, int>(18)
				        {
					        {
						        "english",
						        0
					        },
					        {
						        "english (united states)",
						        1
					        },
					        {
						        "english (united kingdom)",
						        2
					        },
					        {
						        "german",
						        3
					        },
					        {
						        "dutch",
						        4
					        },
					        {
						        "français",
						        5
					        },
					        {
						        "italian",
						        6
					        },
					        {
						        "magyar",
						        7
					        },
					        {
						        "spanish",
						        8
					        },
					        {
						        "swedish",
						        9
					        },
					        {
						        "russian",
						        10
					        },
					        {
						        "polish",
						        11
					        },
					        {
						        "bulgarian",
						        12
					        },
					        {
						        "romanian",
						        13
					        },
					        {
						        "czech",
						        14
					        },
					        {
						        "portuguese",
						        15
					        },
					        {
						        "korean",
						        16
					        },
					        {
						        "danish",
						        17
					        }
				        };
                        }
                        int iValue;
                        if (_Languages.TryGetValue(strKey, out iValue))
                        {
                            switch (iValue)
                            {
                                case 0:
                                    return LanguageEnum.English;
                                case 1:
                                    return LanguageEnum.EnglishUS;
                                case 2:
                                    return LanguageEnum.EnglishUK;
                                case 3:
                                    return LanguageEnum.German;
                                case 4:
                                    return LanguageEnum.Dutch;
                                case 5:
                                    return LanguageEnum.Français;
                                case 6:
                                    return LanguageEnum.Italiano;
                                case 7:
                                    return LanguageEnum.Magyar;
                                case 8:
                                    return LanguageEnum.Spanish;
                                case 9:
                                    return LanguageEnum.Swedish;
                                case 10:
                                    return LanguageEnum.Russian;
                                case 11:
                                    return LanguageEnum.Polish;
                                case 12:
                                    return LanguageEnum.Bulgarian;
                                case 13:
                                    return LanguageEnum.Romanian;
                                case 14:
                                    return LanguageEnum.Czech;
                                case 15:
                                    return LanguageEnum.Portuguese;
                                case 16:
                                    return LanguageEnum.Korean;
                                case 17:
                                    return LanguageEnum.Danish;
                            }
                        }
                    }
                    return LanguageEnum.English;
                }
                finally
                {
                }
            }
        }

        public static string GetLanguageCode(LanguageEnum strLanguage)
        {
            switch (strLanguage)
            {
                default:
                    return "en";
                case LanguageEnum.German:
                    return "de";
                case LanguageEnum.Dutch:
                    return "da";
                case LanguageEnum.Français:
                    return "fr";
                case LanguageEnum.Italiano:
                    return "it";
                case LanguageEnum.Magyar:
                    return "hu";
                case LanguageEnum.Spanish:
                    return "es";
                case LanguageEnum.Swedish:
                    return "sv";
                case LanguageEnum.Russian:
                    return "ru";
                case LanguageEnum.Polish:
                    return "pl";
                case LanguageEnum.Bulgarian:
                    return "bg";
                case LanguageEnum.Romanian:
                    return "ro";
                case LanguageEnum.Czech:
                    return "cs";
                case LanguageEnum.Portuguese:
                    return "pt";
                case LanguageEnum.Korean:
                    return "ko";
                case LanguageEnum.Danish:
                    return "da";
            }
        }

        public static string GetLanguageString(LocalisationProvider language, int iLanguageCode)
        {
            string strLanguageDefault;
            if (!_LanguageEn.TryGetValue(iLanguageCode, out strLanguageDefault))
                strLanguageDefault = "Unknown";

            return GetLanguageString(language, iLanguageCode, strLanguageDefault, null, null, null);
        }

        public static string GetLanguageString(LocalisationProvider language, int iLanguageCode, string strLanguageDefault)
        {
            return GetLanguageString(language, iLanguageCode, strLanguageDefault, null, null, null);
        }

        public static string GetLanguageString(LocalisationProvider language, int iLanguageCode, string strLanguageDefault, string strLanguageParam1)
        {
            return GetLanguageString(language, iLanguageCode, strLanguageDefault, strLanguageParam1, null, null);
        }

        public static string GetLanguageString(LocalisationProvider language, int iLanguageCode, string strLanguageDefault, string strLanguageParam1, string strLanguageParam2)
        {
            return GetLanguageString(language, iLanguageCode, strLanguageDefault, strLanguageParam1, strLanguageParam2, null);
        }

        public static string GetLanguageString(LocalisationProvider language, int iLanguageCode, string strLanguageDefault, string strLanguageParam1, string strLanguageParam2, string strLanguageParam3)
        {
            if (language != null)
            {
                string strText;
                try
                {
                    strText = language.GetString("unmapped", iLanguageCode);
                }
                catch
                {
                    strText = string.Empty;
                }

                if (string.IsNullOrEmpty(strText))
                    strText = strLanguageDefault;

                if (!string.IsNullOrEmpty(strLanguageParam1))
                    strText = strText.Replace("%1", strLanguageParam1);

                if (!string.IsNullOrEmpty(strLanguageParam2))
                    strText = strText.Replace("%2", strLanguageParam2);

                if (!string.IsNullOrEmpty(strLanguageParam3))
                    strText = strText.Replace("%3", strLanguageParam3);

                return strText;
            }
            else
                return strLanguageDefault;
        }

        public static LocalisationProvider GetLocalisationProvider(string strDirectoryName)
		{
            //Load default english language
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MediaPortal.Plugins.WorldWeatherLite.Language.strings_en.xml"))
                {
                    XmlDocument xml = new XmlDocument();
                    xml.Load(stream);

                    foreach(XmlNode node in xml.SelectNodes("//Language/Section/String"))
                        _LanguageEn.Add(int.Parse(node.Attributes["id"].Value),node.InnerText);
                }

            }
            catch
            {
                return null;
            }

			string strLanguageFileEn = Config.GetSubFolder(Config.Dir.Language, string.Format("{0}\\{1}", strDirectoryName, "strings_en.xml"));
			string strLanguageDirectory = Config.GetSubFolder(Config.Dir.Language, strDirectoryName);
			if (System.IO.File.Exists(strLanguageFileEn))
			{
                using (Settings set = new Settings(Config.GetFile(Config.Dir.Config, "mediaportal.xml")))
                {
                    string strGuiLng = set.GetValueAsString("gui", "language", "English");
                    string strCultureName = strGuiLng != null ? GUILocalizeStrings.GetCultureName(strGuiLng) : GUILocalizeStrings.CurrentLanguage();
                    LocalisationProvider result = new LocalisationProvider(strLanguageDirectory, strCultureName);
                    if (!GUILocalizeStrings.Load(strGuiLng))
                        result = null;

                    return result;
                }
			}
			return null;
		}
    }
}
