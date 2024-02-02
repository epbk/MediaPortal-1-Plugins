using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.Plugins.WorldWeatherLite.Database
{
    [DBTableAttribute("guiUnits")]
    public class dbGUIUnits : dbTable
    {
        [DBFieldAttribute(FieldName = "idParent", Default = "0")]
        public int ParentID
        { get; set; }

        [DBFieldAttribute(FieldName = "guiTemperatureUnit", Default = "Celsius")]
        public GUI.GUITemperatureUnitEnum GUITemperatureUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiPressureUnit", Default = "Millibar")]
        public GUI.GUIPressureUnitEnum GUIPressureUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiDistanceUnit", Default = "Kilometer")]
        public GUI.GUIDistanceUnitEnum GUIDistanceUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiWindUnit", Default = "KilometersPerHour")]
        public GUI.GUIWindUnitEnum GUIWindUnit
        { get; set; }

        [DBFieldAttribute(FieldName = "guiPrecipUnit", Default = "Millimeter")]
        public GUI.GUIPrecipitationUnitEnum GUIPrecipitationUnit
        { get; set; }

        public static dbGUIUnits Get(int iIdParent)
        {
            //Manager.Get<dbGUIUnits>(new BaseCriteria(DBField.GetFieldByDBName(typeof(dbGUIUnits), "idParent"), "=", iIdParent));

            List<dbGUIUnits> list = Manager.Get<dbGUIUnits>(null);
            dbGUIUnits result = list.Find(p => p.ParentID == iIdParent);
            if (result != null)
                return result;

            if (list.Count > 0)
            {
                result = new dbGUIUnits()
                {
                    GUITemperatureUnit = list[0].GUITemperatureUnit,
                    GUIPressureUnit = list[0].GUIPressureUnit,
                    GUIDistanceUnit = list[0].GUIDistanceUnit,
                    GUIWindUnit = list[0].GUIWindUnit,
                    GUIPrecipitationUnit = list[0].GUIPrecipitationUnit
                };
            }
            else
            {
                //Obsolete settings
                Database.dbSettings set = Database.dbSettings.Instance;
                result = new dbGUIUnits()
                {
                    GUITemperatureUnit = set.GUITemperatureUnit,
                    GUIPressureUnit = set.GUIPressureUnit,
                    GUIDistanceUnit = set.GUIDistanceUnit,
                    GUIWindUnit = set.GUIWindUnit,
                    GUIPrecipitationUnit = set.GUIPrecipitationUnit
                };

                //result = new dbGUIUnits()
                //{
                //    GUITemperatureUnit = GUI.GUITemperatureUnitEnum.Celsius,
                //    GUIPressureUnit = GUI.GUIPressureUnitEnum.Millibar,
                //    GUIDistanceUnit = GUI.GUIDistanceUnitEnum.Kilometer,
                //    GUIWindUnit = GUI.GUIWindUnitEnum.KilometersPerHour,
                //    GUIPrecipitationUnit = GUI.GUIPrecipitationUnitEnum.Millimeter
                //};
            }

            return result;
        }
    }
}
