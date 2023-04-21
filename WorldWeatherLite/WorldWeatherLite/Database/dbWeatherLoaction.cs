using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("weatherLocation")]
    public class dbWeatherLoaction : dbTable
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
            }
        }private double _Latitude = 0;

        [DBFieldAttribute(FieldName = "locationID", Default = "")]
        public string LocationID
        { get; set; }

        public string ObservationLocation;

        public static dbWeatherLoaction Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = (dbWeatherLoaction)Manager.Get(typeof(dbWeatherLoaction), 1);

                    if (_Instance == null)
                    {
                        Database.dbSettings set = Database.dbSettings.Instance;

                        _Instance = new dbWeatherLoaction()
                        {
                            Name = set.LocationName,
                            Latitude = set.LocationLatitude,
                            Longitude = set.LocationLongitude,
                            LocationID = set.LocationID,
                            Provider = set.Provider,
                        };
                        _Instance.Commit();
                    }
                }

                return _Instance;
            }
        }private static dbWeatherLoaction _Instance = null;
    }
}
