using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace MediaPortal.Pbk.Cornerstone.Collections
{
    public class DiskCachedDictionary<TKey, TValue> : CachedDictionary<TKey, int>
    {
        private BinaryFormatter _Serializer = new BinaryFormatter();

        private bool _Initialized = false;
        private string _CacheLocation = null;

        public void Init()
        {
            if (this._Initialized)
                return;

            this._Initialized = true;

            this._CacheLocation = Path.GetTempPath() + @"Cornerstone\DiskCachedDictionary\" + this.GetHashCode() + @"\";
            Directory.CreateDirectory(this._CacheLocation);
        }

        public void DeInit()
        {
            if (!this._Initialized)
                return;

            Directory.Delete(this._CacheLocation, true);
            this._Initialized = false;
        }

        ~DiskCachedDictionary()
        {
            this.DeInit();
        }

        private int Serialize(TKey key, TValue value)
        {
            this.Init();

            int iLookup = key.GetHashCode();
            FileStream stream = File.Create(this._CacheLocation + iLookup);

            this._Serializer.Serialize(stream, value);
            stream.Close();

            return iLookup;
        }

        private TValue Deserialize(int iLookup)
        {
            this.Init();

            FileStream stream = File.OpenRead(this._CacheLocation + iLookup);
            return (TValue)this._Serializer.Deserialize(stream);
        }

        public void Add(TKey key, TValue value)
        {
            int iLookup = this.Serialize(key, value);
            base.Add(key, iLookup);
        }

        public override bool Remove(TKey key)
        {
            File.Delete(this._CacheLocation + key.GetHashCode());
            return base.Remove(key);
        }

        public new TValue this[TKey key]
        {
            get
            {
                int iLookup = base[key];
                return this.Deserialize(iLookup);
            }
            set
            {
                int iLookup = this.Serialize(key, value);
                base[key] = iLookup;
            }
        }

        public override void Clear()
        {
            this.DeInit();
            base.Clear();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int iLookup;

            bool bSuccess = base.TryGetValue(key, out iLookup);
            if (bSuccess)
                value = this.Deserialize(iLookup);
            else
                value = default(TValue);

            return bSuccess;
        }
    }
}
