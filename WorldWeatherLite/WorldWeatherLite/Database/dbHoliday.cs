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
        [DBFieldAttribute(FieldName = "idParent", Default = "0")]
        public int ParentID
        { get; set; }

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


        public void CopyTo(dbHoliday item)
        {
            foreach (System.Reflection.PropertyInfo pi in this.GetType().GetProperties().Where(f => f.Name != "ParentID" && f.GetCustomAttributes(typeof(DBFieldAttribute), false).Length > 0))
                pi.SetValue(item, pi.GetValue(this, null), null);
        }

        public static List<dbHoliday> Get(int iIdParent)
        {
            List<dbHoliday> list = Manager.Get<dbHoliday>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbHoliday), "idParent"), "=", iIdParent));

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
                }


                //Custom specific holidays
                while (list.Count < 17)
                {
                    db = new dbHoliday()
                        {
                             HolidayType = Utils.HolidayTypeEnum.Unused
                        };

                    list.Add(db);
                }


                //Copy default values from first profile
                List<dbHoliday> listDefault = Manager.Get<dbHoliday>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbHoliday), "idParent"), "=", 1));
                if (listDefault.Count == list.Count)
                {
                    for (int i = 0; i < list.Count; i++)
                        listDefault[i].CopyTo(list[i]);
                }
            }

            return list;
        }
    }
}
