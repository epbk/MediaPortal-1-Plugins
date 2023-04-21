using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Xml;
using System.Windows.Forms;
using System.ComponentModel;
using NLog;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.Threading;
using System.Reflection;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using System.Collections.ObjectModel;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    public abstract class SettingsManager : Dictionary<string, DBSetting>
    {
        public delegate void SettingChangedDelegate(DBSetting setting, object oldValue);


        #region Private Variables
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        private DatabaseManager _DbManager;
        private Dictionary<string, PropertyInfo> _PropertyLookup;
        private Dictionary<PropertyInfo, CornerstoneSettingAttribute> _AttributeLookup;

        private bool _Initializing;

        #endregion

        public ReadOnlyCollection<DBSetting> AllSettings
        {
            get
            {
                return this._AllSettings.AsReadOnly();
            }
        } protected List<DBSetting> _AllSettings;

        /// <summary>
        /// Fires every time a settings value has been changed.
        /// </summary>
        public event SettingChangedDelegate SettingChanged;

        #region ctor
        static SettingsManager()
        {
            Logging.Log.Init();
        }

        public SettingsManager(DatabaseManager dbManager)
        {
            this._DbManager = dbManager;

            this._Initializing = true;

            this.buildPropertyLookup();
            this.loadSettingsFromDatabase();
            this.updateAndSyncSettings();

            this._Initializing = false;

            _Logger.Info("SettingsManager Created");
        }
        #endregion


        /// <summary>
        /// This method should be called by the super class when a setting has been changed.
        /// </summary>
        /// <param name="strSettingIdentifier">
        /// The identifier as defined in the attribute for the setting property in the 
        /// super class.
        /// </param>
        public void OnSettingChanged(string strSettingIdentifier)
        {
            // if we are intializing, ignore all changes
            if (this._Initializing)
                return;

            // make sure we have been passed a valid identifier
            if (!this._PropertyLookup.ContainsKey(strSettingIdentifier))
            {
                _Logger.Error("[OnSettingChanged] Invalid call to OnSettingChanged with \"" + strSettingIdentifier + "\" identifier!");
                return;
            }

            // grab property and setting info
            PropertyInfo property = this._PropertyLookup[strSettingIdentifier];
            DBSetting setting = this[strSettingIdentifier];
            object oldValue;

            // if we are already in the process of updating things just return
            if (setting.ManagerModifyingValue)
                return;

            setting.ManagerModifyingValue = true;

            if (setting.UpdatingFromObject)
            {
                // update the property in the settings manager to reflect the change in the object
                oldValue = property.GetGetMethod().Invoke(this, null);
                property.GetSetMethod().Invoke(this, new object[] { setting.Value });
                setting.Commit();
            }
            else
            {
                // update the actual setting object and commit
                oldValue = setting.Value;
                setting.Value = property.GetGetMethod().Invoke(this, null);
                setting.Commit();
            }

            setting.ManagerModifyingValue = false;

            // notify any listeners of the value change
            if (this.SettingChanged != null)
                this.SettingChanged(setting, oldValue);
        }

        protected void Sync(SettingsManager otherSettings)
        {
            this._AllSettings.AddRange(otherSettings.AllSettings);

            foreach (DBSetting currSetting in otherSettings.Values)
            {
                if (!this.ContainsKey(currSetting.Key))
                    this.Add(currSetting.Key, currSetting);
            }
        }


        /// <summary>
        /// Stores property and attribute info from the super class for quick lookup later.
        /// </summary>
        private void buildPropertyLookup()
        {
            this._PropertyLookup = new Dictionary<string, PropertyInfo>();
            this._AttributeLookup = new Dictionary<PropertyInfo, CornerstoneSettingAttribute>();

            foreach (PropertyInfo currProperty in GetType().GetProperties())
            {
                // make sure this property is intended to be a setting
                object[] attributes = currProperty.GetCustomAttributes(typeof(CornerstoneSettingAttribute), true);
                if (attributes.Length == 0)
                    continue;

                // and store it's info for quick access later
                CornerstoneSettingAttribute attribute = attributes[0] as CornerstoneSettingAttribute;
                this._PropertyLookup[attribute.Identifier] = currProperty;
                this._AttributeLookup[currProperty] = attribute;
            }
        }

        /// <summary>
        /// Loads all existing settings from the database.
        /// </summary>
        private void loadSettingsFromDatabase()
        {
            List<DBSetting> settingList = this._DbManager.Get<DBSetting>(null);
            foreach (DBSetting currSetting in settingList)
            {
                try
                {
                    this.Add(currSetting.Key, currSetting);
                    currSetting.SettingsManager = this;
                }
                catch (Exception e)
                {
                    if (e is ThreadAbortException)
                        throw e;

                    _Logger.Error("[loadSettingsFromDatabase] Error loading setting " + currSetting.Name + " (key = " + currSetting.Key + ")");
                }
            }
        }

        /// <summary>
        /// Populates properties in super class from data in the database, and adds new properties 
        /// defined in the super class to the database.
        /// </summary>
        private void updateAndSyncSettings()
        {
            this._AllSettings = new List<DBSetting>();

            foreach (PropertyInfo currProperty in this._PropertyLookup.Values)
            {
                try
                {
                    this.syncSetting(currProperty);

                    CornerstoneSettingAttribute attribute = this._AttributeLookup[currProperty];
                    DBSetting setting = this[attribute.Identifier];

                    currProperty.GetSetMethod().Invoke(this, new Object[] { setting.Value });

                    this._AllSettings.Add(setting);
                }
                catch (Exception e)
                {
                    if (e is ThreadAbortException)
                        throw e;

                    _Logger.ErrorException("[updateAndSyncSettings] Failed loading setting data for " + currProperty.Name + ".", e);
                }
            }
        }

        private void syncSetting(PropertyInfo property)
        {
            CornerstoneSettingAttribute attribute = this._AttributeLookup[property];
            StringList groups = new StringList(attribute.Groups);

            if (!this.ContainsKey(attribute.Identifier))
            {
                DBSetting newSetting = new DBSetting();
                newSetting.Key = attribute.Identifier;
                newSetting.Name = attribute.Name;
                newSetting.Value = attribute.Default;
                newSetting.Type = DBSetting.TypeLookup(property.PropertyType);
                newSetting.Description = attribute.Description;
                newSetting.MoreInfoLink = attribute.MoreInfoLink;
                newSetting.Hidden = attribute.Hidden;
                newSetting.Sensitive = attribute.Sensitive;
                newSetting.Grouping.AddRange(groups);
                newSetting.DBManager = this._DbManager;
                newSetting.SettingsManager = this;
                newSetting.Commit();

                this[attribute.Identifier] = newSetting;
            }
            else
            {
                DBSetting existingSetting = this[attribute.Identifier];

                // update name if necessary 
                if (!existingSetting.Name.Equals(attribute.Name))
                    existingSetting.Name = attribute.Name;

                // update description if necessary
                if (!existingSetting.Description.Equals(attribute.Description))
                    existingSetting.Description = attribute.Description;

                // update link
                existingSetting.MoreInfoLink = attribute.MoreInfoLink;

                existingSetting.Hidden = attribute.Hidden;
                existingSetting.Sensitive = attribute.Sensitive;

                // update groups if necessary
                bool reloadGrouping = false;
                if (existingSetting.Grouping.Count != groups.Count)
                    reloadGrouping = true;
                else
                    for (int i = 0; i < existingSetting.Grouping.Count; i++)
                    {
                        if (i >= groups.Count || !existingSetting.Grouping[i].Equals(groups[i]))
                        {
                            reloadGrouping = true;
                            break;
                        }
                    }

                if (reloadGrouping)
                {
                    existingSetting.Grouping.Clear();
                    existingSetting.Grouping.AddRange(groups);
                }

                existingSetting.Commit();
            }
        }

        private void generate()
        {
            string strSettings = "\n\n";
            string strSettings2 = "\n\n";

            foreach (DBSetting currSetting in this._DbManager.Get<DBSetting>(null))
            {
                string strDef;

                if (currSetting.Type == "String")
                    strDef = "\"" + currSetting.Value + "\"";
                else
                    strDef = currSetting.Value.ToString();

                string strPropertyName = currSetting.Name;
                while (strPropertyName.Contains(" "))
                    strPropertyName = currSetting.Name.Replace(" ", "");

                string strPrivateName = strPropertyName.Substring(1);
                strPrivateName = "_" + char.ToLower(strPropertyName[0]) + strPrivateName;


                strSettings += "        [CornerstoneSetting(\n";
                strSettings += "            Name = \"" + currSetting.Name + "\",\n";
                strSettings += "            Description = \"" + currSetting.Description + "\",\n";
                strSettings += "            Groups = \"" + currSetting.Grouping.ToString() + "\",\n";
                strSettings += "            Identifier = \"" + currSetting.Key + "\",\n";
                strSettings += "            Default = \"" + strDef + "\")]\n";
                strSettings += "        public " + currSetting.Type.ToString().ToLower() + " " + strPropertyName + " {\n";
                strSettings += "            get { return " + strPrivateName + "; }\n";
                strSettings += "            set {\n";
                strSettings += "                " + strPrivateName + " = value;\n";
                strSettings += "                OnSettingChanged(\"" + currSetting.Key + "\");\n";
                strSettings += "            }\n";
                strSettings += "        }\n";
                strSettings += "        private " + currSetting.Type.ToString().ToLower() + " " + strPrivateName + ";\n\n\n";

                strSettings2 += currSetting.Grouping.ToString() + "\t" + currSetting.Name + " (" + currSetting.Key + ")\n";
            }
            _Logger.Info(strSettings);
            _Logger.Info(strSettings2);

        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class CornerstoneSettingAttribute : Attribute
    {
        public string Identifier { get; set; }
        public string Groups { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public object Default { get; set; }
        public bool Hidden { get; set; }
        public string MoreInfoLink { get; set; }

        /// <summary>
        /// Mark settings as Sensitive to prevent their values from being displayed in log files
        /// </summary>
        public bool Sensitive { get; set; }
    }
}
