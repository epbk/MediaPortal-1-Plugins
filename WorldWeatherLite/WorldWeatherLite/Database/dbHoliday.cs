using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("holiday")]
    public class dbHoliday : dbTable
    {
        [DBFieldAttribute(FieldName = "description", Default = "")]
        public string Description
        { get; set; }

        [DBFieldAttribute(FieldName = "day", Default = "1")]
        public int Day
        { get; set; }

        [DBFieldAttribute(FieldName = "month", Default = "1")]
        public int Month
        { get; set; }

        public static List<dbHoliday> GetAll()
        {
            List<dbHoliday> list = Manager.Get<dbHoliday>(null);

            if (list.Count < 5)
            {
                while (list.Count < 5)
                {
                    dbHoliday db = new dbHoliday();
                    list.Add(db);
                    db.CommitNeeded = true;
                    db.Commit();
                }
            }

            return list;
        }
    }
}
