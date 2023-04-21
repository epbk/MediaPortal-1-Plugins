using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Xml;
using NLog;

namespace MediaPortal.Pbk.GUI
{
    public abstract class SkinSettings
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        protected Dictionary<string, string> _Defines;

        public SkinSettings(string strSkinFileName)
        {
            LoadDefinesFromSkin(strSkinFileName);
            populateProperties();
        }

        // Grabs the <define> tags from the skin for skin parameters from skinner.
        public void LoadDefinesFromSkin(string strSkinFileName)
        {
            try
            {
                // Load the XML file
                XmlDocument doc = new XmlDocument();
                _Logger.Info("[LoadDefinesFromSkin] Loading defines from skin.");
                doc.Load(strSkinFileName);

                // parse out the define tags and store them
                this._Defines = new Dictionary<string, string>();
                foreach (XmlNode node in doc.SelectNodes("/window/define"))
                {
                    string[] tokens = node.InnerText.Split(':');

                    if (tokens.Length < 2)
                        continue;

                    this._Defines[tokens[0]] = tokens[1];
                    _Logger.Debug("[LoadDefinesFromSkin] Loaded define from skin: " + tokens[0] + ": " + tokens[1]);
                }


            }
            catch (Exception e)
            {
                _Logger.ErrorException("[LoadDefinesFromSkin] Unexpected error loading <define> tags from skin file.", e);
            }
        }

        private void populateProperties()
        {
            // loop through our properties and for all defined as a skinsetting, try to load the value
            foreach (PropertyInfo currProperty in GetType().GetProperties())
            {
                try
                {
                    // try to grab the attribute for this proeprty, if it doesnt exist,
                    // this isnt a skin setting
                    object[] attributeList = currProperty.GetCustomAttributes(typeof(SkinSettingAttribute), true);
                    if (attributeList.Length == 0)
                        continue;

                    // grab the setting attribute and set use the default value for now
                    SkinSettingAttribute skinSettingAttr = (SkinSettingAttribute)attributeList[0];
                    object value = skinSettingAttr.Default;

                    // try to grab the skin defined value
                    if (this._Defines.ContainsKey(skinSettingAttr.SettingName))
                    {
                        string strStringValue = this._Defines[skinSettingAttr.SettingName];

                        // try parsing as a int
                        if (currProperty.PropertyType == typeof(int))
                        {
                            int iIntValue;
                            if (int.TryParse(strStringValue, out iIntValue))
                                value = iIntValue;
                            else
                                _Logger.Error("[populateProperties] \"" + strStringValue + "\" is an invalid value for " + skinSettingAttr.SettingName + " skin setting (expecting an int). Using default value.");
                        }

                        // try parsing as a float
                        else if (currProperty.PropertyType == typeof(float))
                        {
                            float fFloatValue;
                            if (float.TryParse(strStringValue, out fFloatValue))
                                value = fFloatValue;
                            else
                                _Logger.Error("[populateProperties] \"" + strStringValue + "\" is an invalid value for " + skinSettingAttr.SettingName + " skin setting (expecting a float). Using default value.");
                        }

                        // try parsing as a double
                        else if (currProperty.PropertyType == typeof(double))
                        {
                            double dDoubleValue;
                            if (double.TryParse(strStringValue, out dDoubleValue))
                                value = dDoubleValue;
                            else
                                _Logger.Error("[populateProperties] \"" + strStringValue + "\" is an invalid value for " + skinSettingAttr.SettingName + " skin setting (expecting a double). Using default value.");
                        }

                        // try parsing as a bool
                        else if (currProperty.PropertyType == typeof(bool))
                        {
                            bool bBoolValue;
                            if (bool.TryParse(strStringValue, out bBoolValue))
                                value = bBoolValue;
                            else
                                _Logger.Error("[populateProperties] \"" + strStringValue + "\" is an invalid value for " + skinSettingAttr.SettingName + " skin setting (expecting true or false). Using default value.");
                        }

                        // try parsing as a char
                        else if (currProperty.PropertyType == typeof(char))
                        {
                            if (strStringValue.Length > 0)
                                _Logger.Error("[populateProperties] \"" + strStringValue + "\" is an invalid value for " + skinSettingAttr.SettingName + " skin setting (expecting a single character). Using default value.");

                            value = strStringValue[0];
                        }

                        // try parsing as a string
                        else if (currProperty.PropertyType == typeof(string))
                        {
                            value = strStringValue;
                        }

                        // unsupported skin setting type
                        else
                        {
                            _Logger.Error("[populateProperties] " + currProperty.PropertyType.Name + " is not a supported SkinSetting type.");
                        }

                    }
                    else
                    {
                        _Logger.Debug("[populateProperties] " + skinSettingAttr.SettingName + " not defined in skin file, using default.");
                    }

                    // try to assign the value to the property
                    try
                    {
                        currProperty.SetValue(this, value, null);
                        _Logger.Info("[populateProperties] Assigned skin setting: " + currProperty.Name + " (" + skinSettingAttr.SettingName + ") = " + value.ToString());
                    }
                    catch (Exception)
                    {
                        _Logger.Error("[populateProperties] Failed to assign SkinSetting " + currProperty.Name + " (" + skinSettingAttr.SettingName + ")");
                    }

                }
                catch (Exception e)
                {
                    _Logger.ErrorException("[populateProperties] Unexpected error processing skin settings!", e);
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SkinSettingAttribute : Attribute
    {
        public string SettingName
        {
            get { return this._SettingName; }
            set { this._SettingName = value; }
        }private string _SettingName;

        public object Default
        {
            get { return this._Default; }
            set { this._Default = value; }
        }private object _Default;

        public SkinSettingAttribute(string strSettingName, object defaultValue)
        {
            this._SettingName = strSettingName;
            this._Default = defaultValue;
        }
    }
}
