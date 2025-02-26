using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace MediaPortal.IptvChannels.SiteUtils
{
    public class IptvChannelPropertyDescriptor : PropertyDescriptor
    {
        private IptvChannel _Channel = null;
        private string _Name;

        public IptvChannelPropertyDescriptor(IptvChannel channel, string strName)
            : base(strName, null)
        {
            this._Channel = channel;
            this._Name = strName;
        }

        public override AttributeCollection Attributes
        {
            get
            {
                return new AttributeCollection(null);
            }
        }

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType
        {
            get
            {
                return this._Channel.GetType();
            }
        }

        public override string DisplayName
        {
            get
            {
                return base.Name;
            }
        }

        public override string Description
        {
            get
            {
                return this._Channel.Id;
            }
        }

        public override object GetValue(object component)
        {
            return this._Channel;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override string Name
        {
            get { return this._Name; }
        }

        public override Type PropertyType
        {
            get { return this._Channel.GetType(); }
        }

        public override void ResetValue(object component) { }

        public override bool ShouldSerializeValue(object component)
        {
            return true;
        }

        public override void SetValue(object component, object value)
        {
            // this._Link = value;
        }
    }
}
