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

        [DBFieldAttribute(FieldName = "holidayType", Default = "Unused")]
        public Utils.HolidayTypeEnum HolidayType
        { get; set; }

        public static List<dbHoliday> GetAll()
        {
            List<dbHoliday> list = Manager.Get<dbHoliday>(null);

            if (list.Count < 17)
            {
                list.ForEach(h =>
                    h.Delete());

                list.Clear();

                dbHoliday db;

                //Default holidays
                for (int i = (int)Utils.HolidayTypeEnum.NewYear; i <= (int)Utils.HolidayTypeEnum.ChristmasDay; i++)
                {
                    db = new dbHoliday()
                    {
                        HolidayType = (Utils.HolidayTypeEnum)i
                    };

                    list.Add(db);
                    db.CommitNeeded = true;
                    db.Commit();
                }


                //Custom specific holidays
                while (list.Count < 17)
                {
                    db = new dbHoliday()
                        {
                             HolidayType = Utils.HolidayTypeEnum.Unused
                        };

                    list.Add(db);
                    db.CommitNeeded = true;
                    db.Commit();
                }
            }

            return list;
        }
    }
}
