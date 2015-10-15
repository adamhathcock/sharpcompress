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

        private static IEnumerable<RarFilePart> GetFileParts(IEnumerable<RarVolume> parts)
        {
            <GetFileParts>d__0 d__ = new <GetFileParts>d__0(-2);
            d__.<>3__parts = parts;
            return d__;
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


        [CompilerGenerated]
        private sealed class <GetFileParts>d__0 : IEnumerable<RarFilePart>, IEnumerable, IEnumerator<RarFilePart>, IEnumerator, IDisposable
        {
            private int <>1__state;
            private RarFilePart <>2__current;
            public IEnumerable<RarVolume> <>3__parts;
            public IEnumerator<RarVolume> <>7__wrap3;
            public IEnumerator<RarFilePart> <>7__wrap5;
            private int <>l__initialThreadId;
            public RarFilePart <fp>5__2;
            public RarVolume <rarPart>5__1;
            public IEnumerable<RarVolume> parts;

            [DebuggerHidden]
            public <GetFileParts>d__0(int <>1__state)
            {
                this.<>1__state = <>1__state;
                this.<>l__initialThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            private void <>m__Finally4()
            {
                this.<>1__state = -1;
                if (this.<>7__wrap3 != null)
                {
                    this.<>7__wrap3.Dispose();
                }
            }

            private void <>m__Finally6()
            {
                this.<>1__state = 1;
                if (this.<>7__wrap5 != null)
                {
                    this.<>7__wrap5.Dispose();
                }
            }

            private bool MoveNext()
            {
                bool flag;
                try
                {
                    int num = this.<>1__state;
                    if (num != 0)
                    {
                        if (num != 3)
                        {
                            goto Label_00D4;
                        }
                        goto Label_009B;
                    }
                    this.<>1__state = -1;
                    this.<>7__wrap3 = this.parts.GetEnumerator();
                    this.<>1__state = 1;
                    while (this.<>7__wrap3.MoveNext())
                    {
                        this.<rarPart>5__1 = this.<>7__wrap3.Current;
                        this.<>7__wrap5 = this.<rarPart>5__1.ReadFileParts().GetEnumerator();
                        this.<>1__state = 2;
                        while (this.<>7__wrap5.MoveNext())
                        {
                            this.<fp>5__2 = this.<>7__wrap5.Current;
                            this.<>2__current = this.<fp>5__2;
                            this.<>1__state = 3;
                            return true;
                        Label_009B:
                            this.<>1__state = 2;
                        }
                        this.<>m__Finally6();
                    }
                    this.<>m__Finally4();
                Label_00D4:
                    flag = false;
                }
                fault
                {
                    this.System.IDisposable.Dispose();
                }
                return flag;
            }

            [DebuggerHidden]
            IEnumerator<RarFilePart> IEnumerable<RarFilePart>.GetEnumerator()
            {
                RarArchiveEntryFactory.<GetFileParts>d__0 d__;
                if ((Thread.CurrentThread.ManagedThreadId == this.<>l__initialThreadId) && (this.<>1__state == -2))
                {
                    this.<>1__state = 0;
                    d__ = this;
                }
                else
                {
                    d__ = new RarArchiveEntryFactory.<GetFileParts>d__0(0);
                }
                d__.parts = this.<>3__parts;
                return d__;
            }

            [DebuggerHidden]
            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.System.Collections.Generic.IEnumerable<SharpCompress.Common.Rar.RarFilePart>.GetEnumerator();
            }

            [DebuggerHidden]
            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            void IDisposable.Dispose()
            {
                switch (this.<>1__state)
                {
                    case 1:
                    case 2:
                    case 3:
                        try
                        {
                            switch (this.<>1__state)
                            {
                                case 2:
                                case 3:
                                    break;

                                default:
                                    break;
                            }
                            try
                            {
                            }
                            finally
                            {
                                this.<>m__Finally6();
                            }
                        }
                        finally
                        {
                            this.<>m__Finally4();
                        }
                        break;
                }
            }

            RarFilePart IEnumerator<RarFilePart>.Current
            {
                [DebuggerHidden]
                get
                {
                    return this.<>2__current;
                }
            }

            object IEnumerator.Current
            {
                [DebuggerHidden]
                get
                {
                    return this.<>2__current;
                }
            }
        }

    }
}

