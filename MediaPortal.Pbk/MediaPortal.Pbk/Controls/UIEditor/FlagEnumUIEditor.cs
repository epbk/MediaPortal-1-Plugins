/* All code in this file Copyright G Himangi
 * Downloaded from http://www.codeproject.com/useritems/flagenumeditor.asp
 * Modified the Namespace just because it was too generic.
 */
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using System.Reflection;


namespace MediaPortal.Pbk.Controls.UIEditor
{
    public class FlagCheckedListBox : CheckedListBox
    {
        private System.ComponentModel.Container _Components = null;
        private bool _IsUpdatingCheckStates = false;
        private Type _EnumType;
        private Enum _EnumValue;

        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public Enum EnumValue
        {
            get
            {
                object e = Enum.ToObject(this._EnumType, this.GetCurrentValue());
                return (Enum)e;
            }
            set
            {
                this.Items.Clear();
                this._EnumValue = value; // Store the current enum value
                this._EnumType = value.GetType(); // Store enum type
                this.fillEnumMembers(); // Add items for enum members
                this.applyEnumValue(); // Check/uncheck items depending on enum value
            }
        }

        public FlagCheckedListBox()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();

            // TODO: Add any initialization after the InitForm call

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_Components != null)
                    _Components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            // 
            // FlaggedCheckedListBox
            // 
            this.CheckOnClick = true;

        }
        #endregion

        // Adds an integer value and its associated description
        public FlagCheckedListBoxItem Add(int iValue, string strCaption)
        {
            FlagCheckedListBoxItem item = new FlagCheckedListBoxItem(iValue, strCaption);
            this.Items.Add(item);
            return item;
        }
        public FlagCheckedListBoxItem Add(FlagCheckedListBoxItem item)
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
            FlagCheckedListBoxItem item = Items[e.Index] as FlagCheckedListBoxItem;
            // Update other items
            this.updateCheckedItems(item, e.NewValue);
        }

        // Checks/Unchecks items depending on the give bitvalue
        protected void UpdateCheckedItems(int iValue)
        {
            this._IsUpdatingCheckStates = true;

            // Iterate over all items
            for (int i = 0; i < this.Items.Count; i++)
            {
                FlagCheckedListBoxItem item = this.Items[i] as FlagCheckedListBoxItem;

                if (item.Value == 0)
                    this.SetItemChecked(i, iValue == 0);
                else
                {

                    // If the bit for the current item is on in the bitvalue, check it
                    if ((item.Value & iValue) == item.Value && item.Value != 0)
                        this.SetItemChecked(i, true);
                    // Otherwise uncheck it
                    else
                        this.SetItemChecked(i, false);
                }
            }

            this._IsUpdatingCheckStates = false;

        }

        // Updates items in the checklistbox
        // composite = The item that was checked/unchecked
        // cs = The check state of that item
        protected void updateCheckedItems(FlagCheckedListBoxItem composite, CheckState state)
        {
            // If the value of the item is 0, call directly.
            if (composite.Value == 0)
                this.UpdateCheckedItems(0);


            // Get the total value of all checked items
            int iSum = 0;
            for (int i = 0; i < this.Items.Count; i++)
            {
                FlagCheckedListBoxItem item = this.Items[i] as FlagCheckedListBoxItem;

                // If item is checked, add its value to the sum.
                if (this.GetItemChecked(i))
                    iSum |= item.Value;
            }

            // If the item has been unchecked, remove its bits from the sum
            if (state == CheckState.Unchecked)
                iSum = iSum & (~composite.Value);
            // If the item has been checked, combine its bits with the sum
            else
                iSum |= composite.Value;

            // Update all items in the checklistbox based on the final bit value
            this.UpdateCheckedItems(iSum);
        }


        // Gets the current bit value corresponding to all checked items
        public int GetCurrentValue()
        {
            int iSum = 0;

            for (int i = 0; i < this.Items.Count; i++)
            {
                FlagCheckedListBoxItem item = this.Items[i] as FlagCheckedListBoxItem;

                if (this.GetItemChecked(i))
                    iSum |= item.Value;
            }

            return iSum;
        }


        // Adds items to the checklistbox based on the members of the enum
        private void fillEnumMembers()
        {
            foreach (string strName in Enum.GetNames(this._EnumType))
            {
                object val = Enum.Parse(this._EnumType, strName);
                int iVal = (int)Convert.ChangeType(val, typeof(int));


                FieldInfo fi = this._EnumType.GetField(Enum.GetName(this._EnumType, iVal));
                if (fi != null)
                {
                    DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                    if (dna != null)
                    {
                        //Description name
                        this.Add(iVal, dna.Description);
                        continue;
                    }
                }


                //Default name
                this.Add(iVal, strName);
            }
        }

        // Checks/unchecks items based on the current value of the enum variable
        private void applyEnumValue()
        {
            int iVal = (int)Convert.ChangeType(this._EnumValue, typeof(int));
            this.UpdateCheckedItems(iVal);

        }
    }

    // Represents an item in the checklistbox
    public class FlagCheckedListBoxItem
    {
        public int Value;
        public string Caption;

        public FlagCheckedListBoxItem(int iValue, string strCaption)
        {
            this.Value = iValue;
            this.Caption = strCaption;
        }

        public override string ToString()
        {
            return this.Caption;
        }

        // Returns true if the value corresponds to a single bit being set
        public bool IsFlag
        {
            get
            {
                return ((this.Value & (this.Value - 1)) == 0);
            }
        }

        // Returns true if this value is a member of the composite bit value
        public bool IsMemberFlag(FlagCheckedListBoxItem composite)
        {
            return (this.IsFlag && ((this.Value & composite.Value) == this.Value));
        }
    }


    // UITypeEditor for flag enums
    public class FlagEnumUIEditor : UITypeEditor
    {
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context != null && context.Instance != null && provider != null)
            {
                IWindowsFormsEditorService edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

                if (edSvc != null)
                {
                    Enum e = (Enum)Convert.ChangeType(value, context.PropertyDescriptor.PropertyType);

                    // The checklistbox
                    FlagCheckedListBox flagEnumCheckedListBox = new FlagCheckedListBox();
                    flagEnumCheckedListBox.BorderStyle = BorderStyle.None;
                    flagEnumCheckedListBox.EnumValue = e;

                    edSvc.DropDownControl(flagEnumCheckedListBox);
                    return flagEnumCheckedListBox.EnumValue;

                }
            }
            return null;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }
    }

    public class EnumUIEditor : UITypeEditor
    {
        private ListBox _ListBox;
        private IWindowsFormsEditorService _EdSvc;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            if (context != null && context.Instance != null && provider != null)
            {
                this._EdSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

                if (this._EdSvc != null)
                {
                    Type t = context.PropertyDescriptor.PropertyType;

                    string strE = (string)Convert.ChangeType(value, typeof(string));

                    // The checklistbox
                    this._ListBox = new ListBox();
                    this._ListBox.BorderStyle = BorderStyle.None;

                    FieldInfo[] fileds = t.GetFields();

                    int iIdx = 0;

                    foreach (object o in Enum.GetValues(t))
                    {
                        FieldInfo fi = t.GetField(Enum.GetName(t, o));
                        DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                        if (dna != null)
                            this._ListBox.Items.Add(dna.Description);
                        else
                            this._ListBox.Items.Add(fi.Name);

                        if (strE == fi.Name || (dna != null && strE == dna.Description))
                            iIdx = this._ListBox.Items.Count - 1;
                    }

                    this._ListBox.Height = Math.Min(200, Math.Max(40, (this._ListBox.Items.Count + 1) * 14));

                    this._ListBox.SelectedIndex = iIdx;

                    this._ListBox.SelectedIndexChanged += this.cbSelectedIndexChanged;

                    this._EdSvc.DropDownControl(this._ListBox);

                    string strValue = (string)this._ListBox.SelectedItem;

                    foreach (FieldInfo fi in fileds)
                    {
                        DescriptionAttribute dna = (DescriptionAttribute)Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute));

                        if ((dna != null) && (strValue == dna.Description))
                            return Enum.Parse(context.PropertyDescriptor.PropertyType, fi.Name);
                        else if (strValue == fi.Name)
                            return Enum.Parse(context.PropertyDescriptor.PropertyType, strValue);
                    }

                    return null;

                }
            }
            return null;
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        private void cbSelectedIndexChanged(object sender, EventArgs e)
        {
            this._EdSvc.CloseDropDown();
        }
    }

}