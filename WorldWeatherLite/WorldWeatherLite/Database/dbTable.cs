using System;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Configuration;
using System.ComponentModel;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    public abstract class dbTable : DatabaseTable
    {
        private const string DB_BACKUP_FOLDER = "Backup";
        private const string DB_FILE_NAME = "WorldWeatherLite.db3";

        public static DatabaseManager Manager
        {
            get
            {
                if (_Manager == null)
                    _Manager = new DatabaseManager(Config.GetFile(Config.Dir.Database, DB_FILE_NAME),
                        Config.GetSubFolder(Config.Dir.Database, DB_BACKUP_FOLDER));

                return _Manager;
            }
        }private static DatabaseManager _Manager = null;

        public dbTable()
            : base() { }

        public override void Commit()
        {
            if (DBManager == null) DBManager = Manager;
            base.Commit();
        }

        public override void Delete()
        {
            if (DBManager == null) DBManager = Manager;
            base.Delete();
        }

        public static string SanityTextValue(string strValue)
        {
            if (!string.IsNullOrWhiteSpace(strValue))
            {
                if (strValue[0] == ' ' || strValue[strValue.Length - 1] == ' ')
                    return strValue.Trim();
            }
            else
                return string.Empty;

            return strValue;
        }
    }
}
