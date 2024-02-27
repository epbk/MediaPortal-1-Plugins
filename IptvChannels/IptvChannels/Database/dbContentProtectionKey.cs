using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.IptvChannels.Database
{
    [DBTableAttribute("contentProtectionKeys")]
    public class dbContentProtectionKey : DbTable
    {
        [DBFieldAttribute(FieldName = "idParent", Default = "0")]
        public int IdParent
        { get; set; }

        [DBFieldAttribute(FieldName = "kid", Default = "")]
        public string KID
        { get; set; }

        [DBFieldAttribute(FieldName = "key", Default = "")]
        public string Key
        { get; set; }

        public static List<dbContentProtectionKey> Get(int iIdParent)
        {
            return Manager.Get<dbContentProtectionKey>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbContentProtectionKey), "idParent"), "=", iIdParent));
        }
    
    }
}
