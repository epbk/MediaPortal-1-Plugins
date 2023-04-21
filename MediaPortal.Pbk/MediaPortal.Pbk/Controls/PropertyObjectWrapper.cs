using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace MediaPortal.Pbk.Controls
{
    public class PropertyObjectWrapper : CustomTypeDescriptor
    {
        public object WrappedObject { get; private set; }
        public List<string> WritetableProperties { get; private set; }
        public List<string> BrowsableProperties { get; private set; }


        public PropertyObjectAttributeModeEnum BrowsableMode { get; private set; }

        public PropertyObjectAttributeModeEnum WritableMode { get; private set; }


        public PropertyObjectWrapper(object o)
            : base(TypeDescriptor.GetProvider(o).GetTypeDescriptor(o))
        {
            this.WrappedObject = o;
            this.WritetableProperties = new List<string>();
            this.BrowsableProperties = new List<string>();

            if (o is IPropertyObject)
            {
                PropertyObjectConfig cfg = ((IPropertyObject)o).PropertyConfig;

                if (cfg != null)
                {
                    this.WritableMode = cfg.WritableMode;
                    this.BrowsableMode = cfg.BrowsableMode;

                    if (cfg.WriteProps != null)
                        this.WritetableProperties.AddRange(cfg.WriteProps);

                    if (cfg.BrowsableProps != null)
                        this.BrowsableProperties.AddRange(cfg.BrowsableProps);
                }
            }
        }

        public PropertyObjectWrapper(object o, IEnumerable<string> writeProps, IEnumerable<string> browsableProps, PropertyObjectAttributeModeEnum writableMode, PropertyObjectAttributeModeEnum browsableMode)
            : base(TypeDescriptor.GetProvider(o).GetTypeDescriptor(o))
        {
            this.WrappedObject = o;
            this.WritetableProperties = new List<string>();
            this.BrowsableProperties = new List<string>();
            this.WritableMode = writableMode;
            this.BrowsableMode = browsableMode;

            if (writeProps != null)
                this.WritetableProperties.AddRange(writeProps);

            if (browsableProps != null)
                this.BrowsableProperties.AddRange(browsableProps);
        }

        public override PropertyDescriptorCollection GetProperties()
        {
            return this.GetProperties(new Attribute[] { });
        }

        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            List<PropertyDescriptor> result = new List<PropertyDescriptor>();

            IEnumerable<PropertyDescriptor> props = base.GetProperties(attributes).Cast<PropertyDescriptor>();

            foreach (PropertyDescriptor p in props)
            {
                PropertyDescriptor pDescr = null;

                List<Attribute> atts = p.Attributes.Cast<Attribute>().ToList();

                if (this.BrowsableProperties.Count > 0)
                {
                    switch (this.BrowsableMode)
                    {
                        case PropertyObjectAttributeModeEnum.Include:
                            if (!this.BrowsableProperties.Contains(p.Name))
                                goto wr;// not exist: keep original

                            break; //exist: set to browsable

                        case PropertyObjectAttributeModeEnum.IncludeAndRemoveOthers:
                            if (!this.BrowsableProperties.Contains(p.Name))
                                continue; //not exist: remove

                            break; //exist: set to browsable

                        case PropertyObjectAttributeModeEnum.Exclude:
                            if (this.BrowsableProperties.Contains(p.Name))
                                continue; // exist: remove
                            else
                                goto wr; //not exist: keep original

                        case PropertyObjectAttributeModeEnum.ExcludeAndIncludeOthers:
                            if (this.BrowsableProperties.Contains(p.Name))
                                continue; // exist: remove

                            break; //not exist: set to browsable
                    }

                    Attribute aB = atts.Find(a => a.GetType() == typeof(BrowsableAttribute));
                    if (aB != null)
                    {
                        atts.Remove(aB);
                        atts.Add(new BrowsableAttribute(true));
                    }
                }

            wr:
                if (this.WritetableProperties.Count > 0)
                {
                    switch (this.WritableMode)
                    {
                        case PropertyObjectAttributeModeEnum.Include:
                            if (this.WritetableProperties.Contains(p.Name))
                                atts.RemoveAll(a => a.GetType().Equals(typeof(ReadOnlyAttribute)));

                            break;

                        case PropertyObjectAttributeModeEnum.IncludeAndRemoveOthers:
                            atts.RemoveAll(a => a.GetType().Equals(typeof(ReadOnlyAttribute)));

                            if (!this.WritetableProperties.Contains(p.Name))
                                atts.Add(new ReadOnlyAttribute(true));

                            break;

                        case PropertyObjectAttributeModeEnum.Exclude:
                            if (this.WritetableProperties.Contains(p.Name))
                            {
                                atts.RemoveAll(a => a.GetType().Equals(typeof(ReadOnlyAttribute)));
                                atts.Add(new ReadOnlyAttribute(true));
                            }

                            break;

                        case PropertyObjectAttributeModeEnum.ExcludeAndIncludeOthers:
                            atts.RemoveAll(a => a.GetType().Equals(typeof(ReadOnlyAttribute)));

                            if (this.WritetableProperties.Contains(p.Name))
                                atts.Add(new ReadOnlyAttribute(true));

                            break;
                    }
                }

                pDescr = TypeDescriptor.CreateProperty(this.WrappedObject.GetType(), p.Name, p.PropertyType, atts.ToArray());

                if (pDescr != null)
                    result.Add(pDescr);
            }

            return new PropertyDescriptorCollection(result.ToArray());
        }
    }
}
