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
        public const int DATABASE_VERSION_CURRENT = 3;
        private const string _DATABASE_VERSION_CURRENT_STRING = "3";

        [DBFieldAttribute(FieldName = "dbVersion", Default = _DATABASE_VERSION_CURRENT_STRING)]
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
        
        [Obsolete]
        [DBFieldAttribute(FieldName = "guiTemperatureUnit", Default = "Celsius")]
        public GUI.GUITemperatureUnitEnum GUITemperatureUnit
        { get; set; }

        [Obsolete]
        [DBFieldAttribute(FieldName = "guiPressureUnit", Default = "Millibar")]
        public GUI.GUIPressureUnitEnum GUIPressureUnit
        { get; set; }

        [Obsolete]
        [DBFieldAttribute(FieldName = "guiDistanceUnit", Default = "Kilometer")]
        public GUI.GUIDistanceUnitEnum GUIDistanceUnit
        { get; set; }

        [Obsolete]
        [DBFieldAttribute(FieldName = "guiWindUnit", Default = "KilometersPerHour")]
        public GUI.GUIWindUnitEnum GUIWindUnit
        { get; set; }

        [Obsolete]
        [DBFieldAttribute(FieldName = "guiPrecipUnit", Default = "Millimeter")]
        public GUI.GUIPrecipitationUnitEnum GUIPrecipitationUnit
        { get; set; }

        [Obsolete]
        [DBFieldAttribute(FieldName = "guiCalendarEnable", Default = "True")]
        public bool GUICalendarEnable
        { get; set; }

        [DBFieldAttribute(FieldName = "profileID", Default = "0")]
        public int ProfileID
        { get; set; }


        public void Upgrade()
        {
            int iVersion;
            while ((iVersion = this.DatabaseVersion) < DATABASE_VERSION_CURRENT)
            {
                switch (iVersion)
                {
                    case 1:
                    case 2:
                        //Assign all holidays to first profile
                        Manager.Get<dbHoliday>(null).ForEach(db =>
                            {
                                db.ParentID = 1;
                                db.CommitNeeded = true;
                                db.Commit();
                            });

                        //Assign all media images to first profile
                        Manager.Get<dbWeatherImage>(null).ForEach(db =>
                        {
                            db.ParentID = 1;
                            db.CommitNeeded = true;
                            db.Commit();
                        });

                        //move 'GUICalendarEnable' value to all profiles
                        Manager.Get<dbProfile>(null).ForEach(db =>
                        {
                            db.GUICalendarEnable = this.GUICalendarEnable;
                            db.CommitNeeded = true;
                            db.Commit();
                        });

                        //switch to v3
                        this.DatabaseVersion = 3;
                        this.CommitNeeded = true;
                        break;
                }
            }

            if (this.CommitNeeded)
                this.Commit();

            //Update old images
            Database.dbWeatherImage.Get(-1).ForEach(im =>
                {
                    if (im.Enable)
                    {
                        if (im.Url.IndexOf("https://api.sat24.com/animated/EU/visual/3/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            dbWeatherImage.InitDefaultSatellite(im);
                            im.CommitNeeded = true;
                            im.Commit();
                        }
                        else if (im.Url.IndexOf("https://api.sat24.com/animated/EU/infraPolair/3/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            dbWeatherImage.InitDefaultInfra(im);
                            im.CommitNeeded = true;
                            im.Commit();
                        }
                        else if (im.Url.IndexOf("https://api.sat24.com/animated/EU/rainTMC/3/", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            im.Enable = false;
                        }
                    }
                });
        }

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
