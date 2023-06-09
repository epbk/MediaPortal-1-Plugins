﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using MediaPortal.Pbk.Cornerstone.Database;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using MediaPortal.Pbk.Cornerstone.GUI.DesignMode;
using MediaPortal.Pbk.Cornerstone.Database.CustomTypes;
using System.Collections;

namespace MediaPortal.Pbk.Cornerstone.GUI.Controls {
    [Designer(typeof(DBObjectListDesigner))]
    public class DBObjectListEditor : ListView, IFieldDisplaySettingsOwner {

        #region Properties

        [Category("Cornerstone Settings")]
        [Description("If set to false, columns will not be displayed and list items will use the ToString() method for contents.")]
        [DefaultValue(true)]
        public bool DisplayColumns {
            get { return _displayColumns; }
            set { 
                _displayColumns = value;
                if (_displayColumns)
                    HeaderStyle = ColumnHeaderStyle.Nonclickable;
                else
                    HeaderStyle = ColumnHeaderStyle.None;
            }
        } private bool _displayColumns = true;

        [Category("Cornerstone Settings")]
        [Description("Manage the type of database table this control connects to and which fields should be displayed.")]
        public FieldDisplaySettings FieldDisplaySettings {
            get {
                if (_fieldSettings == null) {
                    _fieldSettings = new FieldDisplaySettings();
                    _fieldSettings.Owner = this;
                }

                return _fieldSettings;
            }

            set {
                _fieldSettings = value;
                _fieldSettings.Owner = this;

                OnFieldPropertiesChanged();
            }
        } private FieldDisplaySettings _fieldSettings = null;

        // the list of objects displayed in the list
        [Browsable(false)]
        public DynamicList<DatabaseTable> DatabaseObjects {
            get {
                if (_dbObjects == null) {
                    _dbObjects = new DynamicList<DatabaseTable>();
                    _dbObjects.Changed += new ChangedEventHandler(_dbObjects_Changed);
                }
                return _dbObjects;
            }
        } private DynamicList<DatabaseTable> _dbObjects = null;

        // the list of selected objects
        [Browsable(false)]
        public List<DatabaseTable> SelectedDatabaseObjects {
            get {
                List<DatabaseTable> selected = new List<DatabaseTable>();
                foreach (ListViewItem currItem in SelectedItems) 
                    selected.Add((DatabaseTable)currItem.Tag);
                return selected;
            }
        }


        void _dbObjects_Changed(object sender, EventArgs e) {
            if (this.Visible) repopulateList();
        } 

        #endregion

        private bool autoSize = true;

        public DBObjectListEditor()
            : base() {
            this.View = System.Windows.Forms.View.Details;
            this.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            this.FullRowSelect = true;
            VisibleChanged += new EventHandler(DBObjectListEditor_VisibleChanged);

            this.ListViewItemSorter = new ListViewItemComparer();
        }

        void DBObjectListEditor_VisibleChanged(object sender, EventArgs e) {
            if (Visible) repopulateList();
        }

        public void OnFieldPropertiesChanged() {
            BuildColumns();
            repopulateList();
        }

        #region Private Methods

        private void repopulateList() {
            this.Items.Clear();

            foreach (DatabaseTable currObj in DatabaseObjects) {
                ListViewItem newItem = null;

                foreach (DBField currField in FieldDisplaySettings.Fields) {
                    if (FieldDisplaySettings.getProperties(currField.FieldName) == null ||
                        !FieldDisplaySettings.getProperties(currField.FieldName).Visible)
                        continue;
                    
                    if (newItem == null) {
                        newItem = new ListViewItem();
                        if (_displayColumns) 
                            newItem.Text = currField.GetValue(currObj).ToString().Trim();
                        else
                            newItem.Text = currObj.ToString();

                        newItem.Tag = currObj;
                    }
                    else {
                        ListViewItem.ListViewSubItem newSubItem = new ListViewItem.ListViewSubItem();
                        newSubItem.Text = currField.GetValue(currObj).ToString().Trim();
                        newItem.SubItems.Add(newSubItem);
                    }
                }

                if (newItem != null) this.Items.Add(newItem);
            }

            if (autoSize)
                this.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);

            Sort();
        }

        private void BuildColumns() {
            this.Items.Clear();
            this.Columns.Clear();

            Dictionary<FieldProperty, ColumnHeader> columnLookup = new Dictionary<FieldProperty,ColumnHeader>();

            foreach (DBField currField in FieldDisplaySettings.Fields) {
                FieldProperty properties = FieldDisplaySettings.getProperties(currField.Name);
                if (properties == null) {
                    properties = new FieldProperty();
                    properties.FieldName = currField.Name;
                    properties.Visible = true;
                }

                if (properties.Visible == false)
                    continue;


                ColumnHeader newColumn = new ColumnHeader();
                newColumn.Text = properties.DisplayName;
                newColumn.Name = properties.FieldName + "_column";
                newColumn.Tag = currField;

                columnLookup[properties] = newColumn;

                this.Columns.Add(newColumn);

                // if we are not displaying columns we only need the first one.
                if (!_displayColumns) break;
            }

            foreach (FieldProperty currProperty in FieldDisplaySettings.FieldProperties) {
                if (currProperty.ColumnWidth != null && currProperty.Visible != false) {
                    columnLookup[currProperty].Width = (int)currProperty.ColumnWidth;
                    autoSize = false;
                }
            }
        }

        #endregion

    }

    // Implements the manual sorting of items by columns.
    internal class ListViewItemComparer : IComparer {
        private int col;
        
        public ListViewItemComparer() {
            col = 0;
        }
        
        public ListViewItemComparer(int column) {
            col = column;
        }

        public int Compare(object x, object y) {
            return String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
        }
    }
}
