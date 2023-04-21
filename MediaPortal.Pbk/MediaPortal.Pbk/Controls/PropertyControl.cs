using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace MediaPortal.Pbk.Controls
{
    public partial class PropertyControl : UserControl
    {
        public event PropertyValueChangedEventHandler PropertyValueChanged;

        public bool ToolbarVisible
        {
            get { return this.propertyGrid.ToolbarVisible; }
            set { this.propertyGrid.ToolbarVisible = value; }
        }

        public bool HelpVisible
        {
            get { return this.propertyGrid.HelpVisible; }
            set { this.propertyGrid.HelpVisible = value; }
        }

        public PropertySort PropertySort
        {
            get { return this.propertyGrid.PropertySort; }
            set { this.propertyGrid.PropertySort = value; }
        }

        public object SelectedObject
        {
            get
            {
                return this.propertyGrid.SelectedObject != null ? ((PropertyObjectWrapper)this.propertyGrid.SelectedObject).WrappedObject : null;
            }

            set
            {
                if (value is IPropertyObject)
                {
                    PropertyObjectConfig cfg = ((IPropertyObject)value).PropertyConfig;

                    if (cfg != null)
                    {
                        this.SetProperty(value, false, cfg.WriteProps, cfg.BrowsableProps,  cfg.WritableMode, cfg.BrowsableMode);
                        return;
                    }
                }

                this.SetProperty(value, false, null, null, PropertyObjectAttributeModeEnum.Include, PropertyObjectAttributeModeEnum.Include);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GridItem SelectedGridItem
        {
            get { return this.propertyGrid.SelectedGridItem; }
            set { this.propertyGrid.SelectedGridItem = value; }
        }

        public int ColumnWidth
        {
            set { propertygridSetLabelColumnWidth(this.propertyGrid, value); }
        }


        public Color DisabledItemForeColor
        {
            get 
            {
                PropertyInfo p = typeof(PropertyGrid).GetProperty("DisabledItemForeColor", BindingFlags.Public | BindingFlags.Instance);

                return (Color)p.GetValue(this.propertyGrid, null); 
            }
            set
            {
                PropertyInfo p = typeof(PropertyGrid).GetProperty("DisabledItemForeColor", BindingFlags.Public | BindingFlags.Instance);

                p.SetValue(this.propertyGrid, value, null); 
                
            }
        }

        public Color LineColor
        {
            get { return this.propertyGrid.LineColor; }
            set { this.propertyGrid.LineColor = value; }
        }

        public bool PropertyChanged
        {
            get { return this._PropertyChanged; }
        }private bool _PropertyChanged = false;


        public PropertyControl()
        {
            this.InitializeComponent();
        }


        public override void Refresh()
        {
            base.Refresh();

            this.propertyGrid.Refresh();
        }

        public void ClearProperty()
        {
            this.propertyGrid.SelectedObject = null;
        }

        public void CollapseAllGridItems()
        {
            this.propertyGrid.CollapseAllGridItems();
        }

        public void ExpandAllGridItems()
        {
            this.propertyGrid.ExpandAllGridItems();
        }

        /// <summary>
        /// Set property grid object
        /// </summary>
        /// <param name="o">Selected object.</param>
        /// <param name="bReadOnly"></param>
        /// <param name="writableProps"></param>
        /// <param name="browsableProps"></param>
        /// <param name="bWritableMode">True: writable only properties. False: non writable properties.</param>
        /// <param name="bBrowsableMode">True: browsable only properties. False: non browsable properties.</param>
        /// <param name="strPreselectedProperty">Preselect specified property by name. If value is String.Empty then first property is selected. If value is Null then default property is selected.</param>
        [Obsolete()]
        public void SetProperty(object o, bool bReadOnly, IEnumerable<string> writableProps, IEnumerable<string> browsableProps,
            bool bWritableMode, bool bBrowsableMode, string strPreselectedProperty = null)
        {
            this.SetProperty(o, bReadOnly, writableProps, browsableProps,
                bWritableMode ? PropertyObjectAttributeModeEnum.IncludeAndRemoveOthers : PropertyObjectAttributeModeEnum.Exclude,
                bBrowsableMode ? PropertyObjectAttributeModeEnum.IncludeAndRemoveOthers : PropertyObjectAttributeModeEnum.Exclude,
                strPreselectedProperty: strPreselectedProperty);
        }


        /// <summary>
        /// Set property grid object
        /// </summary>
        /// <param name="o">Selected object.</param>
        /// <param name="bReadOnly"></param>
        /// <param name="writableProps"></param>
        /// <param name="browsableProps"></param>
        /// <param name="writableMode">Writable properties mode.</param>
        /// <param name="browsableMode">Browsable properties mode.</param>
        /// <param name="strPreselectedProperty">Preselect specified property by name. If value is String.Empty then first property is selected. If value is Null then default property is selected.</param>
        public void SetProperty(object o, bool bReadOnly, IEnumerable<string> writableProps, IEnumerable<string> browsableProps,
            PropertyObjectAttributeModeEnum writableMode, PropertyObjectAttributeModeEnum browsableMode, string strPreselectedProperty = null)
        {
            if (o == null)
                this.propertyGrid.SelectedObject = null;
            else
            {
                this.propertyGrid.SelectedObject = new PropertyObjectWrapper(o, writableProps, browsableProps, writableMode, browsableMode);
                this.propertyGrid.Enabled = !bReadOnly;

                if (strPreselectedProperty != null && this.propertyGrid.SelectedGridItem != null)
                {
                    //Go to the root
                    GridItem rootItem = this.propertyGrid.SelectedGridItem;
                    while (rootItem.Parent != null)
                    {
                        rootItem = rootItem.Parent;
                    }

                    //Try find the item
                    GridItem item = this.findGridItemByPropertyName(strPreselectedProperty, rootItem.GridItems);
                    if (item != null)
                        item.Select();

                }
            }
        }

        public void ResetSelectedProperty()
        {
            this.propertyGrid.ResetSelectedProperty();
        }

        private GridItem findGridItemByPropertyName(string strName, GridItemCollection items)
        {
            if (items != null)
            {
                foreach (GridItem item in items)
                {
                    if (item.PropertyDescriptor != null
                        && (item.PropertyDescriptor.Name == strName || strName == string.Empty))
                        return item;

                    if (item.GridItems != null)
                    {
                        GridItem result = this.findGridItemByPropertyName(strName, item.GridItems);
                        if (result != null)
                            return result;
                    }
                }
            }

            return null;
        }

        private static void propertygridSetLabelColumnWidth(PropertyGrid grid, int width)
        {
            if (grid == null)
                return;

            FieldInfo fi = grid.GetType().GetField("gridView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fi == null)
                return;

            Control view = fi.GetValue(grid) as Control;
            if (view == null)
                return;

            MethodInfo mi = view.GetType().GetMethod("MoveSplitterTo", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi == null)
                return;
            mi.Invoke(view, new object[] { width });
        }

        private void propertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            this._PropertyChanged = true;
            if (this.PropertyValueChanged != null)
            {
                try { this.PropertyValueChanged(this, e); }
                catch { }
            }
        }

        private void resetValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.propertyGrid.ResetSelectedProperty();
        }

        private void contextMenuStripPropertyGrid_Opening(object sender, CancelEventArgs e)
        {
            GridItem item = this.propertyGrid.SelectedGridItem;
            this.resetValueToolStripMenuItem.Enabled = item != null &&
                                    item.GridItemType == GridItemType.Property &&
                                    item is ITypeDescriptorContext &&
                                    item.PropertyDescriptor.CanResetValue(((ITypeDescriptorContext)item).Instance);
        }

        private void colapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.CollapseAllGridItems();
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ExpandAllGridItems();
        }
    }
}
