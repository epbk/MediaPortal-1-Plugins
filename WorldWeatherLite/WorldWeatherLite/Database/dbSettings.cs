using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("settings")]
    public class dbSettings : dbTable
    {
        public const int DATABASE_VERSION_CURRENT = 2;

        [DBFieldAttribute(FieldName = "dbVersion", Default = "1")]
        public int DatabaseVersion
        { get; set; }

        [DBFieldAttribute(FieldName = "fullscreenBehavior", Default = "Sleep")]
        public FullscreenVideoBehaviorEnum FullscreenVideoBehavior
        { get; set; }

        [DBFieldAttribute(FieldName = "url", Default = "")]
        public string Url
        { get; set; }

        [DBFieldAttribute(FieldName = "imageViewMode", Default = "Coverflow")]
        public ImageViewModeEnum ImageViewMode
        { get; set; }
        
        [DBFieldAttribute(FieldName = "guiTemperatureUnit", Default = "Celsius")]
        public GUI.GUITemperatureUnitEnum GUITemperatureUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiPressureUnit", Default = "Millibar")]
        public GUI.GUIPressureUnitEnum GUIPressureUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiDistanceUnit", Default = "Kilometer")]
        public GUI.GUIDistanceUnitEnum GUIDistanceUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiWindUnit", Default = "KilometersPerHour")]
        public GUI.GUIWindUnitEnum GUIWindUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiPrecipUnit", Default = "Millimeter")]
        public GUI.GUIPrecipitationUnitEnum GUIPrecipitationUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiCalendarEnable", Default = "True")]
        public bool GUICalendarEnable
        { get; set; }

        [DBFieldAttribute(FieldName = "profileID", Default = "0")]
        public int ProfileID
        { get; set; }

        public static dbSettings Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = (dbSettings)Manager.Get(typeof(dbSettings), 1);

                    if (_Instance == null)
                    {
                        _Instance = new dbSettings();
                        _Instance.Commit();
                    }

                    
                }

                return _Instance;

            }
        }private static dbSettings _Instance = null;
    }
}
