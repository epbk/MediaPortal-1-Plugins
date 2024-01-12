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
        public DateTime RefreshLast = DateTime.MinValue;
        public int WeatherRefreshAttempts = 0;

        public override string ToString()
        {
            return this.Name;
        }


        public static List<dbWeatherLoaction> GetAll()
        {
            return Manager.Get<dbWeatherLoaction>(null);
        }

        public static dbWeatherLoaction Get(int iId)
        {
            dbWeatherLoaction result = (dbWeatherLoaction)Manager.Get(typeof(dbWeatherLoaction), iId);
            if (result != null)
                return result;

            List<dbWeatherLoaction> list = Manager.Get<dbWeatherLoaction>(null);
            if (list.Count > 0)
                return list[0];

            result = new dbWeatherLoaction()
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
