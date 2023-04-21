using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;

namespace MediaPortal.Pbk.Cornerstone.Database.Tables
{
    [DBTable("attribute_values")]
    public class DBAttribute : DatabaseTable
    {
        [DBField]
        public DBAttrDescription Description
        {
            get { return this._Description; }
            set
            {
                this._Description = value;
                this._CommitNeeded = true;
            }
        } private DBAttrDescription _Description;

        [DBField]
        public string Value
        {
            get { return this._Value; }
            set
            {
                this._Value = value;
                this._CommitNeeded = true;
            }
        } private string _Value;
    }

    [DBTable("attributes")]
    public class DBAttrDescription : DatabaseTable
    {
        public enum ValueTypeEnum { INT, FLOAT, LONG, BOOL, STRING }

        // Denotes the methods that an attribute's value can be modified.
        public enum SelectionModeEnum
        {
            Manual,    // The user manually enters the value, which should then be type-checked.

            Selection, // Upon creation of the attribute, the user specifies the list of possible 
            // options.

            Dynamic    // Same as SELECTION but each choice has a criteria attached and the selection
            // is picked and updated automatically.
        }

        #region Database Fields

        [DBField]
        // The user defined name of the attribute. 
        public string Name
        {
            get { return this._Name; }
            set
            {
                this._Name = value;
                this._CommitNeeded = true;
            }
        } private string _Name;


        [DBField(FieldName = "table_type")]
        // The table this attribute is attached to.
        public Type Table
        {
            get { return this._TableType; }
            set
            {
                // make sure the type set is an IAttributeOwner
                bool bValid = false;
                if (value != null)
                {
                    Type[] interfaces = value.GetInterfaces();
                    foreach (Type currType in interfaces)
                    {
                        if (currType == typeof(IAttributeOwner))
                            bValid = true;
                    }
                }

                if (bValid || value == null)
                {
                    this._TableType = value;
                    this._CommitNeeded = true;
                }

                // if it's not an IAttributeOwner, this is invalid.
                else 
                    throw new InvalidOperationException("Owning object must implement IAttributeOwner interface.");
            }
        } Type _TableType;


        [DBField(FieldName = "value_type", Default = "INT")]
        // The type of data stored by this attribute.
        public ValueTypeEnum? ValueType
        {
            get { return this._ValueType; }
            set
            {
                if (this._ValueType != null)
                {
                    this._ValueType = value;
                    this.createDefaultPossibleValues();
                }
                else
                    this._ValueType = value;

                this._CommitNeeded = true;
            }
        } private ValueTypeEnum? _ValueType = null;


        [DBField(FieldName = "default_value")]
        // The table this attribute is attached to.
        public string Default
        {
            get { return this._Default; }
            set { this._Default = value; }
        } string _Default;


        [DBField(FieldName = "selection_mode")]
        // The method the user assigns values to this attribute.
        public SelectionModeEnum SelectionMode
        {
            get { return this._SelectionMode; }
            set
            {
                this._SelectionMode = value;
                this._CommitNeeded = true;
            }
        } private SelectionModeEnum _SelectionMode;


        [DBRelation(AutoRetrieve = true)]
        // List of possible values if this attribute is in SELECTION or DYNAMIC mode.
        public RelationList<DBAttrDescription, DBAttrPossibleValues> PossibleValues
        {
            get
            {
                if (this._PossibleValues == null)
                    this._PossibleValues = new RelationList<DBAttrDescription, DBAttrPossibleValues>(this);

                return this._PossibleValues;
            }
            set
            {
                this._PossibleValues = value;
                this._CommitNeeded = true;
            }
        } RelationList<DBAttrDescription, DBAttrPossibleValues> _PossibleValues;

        #endregion

        #region DatabaseTable Overrides

        // Add this attribute to any movies that don't yet have it.
        public override void AfterCommit()
        {
            base.AfterCommit();

            List<DatabaseTable> dbObjs = this.DBManager.Get(Table, null);
            foreach (DatabaseTable currObj in dbObjs)
            {
                IAttributeOwner currOwner = (IAttributeOwner)currObj;

                bool nNeedsThisAttr = true;
                foreach (DBAttribute currAttr in currOwner.Attributes)
                    if (currAttr.Description != null && currAttr.Description.ID == this.ID)
                    {
                        nNeedsThisAttr = false;
                        break;
                    }

                if (nNeedsThisAttr)
                {
                    DBAttribute newAttr = new DBAttribute();
                    newAttr.Description = this;

                    currOwner.Attributes.Add(newAttr);
                    currOwner.Attributes.Commit();
                }
            }
        }

        #endregion

        #region Private

        private void createDefaultPossibleValues()
        {
            if (this.ValueType == ValueTypeEnum.BOOL)
            {
                // clear out any existing possible values
                foreach (DBAttrPossibleValues currValue in this.PossibleValues)
                    currValue.Delete();

                this.PossibleValues.Clear();

                DBAttrPossibleValues newValue = new DBAttrPossibleValues();
                newValue.Value = "true";
                this.PossibleValues.Add(newValue);

                newValue = new DBAttrPossibleValues();
                newValue.Value = "false";
                this.PossibleValues.Add(newValue);
            }
        }

        #endregion
    }

    [DBTable("attribute_possible_values")]
    public class DBAttrPossibleValues : DatabaseTable
    {
        [DBField]
        public string Value
        {
            get { return this._Value; }
            set
            {
                this._Value = value;
                this._CommitNeeded = true;
            }
        } private string _Value;
    }
}
