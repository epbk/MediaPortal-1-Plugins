using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Reflection;


namespace MediaPortal.Pbk.Controls.UIEditor
{
    /// <summary>
    /// Flags enumeration type converter.
    /// </summary>
    internal class FlagsEnumConverter : EnumConverter
    {
        /// <summary>
        /// This class represents an enumeration field in the property grid.
        /// </summary>
        protected class EnumFieldDescriptor : SimplePropertyDescriptor
        {
            #region Fields
            /// <summary>
            /// Stores the context which the enumeration field descriptor was created in.
            /// </summary>
            private ITypeDescriptorContext _Context;
            #endregion

            #region Methods
            /// <summary>
            /// Creates an instance of the enumeration field descriptor class.
            /// </summary>
            /// <param name="componentType">The type of the enumeration.</param>
            /// <param name="strName">The name of the enumeration field.</param>
            /// <param name="context">The current context.</param>
            public EnumFieldDescriptor(Type componentType, string strName, ITypeDescriptorContext context)
                : base(componentType, strName, typeof(bool))
            {
                this._Context = context;
            }

            /// <summary>
            /// Retrieves the value of the enumeration field.
            /// </summary>
            /// <param name="component">
            /// The instance of the enumeration type which to retrieve the field value for.
            /// </param>
            /// <returns>
            /// True if the enumeration field is included to the enumeration; 
            /// otherwise, False.
            /// </returns>
            public override object GetValue(object component)
            {
                return ((int)component & (int)Enum.Parse(this.ComponentType, this.Name)) != 0;
            }

            /// <summary>
            /// Sets the value of the enumeration field.
            /// </summary>
            /// <param name="component">
            /// The instance of the enumeration type which to set the field value to.
            /// </param>
            /// <param name="value">
            /// True if the enumeration field should included to the enumeration; 
            /// otherwise, False.
            /// </param>
            public override void SetValue(object component, object value)
            {
                bool bValue = (bool)value;
                int iNewValue;
                if (bValue)
                    iNewValue = ((int)component) | (int)Enum.Parse(this.ComponentType, this.Name);
                else
                    iNewValue = ((int)component) & ~(int)Enum.Parse(this.ComponentType, this.Name);

                FieldInfo info = component.GetType().GetField("value__", BindingFlags.Instance | BindingFlags.Public);
                info.SetValue(component, iNewValue);
                this._Context.PropertyDescriptor.SetValue(this._Context.Instance, component);
            }

            /// <summary>
            /// Retrieves a value indicating whether the enumeration 
            /// field is set to a non-default value.
            /// </summary>
            public override bool ShouldSerializeValue(object component)
            {
                return (bool)this.GetValue(component) != this.GetDefaultValue();
            }

            /// <summary>
            /// Resets the enumeration field to its default value.
            /// </summary>
            public override void ResetValue(object component)
            {
                this.SetValue(component, this.GetDefaultValue());
            }

            /// <summary>
            /// Retrieves a value indicating whether the enumeration 
            /// field can be reset to the default value.
            /// </summary>
            public override bool CanResetValue(object component)
            {
                return this.ShouldSerializeValue(component);
            }

            /// <summary>
            /// Retrieves the enumerations field’s default value.
            /// </summary>
            private bool GetDefaultValue()
            {
                object defaultValue = null;
                string propertyName = this._Context.PropertyDescriptor.Name;
                Type myComponentType = this._Context.PropertyDescriptor.ComponentType;

                // Get DefaultValueAttribute
                DefaultValueAttribute defaultValueAttribute = (DefaultValueAttribute)Attribute.GetCustomAttribute(
                    myComponentType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    typeof(DefaultValueAttribute));

                if (defaultValueAttribute != null)
                    defaultValue = defaultValueAttribute.Value;

                if (defaultValue != null)
                    return ((int)defaultValue & (int)Enum.Parse(this.ComponentType, this.Name)) != 0;
                else
                    return false;
            }
            #endregion

            #region Properties
            public override AttributeCollection Attributes
            {
                get
                {
                    return new AttributeCollection(new Attribute[] { RefreshPropertiesAttribute.Repaint });
                }
            }
            #endregion
        }

        #region Methods
        /// <summary>
        /// Creates an instance of the FlagsEnumConverter class.
        /// </summary>
        /// <param name="type">The type of the enumeration.</param>
        public FlagsEnumConverter(Type type)
            : base(type) { }

        /// <summary>
        /// Retrieves the property descriptors for the enumeration fields. 
        /// These property descriptors will be used by the property grid 
        /// to show separate enumeration fields.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="value">A value of an enumeration type.</param>
        /// <param name="attributes"></param>
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            if (context != null)
            {
                Type type = value.GetType();
                string[] names = Enum.GetNames(type);
                Array values = Enum.GetValues(type);
                if (names != null)
                {
                    PropertyDescriptorCollection result = new PropertyDescriptorCollection(null);
                    for (int i = 0; i < names.Length; i++)
                    {
                        if ((int)values.GetValue(i) != 0 && names[i] != "All")
                            result.Add(new EnumFieldDescriptor(type, names[i], context));
                    }
                    return result;
                }
            }
            return base.GetProperties(context, value, attributes);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            if (context != null)
                return true;
            else
                return base.GetPropertiesSupported(context);
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return false;
        }
        #endregion
    }
}
