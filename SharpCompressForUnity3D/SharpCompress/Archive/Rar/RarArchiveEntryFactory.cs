namespace SharpCompress.Archive.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal static class RarArchiveEntryFactory
    {
        internal static IEnumerable<RarArchiveEntry> GetEntries(RarArchive archive, IEnumerable<RarVolume> rarParts)
        {
            foreach (IEnumerable<RarFilePart> iteratorVariable0 in GetMatchedFileParts(rarParts))
            {
                yield return new RarArchiveEntry(archive, iteratorVariable0);
            }
        }

        //private static IEnumerable<RarFilePart> GetFileParts(IEnumerable<RarVolume> parts)
        //{
        //    _GetFileParts_d__0 d__ = new _GetFileParts_d__0(-2);
        //    d__.__3__parts = parts;
        //    return d__;
        //}
        private static IEnumerable<RarFilePart> GetFileParts(IEnumerable<RarVolume> parts) {
            foreach (RarVolume rarPart in parts) {
                foreach (RarFilePart fp in rarPart.ReadFileParts()) {
                    yield return fp;
                }
            }
        }
        private static IEnumerable<IEnumerable<RarFilePart>> GetMatchedFileParts(IEnumerable<RarVolume> parts)
        {
            List<RarFilePart> iteratorVariable0 = new List<RarFilePart>();
            foreach (RarFilePart iteratorVariable1 in GetFileParts(parts))
            {
                iteratorVariable0.Add(iteratorVariable1);
                if (!FlagUtility.HasFlag((long) ((ulong) iteratorVariable1.FileHeader.FileFlags), 2L))
                {
                    yield return iteratorVariable0;
                    iteratorVariable0 = new List<RarFilePart>();
                }
            }
            if (iteratorVariable0.Count <= 0)
            {
                yield break;
            }
            yield return iteratorVariable0;
        }


        //[CompilerGenerated]
        //private sealed class _GetFileParts_d__0 : IEnumerable<RarFilePart>, IEnumerable, IEnumerator<RarFilePart>, IEnumerator, IDisposable
        //{
        //    private int __1__state;
        //    private RarFilePart __2__current;
        //    public IEnumerable<RarVolume> __3__parts;
        //    public IEnumerator<RarVolume> __7__wrap3;
        //    public IEnumerator<RarFilePart> __7__wrap5;
        //    private int __l__initialThreadId;
        //    public RarFilePart _fp_5__2;
        //    public RarVolume _rarPart_5__1;
        //    public IEnumerable<RarVolume> parts;

        //    [DebuggerHidden]
        //    public _GetFileParts_d__0(int __1__state)
        //    {
        //        this.__1__state = __1__state;
        //        this.__l__initialThreadId = Thread.CurrentThread.ManagedThreadId;
        //    }

        //    private void __m__Finally4()
        //    {
        //        this.__1__state = -1;
        //        if (this.__7__wrap3 != null)
        //        {
        //            this.__7__wrap3.Dispose();
        //        }
        //    }

        //    private void __m__Finally6()
        //    {
        //        this.__1__state = 1;
        //        if (this.__7__wrap5 != null)
        //        {
        //            this.__7__wrap5.Dispose();
        //        }
        //    }

        //    public bool MoveNext()
        //    {
        //        bool flag;
        //        try
        //        {
        //            int num = this.__1__state;
        //            if (num != 0)
        //            {
        //                if (num != 3)
        //                {
        //                    goto Label_00D4;
        //                }
        //                goto Label_009B;
        //            }
        //            this.__1__state = -1;
        //            this.__7__wrap3 = this.parts.GetEnumerator();
        //            this.__1__state = 1;
        //            while (this.__7__wrap3.MoveNext())
        //            {
        //                this._rarPart_5__1 = this.__7__wrap3.Current;
        //                this.__7__wrap5 = this._rarPart_5__1.ReadFileParts().GetEnumerator();
        //                this.__1__state = 2;
        //                while (this.__7__wrap5.MoveNext())
        //                {
        //                    this._fp_5__2 = this.__7__wrap5.Current;
        //                    this.__2__current = this._fp_5__2;
        //                    this.__1__state = 3;
        //                    return true;
        //                Label_009B:
        //                    this.__1__state = 2;
        //                }
        //                this.__m__Finally6();
        //            }
        //            this.__m__Finally4();
        //        Label_00D4:
        //            flag = false;
        //        }
        //        finally
        //        {
        //            this.System.IDisposable.Dispose();
        //        }
        //        return flag;
        //    }

        //    [DebuggerHidden]
        //    IEnumerator<RarFilePart> IEnumerable<RarFilePart>.GetEnumerator()
        //    {
        //        RarArchiveEntryFactory._GetFileParts_d__0 d__;
        //        if ((Thread.CurrentThread.ManagedThreadId == this.__l__initialThreadId) && (this.__1__state == -2))
        //        {
        //            this.__1__state = 0;
        //            d__ = this;
        //        }
        //        else
        //        {
        //            d__ = new RarArchiveEntryFactory._GetFileParts_d__0(0);
        //        }
        //        d__.parts = this.__3__parts;
        //        return d__;
        //    }

        //    [DebuggerHidden]
        //    IEnumerator IEnumerable.GetEnumerator()
        //    {
        //        return this.System.Collections.Generic.IEnumerable<SharpCompress.Common.Rar.RarFilePart>.GetEnumerator();
        //    }

        //    [DebuggerHidden]
        //    void IEnumerator.Reset()
        //    {
        //        throw new NotSupportedException();
        //    }

        //    void IDisposable.Dispose()
        //    {
        //        switch (this.__1__state)
        //        {
        //            case 1:
        //            case 2:
        //            case 3:
        //                try
        //                {
        //                    switch (this.__1__state)
        //                    {
        //                        case 2:
        //                        case 3:
        //                            break;

        //                        default:
        //                            break;
        //                    }
        //                    try
        //                    {
        //                    }
        //                    finally
        //                    {
        //                        this.__m__Finally6();
        //                    }
        //                }
        //                finally
        //                {
        //                    this.__m__Finally4();
        //                }
        //                break;
        //        }
        //    }

        //    RarFilePart IEnumerator<RarFilePart>.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }

        //    object IEnumerator.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }

           
        //}

    }
}

