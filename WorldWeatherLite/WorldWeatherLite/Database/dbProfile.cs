using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("weatherLocation")]
    public class dbProfile : dbTable
    {
        [DBFieldAttribute(FieldName = "provider", Default = "FORECA")]
        public Providers.ProviderTypeEnum Provider
        { get; set; }

        [DBFieldAttribute(FieldName = "locationName", Default = "")]
        public string Name
        { get; set; }

        [DBFieldAttribute(FieldName = "locationCountry", Default = "")]
        public string Country
        { get; set; }

        [DBFieldAttribute(FieldName = "locationLong", Default = "0")]
        public double Longitude
        {
            get { return this._Longitude; }
            set
            {
                if (value < -180)
                    this._Longitude = -180;
                else if (value > 180)
                    this._Longitude = 180;
                else
                    this._Longitude = value;

                this.TimeZoneID = string.Empty;
                this._TimeZone = null;
            }
        }private double _Longitude = 0;

        [DBFieldAttribute(FieldName = "locationLat", Default = "0")]
        public double Latitude
        {
            get { return this._Latitude; }
            set
            {
                if (value < -90)
                    this._Latitude = -90;
                else if (value > 90)
                    this._Latitude = 90;
                else
                    this._Latitude = value;

                this.TimeZoneID = string.Empty;
                this._TimeZone = null;
            }
        }private double _Latitude = 0;

        [DBFieldAttribute(FieldName = "locationID", Default = "")]
        public string LocationID
        { get; set; }

        [DBFieldAttribute(FieldName = "timeZoneID", Default = "")]
        public string TimeZoneID
        { get; set; }

        [DBFieldAttribute(FieldName = "guiCalendarEnable", Default = "True")]
        public bool GUICalendarEnable
        { get; set; }

        [DBFieldAttribute(FieldName = "weatherRefreshInterval", Default = "1800")]
        public int WeatherRefreshInterval
        {
            get { return this._WeatherRefreshInterval; }
            set
            {
                if (value < 60)
                    value = 60;

                this._WeatherRefreshInterval = value;
            }
        }private int _WeatherRefreshInterval = 1800;

        [DBFieldAttribute(FieldName = "providerApiKey", Default = "")]
        public string ProviderApiKey
        {
            get { return this._ProviderApiKey; }
            set { this._ProviderApiKey = SanityTextValue(value); }
            
        }private string _ProviderApiKey = string.Empty;

        
        public TimeZoneInfo TimeZone
        {
            get
            {
                if (this._TimeZone == null)
                {
                    if (string.IsNullOrWhiteSpace(this.TimeZoneID))
                    {
                        GeoTimeZone.TimeZoneResult tzResult = GeoTimeZone.TimeZoneLookup.GetTimeZone(this.Latitude, this.Longitude);
                        this._TimeZone = TimeZoneConverter.TZConvert.GetTimeZoneInfo(tzResult.Result);
                        if (this._TimeZone != null)
                        {
                            this.TimeZoneID = this._TimeZone.Id;
                            this.CommitNeeded = true;
                        }
                    }
                    else
                        this._TimeZone = TimeZoneInfo.GetSystemTimeZones().FirstOrDefault(tz => tz.Id.Equals(this.TimeZoneID));
                }

                return this._TimeZone;
            }

            set
            {
                this._TimeZone = value;
            }
        }private TimeZoneInfo _TimeZone = null;

        public string ObservationLocation;
        public DateTime RefreshLast = DateTime.MinValue;
        public int WeatherRefreshAttempts = 0;

        public dbGUIUnits GUIUnits
        {
            get
            {
                if (this._GUIUnits == null)
                {
                    this._GUIUnits = dbGUIUnits.Get(this.ID.HasValue ? (int)this.ID : 0);
                    if (this.ID.HasValue && !this._GUIUnits.ID.HasValue)
                    {
                        this._GUIUnits.ParentID = this.ID.Value;
                        this._GUIUnits.CommitNeeded = true;
                    }
                }

                return this._GUIUnits;

            }
        }private dbGUIUnits _GUIUnits = null;

        public List<dbHoliday> Holidays
        {
            get
            {
                if (this._Holidays == null)
                {
                    this._Holidays = dbHoliday.Get(this.ID.HasValue ? (int)this.ID : 0);
                    if (this.ID.HasValue && !this._Holidays.All(h => h.ID.HasValue && this.ID.HasValue))
                    {
                        this._Holidays.ForEach(h =>
                            {
                                h.ParentID = this.ID.Value;
                                h.CommitNeeded = true;
                            });
                    }
                }

                return this._Holidays;

            }
        }private List<dbHoliday> _Holidays = null;

        public List<dbWeatherImage> WeatherImages
        {
            get
            {
                if (this._WeatherImages == null)
                {
                    this._WeatherImages = dbWeatherImage.Get(this.ID.HasValue ? (int)this.ID : 0);
                    if (this.ID.HasValue && !this._WeatherImages.All(h => h.ID.HasValue && this.ID.HasValue))
                    {
                        this._WeatherImages.ForEach(w =>
                        {
                            w.ParentID = this.ID.Value;
                            w.CommitNeeded = true;
                        });
                    }
                }

                return this._WeatherImages;

            }
        }private List<dbWeatherImage> _WeatherImages = null;

        public DateTime GetLocalTime(DateTime dtUtc, out TimeSpan tsUtcOffset)
        {
            //TimeZone
            TimeZoneInfo tz = this.TimeZone;
            if (tz == null)
                tz = TimeZoneInfo.Local;
            tsUtcOffset = tz.GetUtcOffset(dtUtc);
            return dtUtc.Add(tsUtcOffset);
        }

        public new bool CommitNeeded
        {
            get
            {
                if (this.GUIUnits.CommitNeeded)
                    return true;

                if (this.Holidays.Any(h => h.CommitNeeded))
                    return true;

                if (this.WeatherImages.Any(wi => wi.CommitNeeded))
                    return true;

                return base.CommitNeeded;
            }

            set
            {
                base.CommitNeeded = value;
            }
        }

        public override void Commit()
        {
            base.Commit();

            dbGUIUnits units = this.GUIUnits;
            units.ParentID = this.ID.Value;
            units.CommitNeeded = true;
            units.Commit();

            this.Holidays.ForEach(h =>
            {
                h.ParentID = this.ID.Value;
                h.CommitNeeded = true;
                h.Commit();
            });

            this.WeatherImages.ForEach(w =>
            {
                w.ParentID = this.ID.Value;
                w.CommitNeeded = true;
                w.Commit();
            });
        }

        public override void Delete()
        {
            dbGUIUnits units = this.GUIUnits;
            if (units.ID.HasValue)
                units.Delete();

            this.Holidays.ForEach(h =>
            {
                if (h.ID.HasValue)
                    h.Delete();
            });

            this.WeatherImages.ForEach(w =>
            {
                if (w.ID.HasValue)
                    w.Delete();
            });

            base.Delete();
        }

        public override string ToString()
        {
            return this.Name;
        }


        public static List<dbProfile> GetAll()
        {
            return Manager.Get<dbProfile>(null);
        }

        public static dbProfile Get(int iId)
        {
            dbProfile result = (dbProfile)Manager.Get(typeof(dbProfile), iId);
            if (result != null)
                return result;

            List<dbProfile> list = Manager.Get<dbProfile>(null);
            if (list.Count > 0)
                return list[0];

            result = new dbProfile()
            {
                Name = "New location",
                Latitude = 0,
                Longitude = 0,
                LocationID = string.Empty,
                Provider =  Providers.ProviderTypeEnum.FORECA,
            };

            result.CommitNeeded = true;
            result.Commit();

            return result;
        }
    }
}
