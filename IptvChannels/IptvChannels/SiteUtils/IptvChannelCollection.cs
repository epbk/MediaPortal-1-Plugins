using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public class IptvChannelCollection : ICustomTypeDescriptor, IEnumerable<IptvChannel>
    {
        private List<IptvChannel> _List;

        public IptvChannelCollection(List<IptvChannel> list)
        {
            this._List = list ?? throw new NullReferenceException("list");
        }

        public IptvChannel this[int index]
        {
            get
            {
                return index >= 0 && index < this._List.Count ? this._List[index] : null;
            }
        }

        public int Count
        {
            get { return this._List.Count; }
        }

        public IptvChannel Find(Predicate<IptvChannel> match)
        {
            return this._List.Find(match);
        }

        public bool Exists(Predicate<IptvChannel> match)
        {
            return this._List.Exists(match);
        }

        public void ForEach(Action<IptvChannel> action)
        {
            this._List.ForEach(action);
        }

        #region ICustomTypeDescriptor

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this, true);
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        public PropertyDescriptorCollection GetProperties()
        {
            PropertyDescriptorCollection pds = new PropertyDescriptorCollection(null);

            for (int i = 0; i < this._List.Count; i++)
            {
                PropertyDescriptor pd = new IptvChannelPropertyDescriptor(this._List[i], "#" + i.ToString("00"));
                pds.Add(pd);
            }
            return pds;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return this.GetProperties();
        }

        #endregion

        #region IEnumerable

        public IEnumerator<IptvChannel> GetEnumerator()
        {
            return this._List.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
