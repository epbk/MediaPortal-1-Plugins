using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;

namespace MediaPortal.Pbk.Cornerstone.Database
{
    public interface IAttributeOwner
    {
        RelationList<DatabaseTable, DBAttribute> Attributes
        {
            get;
        }
    }
}
