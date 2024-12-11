using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.Threading;

namespace MediaPortal.IptvChannels.Database
{
    [DBTableAttribute("contentProtections")]
    public class dbContentProtectionBox : DbTable
    {
        [DBFieldAttribute(FieldName = "pssh", Default = "")]
        public string PSSH
        { get; set; }

        [DBFieldAttribute(FieldName = "licenceServer", Default = "")]
        public string LicenceServer
        { get; set; }

        [DBFieldAttribute(FieldName = "lastRefresh", Default = "1900-01-01 00:00:00Z")]
        public DateTime LastRefresh
        { get; set; }

        [DBFieldAttribute(FieldName = "lastAccess", Default = "1900-01-01 00:00:00Z")]
        public DateTime LastAccess
        { get; set; }

        public bool Refreshing = false;
        public readonly ManualResetEvent FlagRefreshDone = new ManualResetEvent(false);

        public readonly List<dbContentProtectionKey> Keys = new List<dbContentProtectionKey>();

        public dbContentProtectionBox()
        { }
        public dbContentProtectionBox(string strLicenceServer, string strPSSH)
        {
            this.LicenceServer = strLicenceServer;
            this.PSSH = strPSSH;
        }

        public static dbContentProtectionBox Get(string strLicenceServer, string strPSSH)
        {
            BaseCriteria critLic = new BaseCriteria(DBField.GetFieldByDBName(typeof(dbContentProtectionBox), "licenceServer"), "=", strLicenceServer);
            BaseCriteria critPSSH = new BaseCriteria(DBField.GetFieldByDBName(typeof(dbContentProtectionBox), "pssh"), "=", strPSSH);
            List<dbContentProtectionBox> result = Manager.Get<dbContentProtectionBox>(new GroupedCriteria(critLic, GroupedCriteria.Operator.AND, critPSSH));

            if (result.Count == 0)
                return null;

            result[0].Keys.AddRange(dbContentProtectionKey.Get((int)result[0].ID));

            return result[0];
        }
    }
}
