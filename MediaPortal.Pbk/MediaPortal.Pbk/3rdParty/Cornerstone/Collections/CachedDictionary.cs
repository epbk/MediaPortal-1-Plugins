using System;
using System.Collections.Generic;

namespace MediaPortal.Pbk.Cornerstone.Collections
{
    /// <summary>
    /// Stores a value for a limited period of time. Once an item in the CachedDictionary has
    /// been in the collection for a specified Timeout length without access, it will be 
    /// automatically removed.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class CachedDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {

        protected Dictionary<TKey, DateTime> LastAccessed
        {
            get
            {
                if (this._LastAccessed == null)
                    this._LastAccessed = new Dictionary<TKey, DateTime>();

                return this._LastAccessed;
            }
        } private Dictionary<TKey, DateTime> _LastAccessed;

        /// <summary>
        /// Get/set the value after which items should expire
        /// </summary>
        public TimeSpan Timeout
        {
            get { return this._Ttl; }
            set
            {
                if (value == null)
                    this._Ttl = TimeSpan.Zero;
                else
                    this._Ttl = value;
            }
        } private TimeSpan _Ttl = new TimeSpan(0, 60, 0);

        /// <summary>
        /// Purge all expired items from memory. Items otherwise will not be removed
        /// until attempted access.
        /// </summary>
        public void Compact()
        {
            foreach (TKey currKey in Keys)
            {
                this.checkExpiration(currKey);
            }
        }

        // remove key / value pair if the given key exists and has expired
        private void checkExpiration(TKey key)
        {
            if (this.LastAccessed.ContainsKey(key) && DateTime.Now - this.LastAccessed[key] > this.Timeout)
            {
                this.Remove(key);
            }
        }

        #region Dictionary methods

        public virtual new void Add(TKey key, TValue value)
        {
            this.LastAccessed.Add(key, DateTime.Now);
            base.Add(key, value);
        }

        public virtual new bool Remove(TKey key)
        {
            this.LastAccessed.Remove(key);
            return base.Remove(key);
        }

        public virtual new TValue this[TKey key]
        {
            get
            {
                this.checkExpiration(key);

                if (this.LastAccessed.ContainsKey(key))
                    this.LastAccessed[key] = DateTime.Now;

                return base[key];
            }
            set
            {
                this.LastAccessed[key] = DateTime.Now;
                base[key] = value;
            }
        }

        public virtual new void Clear()
        {
            this.LastAccessed.Clear();
            base.Clear();
        }

        public new bool ContainsKey(TKey key)
        {
            this.checkExpiration(key);

            if (this.LastAccessed.ContainsKey(key))
                this.LastAccessed[key] = DateTime.Now;

            return base.ContainsKey(key);
        }

        public virtual new bool TryGetValue(TKey key, out TValue value)
        {
            this.checkExpiration(key);

            if (this.LastAccessed.ContainsKey(key))
                this.LastAccessed[key] = DateTime.Now;

            return base.TryGetValue(key, out value);
        }

        #endregion

    }
}
