#region License, Terms and Author(s)
//
// LINQBridge
// Copyright (c) 2007 Atif Aziz, Joseph Albahari. All rights reserved.
//
//  Author(s):
//
//      Atif Aziz, http://www.raboof.com
//
// This library is free software; you can redistribute it and/or modify it 
// under the terms of the New BSD License, a copy of which should have 
// been delivered along with this distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT 
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A 
// PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT 
// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
#endregion

// $Id$

namespace System.Linq
{
    #region Imports

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using IEnumerable=System.Collections.IEnumerable;
    using LinqBridge;

    #endregion

    /// <summary>
    /// Represents a collection of keys each mapped to one or more values.
    /// </summary>

    internal sealed class Lookup<TKey, TElement> : ILookup<TKey, TElement>
    {
        private readonly Dictionary<Key<TKey>, IGrouping<TKey, TElement>> _map;
        private readonly List<Key<TKey>> _orderedKeys; // remember order of insertion

        internal Lookup(IEqualityComparer<TKey> comparer)
        {
            _map = new Dictionary<Key<TKey>, IGrouping<TKey, TElement>>(new KeyComparer<TKey>(comparer));
            _orderedKeys = new List<Key<TKey>>();
        }

        internal void Add(IGrouping<TKey, TElement> item)
        {
            var key = new Key<TKey>(item.Key);
            _map.Add(key, item);
            _orderedKeys.Add(key);
        }

        internal IEnumerable<TElement> Find(TKey key)
        {
            IGrouping<TKey, TElement> grouping;
            return _map.TryGetValue(new Key<TKey>(key), out grouping) ? grouping : null;
        }

        /// <summary>
        /// Gets the number of key/value collection pairs in the <see cref="Lookup{TKey,TElement}" />.
        /// </summary>

        public int Count
        {
            get { return _map.Count; }
        }

        /// <summary>
        /// Gets the collection of values indexed by the specified key.
        /// </summary>

        public IEnumerable<TElement> this[TKey key]
        {
            get
            {
                IGrouping<TKey, TElement> result;
                return _map.TryGetValue(new Key<TKey>(key), out result) ? result : Enumerable.Empty<TElement>();
            }
        }

        /// <summary>
        /// Determines whether a specified key is in the <see cref="Lookup{TKey,TElement}" />.
        /// </summary>

        public bool Contains(TKey key)
        {
            return _map.ContainsKey(new Key<TKey>(key));
        }

        /// <summary>
        /// Applies a transform function to each key and its associated 
        /// values and returns the results.
        /// </summary>

        public IEnumerable<TResult> ApplyResultSelector<TResult>(
            Func<TKey, IEnumerable<TElement>, TResult> resultSelector)
        {
            if (resultSelector == null) 
                throw new ArgumentNullException("resultSelector");
            
            foreach (var pair in _map)
                yield return resultSelector(pair.Key.Value, pair.Value);
        }

        /// <summary>
        /// Returns a generic enumerator that iterates through the <see cref="Lookup{TKey,TElement}" />.
        /// </summary>

        public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
        {
            foreach (var key in _orderedKeys)
                yield return _map[key];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
