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
    using System.Collections.Generic;

    #endregion

    /// <remarks>
    /// This type is not intended to be used directly from user code.
    /// It may be removed or changed in a future version without notice.
    /// </remarks>

    sealed class DelegatingComparer<T> : IComparer<T>
    {
        private readonly Func<T, T, int> _comparer;

        public DelegatingComparer(Func<T, T, int> comparer)
        {
            if (comparer == null) throw new ArgumentNullException("comparer");
            _comparer = comparer;
        }

        public int Compare(T x, T y) { return _comparer(x, y); }
    }

    /// <remarks>
    /// This type is not intended to be used directly from user code.
    /// It may be removed or changed in a future version without notice.
    /// </remarks>

    struct Key<T>
    {
        public Key(T value) : this() { Value = value; }
        public T Value { get; private set; }
    }

    /// <remarks>
    /// This type is not intended to be used directly from user code.
    /// It may be removed or changed in a future version without notice.
    /// </remarks>

    sealed class KeyComparer<T> : IEqualityComparer<Key<T>>
    {
        private readonly IEqualityComparer<T> _innerComparer;

        public KeyComparer(IEqualityComparer<T> innerComparer)
        {
            _innerComparer = innerComparer ?? EqualityComparer<T>.Default;
        }

        public bool Equals(Key<T> x, Key<T> y)
        {
            return _innerComparer.Equals(x.Value, y.Value);
        }

        public int GetHashCode(Key<T> obj)
        {
            return obj.Value == null ? 0 : _innerComparer.GetHashCode(obj.Value);
        }
    }
}
