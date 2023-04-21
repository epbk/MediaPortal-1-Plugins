using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using MediaPortal.Pbk.Cornerstone.Extensions;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    [DBTableAttribute("criteria")]
    public class DBCriteria<T> : GenericDatabaseTable<T>, IFilter<T>, IGenericFilter
        where T : DatabaseTable
    {

        public enum OperatorEnum
        {
            // general operators
            [Description("equals")]
            EQUAL,

            [Description("does not equal")]
            NOT_EQUAL,


            // numeric operators
            [Description("is less than")]
            LESS_THAN,

            [Description("is greater than")]
            GREATER_THAN,


            // string operators
            [Description("contains")]
            CONTAINS,

            [Description("does not contain")]
            NOT_CONTAIN,

            [Description("begins with")]
            BEGINS_WITH,

            [Description("does not begin with")]
            NOT_BEGIN_WITH,

            [Description("ends with")]
            ENDS_WITH,

            [Description("does not end with")]
            NOT_ENDS_WITH
        }

        #region IFilter<T> Members

        public event FilterUpdatedDelegate<T> Updated;

        public HashSet<T> Filter(ICollection<T> input)
        {
            return this.Filter(input, false);
        }

        public HashSet<T> Filter(ICollection<T> input, bool bForceActive)
        {
            bool bAtive = bForceActive || this._Active;
            HashSet<T> results = new HashSet<T>();

            // if we are not active, just return the inputs.
            if (!bAtive)
            {
                if (input is HashSet<T>)
                    return (HashSet<T>)input;

                foreach (T currItem in input)
                    results.Add(currItem);
                return results;
            }

            foreach (T currItem in input)
            {
                if (this.Relation == null)
                {
                    if (isIncluded(this.Field.GetValue(currItem)))
                        results.Add(currItem);
                }
                else
                {
                    foreach (DatabaseTable currSubItem in this.Relation.GetRelationList(currItem))
                        if (isIncluded(this.Field.GetValue(currSubItem)))
                        {
                            results.Add(currItem);
                            break;
                        }
                }
            }

            return results;
        }

        private bool isIncluded(object value)
        {
            switch (this.Operator)
            {
                case OperatorEnum.EQUAL:
                    if (value == null)
                    {
                        if (this.Value == null || string.IsNullOrEmpty(Value.ToString().Trim()))
                            return true;

                        return false;
                    }
                    else if (this.Value == null)
                    {
                        if (value == null || string.IsNullOrEmpty(value.ToString().Trim()))
                            return true;

                        return false;
                    }
                    else if (this.Field.Type == typeof(StringList))
                    {
                        if (((StringList)value).Contains(this.Value.ToString().Trim()))
                            return true;
                    }
                    else if (this.Field.Type == typeof(string))
                    {
                        if (value.ToString().Trim().ToLower().Equals(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    else if (this.Field.Type == typeof(DateTime))
                    {
                        if (((DateTime)value).Date.Equals(doDateTimeConversion(this.Value)))
                            return true;
                    }
                    else
                    {
                        if (value == null && this.Value != null)
                            return false;

                        if (value == null && this.Value == null)
                            return true;

                        if (value != null && this.Value == null)
                            return false;

                        if (value.Equals(this.Value))
                            return true;
                    }
                    break;

                case OperatorEnum.NOT_EQUAL:
                    if (this.Field.Type == typeof(StringList))
                    {
                        if (!((StringList)value).Contains(this.Value.ToString().Trim()))
                            return true;
                    }
                    else if (this.Field.Type == typeof(string))
                    {
                        if (!value.ToString().Trim().ToLower().Equals(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    else if (this.Field.Type == typeof(DateTime))
                    {
                        if (!((DateTime)value).Date.Equals(doDateTimeConversion(this.Value)))
                            return true;
                    }
                    else
                    {
                        if (!value.Equals(this.Value))
                            return true;
                    }
                    break;

                case OperatorEnum.CONTAINS:
                    if (value.ToString().Trim().ToLower().Contains(this.Value.ToString().Trim().ToLower()))
                        return true;
                    break;

                case OperatorEnum.NOT_CONTAIN:
                    if (!value.ToString().Trim().ToLower().Contains(this.Value.ToString().Trim().ToLower()))
                        return true;
                    break;

                case OperatorEnum.GREATER_THAN:
                    if (value is int)
                    {
                        if ((int)value > (int)this.Value)
                            return true;
                    }
                    else if (value is float)
                    {
                        if ((float)value > (float)this.Value)
                            return true;
                    }
                    else if (this.Field.Type == typeof(DateTime))
                    {
                        if (((DateTime)value).Date > doDateTimeConversion(this.Value))
                            return true;
                    }
                    break;

                case OperatorEnum.LESS_THAN:
                    if (value is int)
                    {
                        if ((int)value < (int)this.Value)
                            return true;
                    }
                    else if (value is float)
                    {
                        if ((float)value < (float)this.Value)
                            return true;
                    }
                    else if (this.Field.Type == typeof(DateTime))
                    {
                        if (((DateTime)value).Date < doDateTimeConversion(this.Value))
                            return true;
                    }
                    break;
                case OperatorEnum.BEGINS_WITH:
                    if (this.Field.Type == typeof(StringList))
                    {
                        foreach (string strCurrStr in (StringList)value)
                        {
                            if (strCurrStr.Trim().ToLower().StartsWith(this.Value.ToString().Trim().ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (value.ToString().Trim().ToLower().StartsWith(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    break;

                case OperatorEnum.NOT_BEGIN_WITH:
                    if (this.Field.Type == typeof(StringList))
                    {
                        foreach (string strCurrStr in (StringList)value)
                        {
                            if (!strCurrStr.Trim().ToLower().StartsWith(this.Value.ToString().Trim().ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (!value.ToString().Trim().ToLower().StartsWith(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    break;

                case OperatorEnum.ENDS_WITH:
                    if (this.Field.Type == typeof(StringList))
                    {
                        foreach (string strCurrStr in (StringList)value)
                        {
                            if (strCurrStr.Trim().ToLower().EndsWith(this.Value.ToString().Trim().ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (value.ToString().ToLower().Trim().EndsWith(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    break;

                case OperatorEnum.NOT_ENDS_WITH:
                    if (Field.Type == typeof(StringList))
                    {
                        foreach (string strCurrStr in (StringList)value)
                        {
                            if (!strCurrStr.Trim().ToLower().EndsWith(this.Value.ToString().Trim().ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (!value.ToString().Trim().ToLower().EndsWith(this.Value.ToString().Trim().ToLower()))
                            return true;
                    }
                    break;
            }
            return false;
        }

        public bool Active
        {
            get { return this._Active; }
            set
            {
                if (this._Active != value)
                {
                    this._Active = value;

                    if (this.Updated != null)
                        this.Updated(this);
                }
            }
        }private bool _Active = true;

        #endregion

        #region Database Fields

        [DBField]
        public DBField Field
        {
            get { return this._Field; }

            set
            {
                this._Field = value;
                this._CommitNeeded = true;
            }
        } private DBField _Field = null;

        [DBField]
        public DBRelation Relation
        {
            get { return this._SubTableRelationship; }

            set
            {
                this._SubTableRelationship = value;
                this._CommitNeeded = true;
            }
        } private DBRelation _SubTableRelationship;

        [DBField]
        public OperatorEnum Operator
        {
            get { return this._Operator; }

            set
            {
                this._Operator = value;
                this._CommitNeeded = true;
            }
        } private OperatorEnum _Operator;

        [DBField]
        public object Value
        {
            get
            {
                return this._Value;
            }

            set
            {
                if (value == null && (this.Field == null || this.Field.IsNullable))
                    this._Value = value;
                else if (this.Field == null || value.GetType() == this.Field.Type)
                    this._Value = value;
                else if (this.Field.Type == typeof(StringList))
                    this._Value = value.ToString();
                else if (this.Field.Type == typeof(DateTime))
                {
                    DateTime dtNewValue = DateTime.Now;
                    if (DateTime.TryParse((string)value, out dtNewValue))
                        this._Value = dtNewValue;
                    else
                        this._Value = value.ToString();
                }
                else if (value is string)
                    this._Value = Field.ConvertString(this.DBManager, (string)value);
                else
                    this._Value = null;

                this._CommitNeeded = true;
            }
        } private object _Value;

        #endregion

        public List<OperatorEnum> GetOperators()
        {
            return this.GetOperators(this._Field);
        }

        public List<OperatorEnum> GetOperators(DBField field)
        {
            List<OperatorEnum> rtn = new List<OperatorEnum>();

            if (!field.AllowManualFilterInput)
            {
                rtn.Add(DBCriteria<T>.OperatorEnum.EQUAL);
                rtn.Add(DBCriteria<T>.OperatorEnum.NOT_EQUAL);
                return rtn;
            }

            switch (field.DBType)
            {
                case DBField.DBDataType.ENUM:
                case DBField.DBDataType.BOOL:
                    rtn.Add(DBCriteria<T>.OperatorEnum.EQUAL);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_EQUAL);
                    break;

                case DBField.DBDataType.DATE_TIME:
                case DBField.DBDataType.INTEGER:
                case DBField.DBDataType.LONG:
                case DBField.DBDataType.REAL:
                    rtn.Add(DBCriteria<T>.OperatorEnum.EQUAL);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_EQUAL);
                    rtn.Add(DBCriteria<T>.OperatorEnum.LESS_THAN);
                    rtn.Add(DBCriteria<T>.OperatorEnum.GREATER_THAN);
                    break;

                case DBField.DBDataType.TEXT:
                    rtn.Add(DBCriteria<T>.OperatorEnum.EQUAL);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_EQUAL);
                    rtn.Add(DBCriteria<T>.OperatorEnum.CONTAINS);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_CONTAIN);
                    rtn.Add(DBCriteria<T>.OperatorEnum.BEGINS_WITH);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_BEGIN_WITH);
                    rtn.Add(DBCriteria<T>.OperatorEnum.ENDS_WITH);
                    rtn.Add(DBCriteria<T>.OperatorEnum.NOT_ENDS_WITH);
                    break;

                case DBField.DBDataType.STRING_OBJECT:
                    if (field.Type == typeof(StringList))
                    {
                        rtn.Add(DBCriteria<T>.OperatorEnum.EQUAL);
                        rtn.Add(DBCriteria<T>.OperatorEnum.NOT_EQUAL);
                        rtn.Add(DBCriteria<T>.OperatorEnum.CONTAINS);
                        rtn.Add(DBCriteria<T>.OperatorEnum.NOT_CONTAIN);
                        rtn.Add(DBCriteria<T>.OperatorEnum.BEGINS_WITH);
                        rtn.Add(DBCriteria<T>.OperatorEnum.NOT_BEGIN_WITH);
                        rtn.Add(DBCriteria<T>.OperatorEnum.ENDS_WITH);
                        rtn.Add(DBCriteria<T>.OperatorEnum.NOT_ENDS_WITH);
                    }
                    break;
                case DBField.DBDataType.TYPE:
                case DBField.DBDataType.DB_FIELD:
                case DBField.DBDataType.DB_OBJECT:
                    break;
            }

            return rtn;
        }

        /// <summary>
        /// Returns a calculated date value
        /// </summary>
        /// <param name="date">actual date or relative format string</param>
        /// <returns>parsed date as given or calculated relative date</returns>
        private DateTime doDateTimeConversion(object date)
        {
            DateTime dtNewDate = DateTime.MinValue;
            string strPart = null;
            int iDiff = 0;

            string strValue = date.ToString();

            // try to parse a date from the given object
            if (!DateTime.TryParse(date.ToString(), out dtNewDate))
            {
                // if parsing fails we have a relative string format
                dtNewDate = DateTime.Today;
                int iLength = strValue.Length - 1;
                strPart = strValue[iLength].ToString();

                if (iLength > 0)
                    int.TryParse(strValue.Substring(0, iLength), out iDiff);
            }

            // based on the given datepart in the relative string we get our calculated date
            // if no part is given we just return the newDate value
            switch (strPart)
            {
                // --- Days
                case "d": // within day (ago) or today
                    return dtNewDate.AddDays(iDiff);
                // --- Weeks
                case "w":
                case "W":
                    if (strPart == "w" && iDiff == 0 || strPart == "W") // start of (this/diff) week (by week number)
                        return dtNewDate.GetStartOfWeek().AddDays(iDiff * 7);
                    else // within a week span (ago) (7 days)
                        return dtNewDate.AddDays(iDiff * 7);
                // --- Months
                case "m":
                case "M":
                    if (strPart == "m" && iDiff == 0 || strPart == "M") // start of (this/diff) month
                        return dtNewDate.GetStartOfMonth().AddMonths(iDiff);
                    else // within a month span (ago)
                        return dtNewDate.AddMonths(iDiff);
                // --- Years
                case "y":
                case "Y":
                    if (strPart == "y" && iDiff == 0 || strPart == "Y") // start of (this/diff) month
                        return dtNewDate.GetStartOfYear().AddYears(iDiff);
                    else // within a year span (ago)
                        return dtNewDate.AddYears(iDiff);
                // --- Today or parsed date value
                default:
                    return dtNewDate;
            }
        }

    }


}
