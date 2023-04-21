using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Drawing.Design;
using System.Windows.Forms.Design;

namespace MediaPortal.Pbk.Controls.UIEditor
{
    public interface ISelectorCheckedListBox
    {
        List<object> SelectedObjects
        { get; }

        IEnumerable<object> AvailableObjects
        { get; }
    }

    public class SelectorCheckedListBox : CheckedListBox
    {
        private System.ComponentModel.Container _Components = null;
        private bool _IsUpdatingCheckStates = false;
        private ISelectorCheckedListBox _Value;

        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public ISelectorCheckedListBox Value
        {
            get
            {
                return this._Value;
            }
            set
            {
                this.Items.Clear();
                this._Value = value;
                
                this.fillMembers();
                this.updateCheckedItems();
            }
        }

        public SelectorCheckedListBox()
        {
            this.InitializeComponent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this._Components != null)
                    this._Components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            this.CheckOnClick = true;
        }
        #endregion

        public SelectorCheckedListBoxItem Add(object o, string strCaption)
        {
            SelectorCheckedListBoxItem item = new SelectorCheckedListBoxItem(o, strCaption);
            this.Items.Add(item);
            return item;
        }
        public SelectorCheckedListBoxItem Add(SelectorCheckedListBoxItem item)
        {
            this.Items.Add(item);
            return item;
        }

        protected override void OnItemCheck(ItemCheckEventArgs e)
        {
            base.OnItemCheck(e);

            if (this._IsUpdatingCheckStates)
                return;

            // Get the checked/unchecked item
            SelectorCheckedListBoxItem item = Items[e.Index] as SelectorCheckedListBoxItem;
            // Update other items
            this.updateCheckedItems(item, e.NewValue);
        }

        protected void updateCheckedItems()
        {
            this._IsUpdatingCheckStates = true;

            // Iterate over all items
            for (int i = 0; i < this.Items.Count; i++)
            {
                SelectorCheckedListBoxItem item = this.Items[i] as SelectorCheckedListBoxItem;
                
                this.SetItemChecked(i, this._Value.SelectedObjects.Exists(p => p == item.Value));
            }

            this._IsUpdatingCheckStates = false;
        }

        protected void updateCheckedItems(SelectorCheckedListBoxItem composite, CheckState state)
        {
            if (state == CheckState.Unchecked)
                this._Value.SelectedObjects.Remove(composite.Value);
            // If the item has been checked, combine its bits with the sum
            else if (this._Value.SelectedObjects.IndexOf(composite.Value) < 0)
                this._Value.SelectedObjects.Add(composite.Value);
        }

        private void fillMembers()
        {
            foreach (object o in this._Value.AvailableObjects)
            {
                this.Add(o, o.ToString());
            }
        }

    }

    public class SelectorCheckedListBoxItem
    {
        public object Value;
        public string Caption;

        public SelectorCheckedListBoxItem(object o, string strCaption)
        {
            this.Value = o;
            this.Caption = strCaption;
        }

        public override string ToString()
        {
            return this.Caption;
        }
    }

    public class SelectorUIEditor : UITypeEditor
    {
        // The checklistbox
        private SelectorCheckedListBox _Control;

        public SelectorUIEditor()
        {
            this._Control = new SelectorCheckedListBox();
            this._Control.BorderStyle = BorderStyle.None;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context != null && context.Instance != null && provider != null)
            {
                IWindowsFormsEditorService edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

                if (edSvc != null)
                {
                    this._Control.Value = (ISelectorCheckedListBox)value;
                    edSvc.DropDownControl(this._Control);
                    return this._Control.Value;

                }
            }
            return null;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }


    }

}