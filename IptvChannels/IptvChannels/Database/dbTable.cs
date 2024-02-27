using System;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using TvLibrary.Interfaces;

namespace MediaPortal.IptvChannels.Database
{
    public abstract class DbTable : DatabaseTable
    {
        private const string DB_BACKUP_FOLDER = "Backup";
        private const string DB_FILE_NAME = "databaseIptvChannels.db3";

        public static DatabaseManager Manager
        {
            get
            {
                if (_Manager == null)
                {
                    string strDir = string.Format(@"{0}\Pbk\Database\", PathManager.GetDataPath);
                    if (!System.IO.Directory.Exists(strDir))
                        System.IO.Directory.CreateDirectory(strDir);

                    _Manager = new DatabaseManager(string.Format(@"{0}\{1}", strDir, DB_FILE_NAME), string.Format(@"{0}\{1}\", strDir, DB_BACKUP_FOLDER));
                }
                return _Manager;
            }
        }private static DatabaseManager _Manager = null;

        public DbTable()
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
    }
}