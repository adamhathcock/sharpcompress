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

namespace LinqBridge
{
    #region Imports

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    #endregion

    internal sealed class OrderedEnumerable<T, K> : IOrderedEnumerable<T>
    {
        private readonly IEnumerable<T> _source;
        private readonly Func<T[], IComparer<int>, IComparer<int>> _comparerComposer;

        public OrderedEnumerable(IEnumerable<T> source, 
            Func<T, K> keySelector, IComparer<K> comparer, bool descending) :
            this(source, (_, next) => next, keySelector, comparer, descending) {}

        private OrderedEnumerable(IEnumerable<T> source, 
            Func<T[], IComparer<int>, IComparer<int>> parent,
            Func<T, K> keySelector, IComparer<K> comparer, bool descending)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");
            Debug.Assert(parent != null);
            
            _source = source;
            
            comparer = comparer ?? Comparer<K>.Default;
            var direction = descending ? -1 : 1;
            
            _comparerComposer = (items, next) =>
            {
                Debug.Assert(items != null);
                Debug.Assert(next != null);

                var keys = new K[items.Length];
                for (var i = 0; i < items.Length; i++)
                    keys[i] = keySelector(items[i]);
                
                return parent(items, new DelegatingComparer<int>((i, j) =>
                {
                    var result = direction * comparer.Compare(keys[i], keys[j]);
                    return result != 0 ? result : next.Compare(i, j);
                }));
            };
        }

        public IOrderedEnumerable<T> CreateOrderedEnumerable<KK>(
            Func<T, KK> keySelector, IComparer<KK> comparer, bool descending)
        {
            return new OrderedEnumerable<T, KK>(_source, _comparerComposer, keySelector, comparer, descending);
        }

        public IEnumerator<T> GetEnumerator()
        {
            //
            // Sort using Array.Sort but docs say that it performs an 
            // unstable sort. LINQ, on the other hand, says OrderBy performs 
            // a stable sort. Use the item position then as a tie 
            // breaker when all keys compare equal, thus making the sort 
            // stable.
            //

            var items = _source.ToArray();
            var positionComparer = new DelegatingComparer<int>((i, j) => i.CompareTo(j));
            var comparer = _comparerComposer(items, positionComparer);
            var keys = new int[items.Length];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = i;
            Array.Sort(keys, items, comparer);
            return ((IEnumerable<T>) items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
