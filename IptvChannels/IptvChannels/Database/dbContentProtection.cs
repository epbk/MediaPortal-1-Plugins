using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.IptvChannels.Database
{
    [DBTableAttribute("contentProtections")]
    public class dbContentProtection : DbTable
    {
        [DBFieldAttribute(FieldName = "pssh", Default = "")]
        public string PSSH
        { get; set; }

        public static dbContentProtection Get(string strPSSH)
        {
            List<dbContentProtection> result = Manager.Get<dbContentProtection>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbContentProtection), "pssh"), "=", strPSSH));

            if (result.Count == 0)
                return null;

            return result[0];
        }
    }
}
