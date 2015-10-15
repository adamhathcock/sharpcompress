namespace SharpCompress.Compressor.Deflate
{
    using System;
    using System.Runtime.CompilerServices;

    //internal sealed class DeflateManager
    //{
    //    internal ZlibCodec _codec;
    //    internal int _distanceOffset;
    //    private static readonly string[] _ErrorMessage = new string[] { "need dictionary", "stream end", "", "file error", "stream error", "data error", "insufficient memory", "buffer error", "incompatible version", "" };
    //    private int _lengthOffset;
    //    private bool _WantRfc1950HeaderBytes = true;
    //    internal short bi_buf;
    //    internal int bi_valid;
    //    private short[] bl_count = new short[InternalConstants.MAX_BITS + 1];
    //    private short[] bl_tree = new short[((2 * InternalConstants.BL_CODES) + 1) * 2];
    //    private int blockStart;
    //    private const int Buf_size = 0x10;
    //    private const int BUSY_STATE = 0x71;
    //    private CompressionLevel compressionLevel;
    //    private CompressionStrategy compressionStrategy;
    //    private Config config;
    //    internal sbyte data_type;
    //    private CompressFunc DeflateFunction;
    //    private sbyte[] depth = new sbyte[(2 * InternalConstants.L_CODES) + 1];
    //    private short[] dyn_dtree = new short[((2 * InternalConstants.D_CODES) + 1) * 2];
    //    private short[] dyn_ltree = new short[HEAP_SIZE * 2];
    //    private const int DYN_TREES = 2;
    //    private const int END_BLOCK = 0x100;
    //    internal static readonly int[] ExtraDistanceBits = new int[] { 
    //        0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 
    //        7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13
    //     };
    //    internal static readonly int[] ExtraLengthBits = new int[] { 
    //        0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 
    //        3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0
    //     };
    //    private const int FINISH_STATE = 0x29a;
    //    private int hash_bits;
    //    private int hash_mask;
    //    private int hash_shift;
    //    private int hash_size;
    //    private short[] head;
    //    private int[] heap = new int[(2 * InternalConstants.L_CODES) + 1];
    //    private int heap_len;
    //    private int heap_max;
    //    private static readonly int HEAP_SIZE = ((2 * InternalConstants.L_CODES) + 1);
    //    private const int INIT_STATE = 0x2a;
    //    private int ins_h;
    //    internal int last_eob_len;
    //    internal int last_flush;
    //    internal int last_lit;
    //    internal int lit_bufsize;
    //    private int lookahead;
    //    private int match_available;
    //    private int match_length;
    //    private int match_start;
    //    internal int matches;
    //    private const int MAX_MATCH = 0x102;
    //    private const int MEM_LEVEL_DEFAULT = 8;
    //    private const int MEM_LEVEL_MAX = 9;
    //    private const int MIN_LOOKAHEAD = 0x106;
    //    private const int MIN_MATCH = 3;
    //    internal int nextPending;
    //    internal int opt_len;
    //    internal byte[] pending;
    //    internal int pendingCount;
    //    private const int PRESET_DICT = 0x20;
    //    internal short[] prev;
    //    private int prev_length;
    //    private int prev_match;
    //    private bool Rfc1950BytesEmitted = false;
    //    internal int static_len;
    //    private const int STATIC_TREES = 1;
    //    internal int status;
    //    private const int STORED_BLOCK = 0;
    //    private int strstart;
    //    private Tree treeBitLengths = new Tree();
    //    private Tree treeDistances = new Tree();
    //    private Tree treeLiterals = new Tree();
    //    internal int w_bits;
    //    internal int w_mask;
    //    internal int w_size;
    //    internal byte[] window;
    //    internal int window_size;
    //    private const int Z_ASCII = 1;
    //    private const int Z_BINARY = 0;
    //    private const int Z_DEFLATED = 8;
    //    private const int Z_UNKNOWN = 2;

    //    internal DeflateManager()
    //    {
    //    }

    //    private void _fillWindow()
    //    {
    //        int num;
    //        int num4;
    //    Label_0001:
    //        num4 = (this.window_size - this.lookahead) - this.strstart;
    //        if (((num4 == 0) && (this.strstart == 0)) && (this.lookahead == 0))
    //        {
    //            num4 = this.w_size;
    //        }
    //        else if (num4 == -1)
    //        {
    //            num4--;
    //        }
    //        else if (this.strstart >= ((this.w_size + this.w_size) - 0x106))
    //        {
    //            int num2;
    //            Array.Copy(this.window, this.w_size, this.window, 0, this.w_size);
    //            this.match_start -= this.w_size;
    //            this.strstart -= this.w_size;
    //            this.blockStart -= this.w_size;
    //            num = this.hash_size;
    //            int index = num;
    //            do
    //            {
    //                num2 = this.head[--index] & 0xffff;
    //                this.head[index] = (num2 >= this.w_size) ? ((short) (num2 - this.w_size)) : ((short) 0);
    //            }
    //            while (--num != 0);
    //            num = this.w_size;
    //            index = num;
    //            do
    //            {
    //                num2 = this.prev[--index] & 0xffff;
    //                this.prev[index] = (num2 >= this.w_size) ? ((short) (num2 - this.w_size)) : ((short) 0);
    //            }
    //            while (--num != 0);
    //            num4 += this.w_size;
    //        }
    //        if (this._codec.AvailableBytesIn != 0)
    //        {
    //            num = this._codec.read_buf(this.window, this.strstart + this.lookahead, num4);
    //            this.lookahead += num;
    //            if (this.lookahead >= 3)
    //            {
    //                this.ins_h = this.window[this.strstart] & 0xff;
    //                this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 1] & 0xff)) & this.hash_mask;
    //            }
    //            if ((this.lookahead < 0x106) && (this._codec.AvailableBytesIn != 0))
    //            {
    //                goto Label_0001;
    //            }
    //        }
    //    }

    //    internal void _InitializeBlocks()
    //    {
    //        int num;
    //        for (num = 0; num < InternalConstants.L_CODES; num++)
    //        {
    //            this.dyn_ltree[num * 2] = 0;
    //        }
    //        for (num = 0; num < InternalConstants.D_CODES; num++)
    //        {
    //            this.dyn_dtree[num * 2] = 0;
    //        }
    //        for (num = 0; num < InternalConstants.BL_CODES; num++)
    //        {
    //            this.bl_tree[num * 2] = 0;
    //        }
    //        this.dyn_ltree[0x200] = 1;
    //        this.opt_len = this.static_len = 0;
    //        this.last_lit = this.matches = 0;
    //    }

    //    private void _InitializeLazyMatch()
    //    {
    //        this.window_size = 2 * this.w_size;
    //        Array.Clear(this.head, 0, this.hash_size);
    //        this.config = Config.Lookup(this.compressionLevel);
    //        this.SetDeflater();
    //        this.strstart = 0;
    //        this.blockStart = 0;
    //        this.lookahead = 0;
    //        this.match_length = this.prev_length = 2;
    //        this.match_available = 0;
    //        this.ins_h = 0;
    //    }

    //    private void _InitializeTreeData()
    //    {
    //        this.treeLiterals.dyn_tree = this.dyn_ltree;
    //        this.treeLiterals.staticTree = StaticTree.Literals;
    //        this.treeDistances.dyn_tree = this.dyn_dtree;
    //        this.treeDistances.staticTree = StaticTree.Distances;
    //        this.treeBitLengths.dyn_tree = this.bl_tree;
    //        this.treeBitLengths.staticTree = StaticTree.BitLengths;
    //        this.bi_buf = 0;
    //        this.bi_valid = 0;
    //        this.last_eob_len = 8;
    //        this._InitializeBlocks();
    //    }

    //    internal void _tr_align()
    //    {
    //        this.send_bits(2, 3);
    //        this.send_code(0x100, StaticTree.lengthAndLiteralsTreeCodes);
    //        this.bi_flush();
    //        if ((((1 + this.last_eob_len) + 10) - this.bi_valid) < 9)
    //        {
    //            this.send_bits(2, 3);
    //            this.send_code(0x100, StaticTree.lengthAndLiteralsTreeCodes);
    //            this.bi_flush();
    //        }
    //        this.last_eob_len = 7;
    //    }

    //    internal void _tr_flush_block(int buf, int stored_len, bool eof)
    //    {
    //        int num;
    //        int num2;
    //        int num3 = 0;
    //        if (this.compressionLevel > CompressionLevel.None)
    //        {
    //            if (this.data_type == 2)
    //            {
    //                this.set_data_type();
    //            }
    //            this.treeLiterals.build_tree(this);
    //            this.treeDistances.build_tree(this);
    //            num3 = this.BuildBlTree();
    //            num = ((this.opt_len + 3) + 7) >> 3;
    //            num2 = ((this.static_len + 3) + 7) >> 3;
    //            if (num2 <= num)
    //            {
    //                num = num2;
    //            }
    //        }
    //        else
    //        {
    //            num = num2 = stored_len + 5;
    //        }
    //        if (((stored_len + 4) <= num) && (buf != -1))
    //        {
    //            this._tr_stored_block(buf, stored_len, eof);
    //        }
    //        else if (num2 == num)
    //        {
    //            this.send_bits(2 + (eof ? 1 : 0), 3);
    //            this.send_compressed_block(StaticTree.lengthAndLiteralsTreeCodes, StaticTree.distTreeCodes);
    //        }
    //        else
    //        {
    //            this.send_bits(4 + (eof ? 1 : 0), 3);
    //            this.send_all_trees(this.treeLiterals.max_code + 1, this.treeDistances.max_code + 1, num3 + 1);
    //            this.send_compressed_block(this.dyn_ltree, this.dyn_dtree);
    //        }
    //        this._InitializeBlocks();
    //        if (eof)
    //        {
    //            this.bi_windup();
    //        }
    //    }

    //    internal void _tr_stored_block(int buf, int stored_len, bool eof)
    //    {
    //        this.send_bits(eof ? 1 : 0, 3);
    //        this.copy_block(buf, stored_len, true);
    //    }

    //    internal bool _tr_tally(int dist, int lc)
    //    {
    //        this.pending[this._distanceOffset + (this.last_lit * 2)] = (byte) (dist >> 8);
    //        this.pending[(this._distanceOffset + (this.last_lit * 2)) + 1] = (byte) dist;
    //        this.pending[this._lengthOffset + this.last_lit] = (byte) lc;
    //        this.last_lit++;
    //        if (dist == 0)
    //        {
    //            this.dyn_ltree[lc * 2] = (short) (this.dyn_ltree[lc * 2] + 1);
    //        }
    //        else
    //        {
    //            this.matches++;
    //            dist--;
    //            this.dyn_ltree[((Tree.LengthCode[lc] + InternalConstants.LITERALS) + 1) * 2] = (short) (this.dyn_ltree[((Tree.LengthCode[lc] + InternalConstants.LITERALS) + 1) * 2] + 1);
    //            this.dyn_dtree[Tree.DistanceCode(dist) * 2] = (short) (this.dyn_dtree[Tree.DistanceCode(dist) * 2] + 1);
    //        }
    //        if (((this.last_lit & 0x1fff) == 0) && (this.compressionLevel > CompressionLevel.Level2))
    //        {
    //            int num = this.last_lit << 3;
    //            int num2 = this.strstart - this.blockStart;
    //            for (int i = 0; i < InternalConstants.D_CODES; i++)
    //            {
    //                num += (int) (this.dyn_dtree[i * 2] * (5L + ExtraDistanceBits[i]));
    //            }
    //            num = num >> 3;
    //            if ((this.matches < (this.last_lit / 2)) && (num < (num2 / 2)))
    //            {
    //                return true;
    //            }
    //        }
    //        return ((this.last_lit == (this.lit_bufsize - 1)) || (this.last_lit == this.lit_bufsize));
    //    }

    //    internal void bi_flush()
    //    {
    //        if (this.bi_valid == 0x10)
    //        {
    //            this.pending[this.pendingCount++] = (byte) this.bi_buf;
    //            this.pending[this.pendingCount++] = (byte) (this.bi_buf >> 8);
    //            this.bi_buf = 0;
    //            this.bi_valid = 0;
    //        }
    //        else if (this.bi_valid >= 8)
    //        {
    //            this.pending[this.pendingCount++] = (byte) this.bi_buf;
    //            this.bi_buf = (short) (this.bi_buf >> 8);
    //            this.bi_valid -= 8;
    //        }
    //    }

    //    internal void bi_windup()
    //    {
    //        if (this.bi_valid > 8)
    //        {
    //            this.pending[this.pendingCount++] = (byte) this.bi_buf;
    //            this.pending[this.pendingCount++] = (byte) (this.bi_buf >> 8);
    //        }
    //        else if (this.bi_valid > 0)
    //        {
    //            this.pending[this.pendingCount++] = (byte) this.bi_buf;
    //        }
    //        this.bi_buf = 0;
    //        this.bi_valid = 0;
    //    }

    //    internal int BuildBlTree()
    //    {
    //        this.ScanTree(this.dyn_ltree, this.treeLiterals.max_code);
    //        this.ScanTree(this.dyn_dtree, this.treeDistances.max_code);
    //        this.treeBitLengths.build_tree(this);
    //        int index = InternalConstants.BL_CODES - 1;
    //        while (index >= 3)
    //        {
    //            if (this.bl_tree[(Tree.bl_order[index] * 2) + 1] != 0)
    //            {
    //                break;
    //            }
    //            index--;
    //        }
    //        this.opt_len += (((3 * (index + 1)) + 5) + 5) + 4;
    //        return index;
    //    }

    //    internal void copy_block(int buf, int len, bool header)
    //    {
    //        this.bi_windup();
    //        this.last_eob_len = 8;
    //        if (header)
    //        {
    //            this.pending[this.pendingCount++] = (byte) len;
    //            this.pending[this.pendingCount++] = (byte) (len >> 8);
    //            this.pending[this.pendingCount++] = (byte) ~len;
    //            this.pending[this.pendingCount++] = (byte) (~len >> 8);
    //        }
    //        this.put_bytes(this.window, buf, len);
    //    }

    //    internal int Deflate(FlushType flush)
    //    {
    //        if (((this._codec.OutputBuffer == null) || ((this._codec.InputBuffer == null) && (this._codec.AvailableBytesIn != 0))) || ((this.status == 0x29a) && (flush != FlushType.Finish)))
    //        {
    //            this._codec.Message = _ErrorMessage[4];
    //            throw new ZlibException(string.Format("Something is fishy. [{0}]", this._codec.Message));
    //        }
    //        if (this._codec.AvailableBytesOut == 0)
    //        {
    //            this._codec.Message = _ErrorMessage[7];
    //            throw new ZlibException("OutputBuffer is full (AvailableBytesOut == 0)");
    //        }
    //        int num = this.last_flush;
    //        this.last_flush = (int) flush;
    //        if (this.status == 0x2a)
    //        {
    //            int num2 = (8 + ((this.w_bits - 8) << 4)) << 8;
    //            int num3 = ((int) ((this.compressionLevel - 1) & 0xff)) >> 1;
    //            if (num3 > 3)
    //            {
    //                num3 = 3;
    //            }
    //            num2 |= num3 << 6;
    //            if (this.strstart != 0)
    //            {
    //                num2 |= 0x20;
    //            }
    //            num2 += 0x1f - (num2 % 0x1f);
    //            this.status = 0x71;
    //            this.pending[this.pendingCount++] = (byte) (num2 >> 8);
    //            this.pending[this.pendingCount++] = (byte) num2;
    //            if (this.strstart != 0)
    //            {
    //                this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & -16777216) >> 0x18);
    //                this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & 0xff0000) >> 0x10);
    //                this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & 0xff00) >> 8);
    //                this.pending[this.pendingCount++] = (byte) (this._codec._Adler32 & 0xff);
    //            }
    //            this._codec._Adler32 = Adler.Adler32(0, null, 0, 0);
    //        }
    //        if (this.pendingCount != 0)
    //        {
    //            this._codec.flush_pending();
    //            if (this._codec.AvailableBytesOut == 0)
    //            {
    //                this.last_flush = -1;
    //                return 0;
    //            }
    //        }
    //        else if (((this._codec.AvailableBytesIn == 0) && (flush <= num)) && (flush != FlushType.Finish))
    //        {
    //            return 0;
    //        }
    //        if ((this.status == 0x29a) && (this._codec.AvailableBytesIn != 0))
    //        {
    //            this._codec.Message = _ErrorMessage[7];
    //            throw new ZlibException("status == FINISH_STATE && _codec.AvailableBytesIn != 0");
    //        }
    //        if (((this._codec.AvailableBytesIn != 0) || (this.lookahead != 0)) || ((flush != FlushType.None) && (this.status != 0x29a)))
    //        {
    //            BlockState state = this.DeflateFunction(flush);
    //            switch (state)
    //            {
    //                case BlockState.FinishStarted:
    //                case BlockState.FinishDone:
    //                    this.status = 0x29a;
    //                    break;
    //            }
    //            if ((state == BlockState.NeedMore) || (state == BlockState.FinishStarted))
    //            {
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    this.last_flush = -1;
    //                }
    //                return 0;
    //            }
    //            if (state == BlockState.BlockDone)
    //            {
    //                if (flush == FlushType.Partial)
    //                {
    //                    this._tr_align();
    //                }
    //                else
    //                {
    //                    this._tr_stored_block(0, 0, false);
    //                    if (flush == FlushType.Full)
    //                    {
    //                        for (int i = 0; i < this.hash_size; i++)
    //                        {
    //                            this.head[i] = 0;
    //                        }
    //                    }
    //                }
    //                this._codec.flush_pending();
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    this.last_flush = -1;
    //                    return 0;
    //                }
    //            }
    //        }
    //        if (flush != FlushType.Finish)
    //        {
    //            return 0;
    //        }
    //        if (!(this.WantRfc1950HeaderBytes && !this.Rfc1950BytesEmitted))
    //        {
    //            return 1;
    //        }
    //        this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & -16777216) >> 0x18);
    //        this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & 0xff0000) >> 0x10);
    //        this.pending[this.pendingCount++] = (byte) ((this._codec._Adler32 & 0xff00) >> 8);
    //        this.pending[this.pendingCount++] = (byte) (this._codec._Adler32 & 0xff);
    //        this._codec.flush_pending();
    //        this.Rfc1950BytesEmitted = true;
    //        return ((this.pendingCount != 0) ? 0 : 1);
    //    }

    //    internal BlockState DeflateFast(FlushType flush)
    //    {
    //        int num = 0;
    //        while (true)
    //        {
    //            bool flag;
    //            if (this.lookahead < 0x106)
    //            {
    //                this._fillWindow();
    //                if ((this.lookahead < 0x106) && (flush == FlushType.None))
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //                if (this.lookahead == 0)
    //                {
    //                    this.flush_block_only(flush == FlushType.Finish);
    //                    if (this._codec.AvailableBytesOut == 0)
    //                    {
    //                        if (flush == FlushType.Finish)
    //                        {
    //                            return BlockState.FinishStarted;
    //                        }
    //                        return BlockState.NeedMore;
    //                    }
    //                    return ((flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone);
    //                }
    //            }
    //            if (this.lookahead >= 3)
    //            {
    //                this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 2] & 0xff)) & this.hash_mask;
    //                num = this.head[this.ins_h] & 0xffff;
    //                this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
    //                this.head[this.ins_h] = (short) this.strstart;
    //            }
    //            if (((num != 0L) && (((this.strstart - num) & 0xffff) <= (this.w_size - 0x106))) && (this.compressionStrategy != CompressionStrategy.HuffmanOnly))
    //            {
    //                this.match_length = this.longest_match(num);
    //            }
    //            if (this.match_length >= 3)
    //            {
    //                flag = this._tr_tally(this.strstart - this.match_start, this.match_length - 3);
    //                this.lookahead -= this.match_length;
    //                if ((this.match_length <= this.config.MaxLazy) && (this.lookahead >= 3))
    //                {
    //                    this.match_length--;
    //                    do
    //                    {
    //                        this.strstart++;
    //                        this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 2] & 0xff)) & this.hash_mask;
    //                        num = this.head[this.ins_h] & 0xffff;
    //                        this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
    //                        this.head[this.ins_h] = (short) this.strstart;
    //                    }
    //                    while (--this.match_length != 0);
    //                    this.strstart++;
    //                }
    //                else
    //                {
    //                    this.strstart += this.match_length;
    //                    this.match_length = 0;
    //                    this.ins_h = this.window[this.strstart] & 0xff;
    //                    this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 1] & 0xff)) & this.hash_mask;
    //                }
    //            }
    //            else
    //            {
    //                flag = this._tr_tally(0, this.window[this.strstart] & 0xff);
    //                this.lookahead--;
    //                this.strstart++;
    //            }
    //            if (flag)
    //            {
    //                this.flush_block_only(false);
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //            }
    //        }
    //    }

    //    internal BlockState DeflateNone(FlushType flush)
    //    {
    //        int num = 0xffff;
    //        if (num > (this.pending.Length - 5))
    //        {
    //            num = this.pending.Length - 5;
    //        }
    //        while (true)
    //        {
    //            if (this.lookahead <= 1)
    //            {
    //                this._fillWindow();
    //                if ((this.lookahead == 0) && (flush == FlushType.None))
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //                if (this.lookahead == 0)
    //                {
    //                    this.flush_block_only(flush == FlushType.Finish);
    //                    if (this._codec.AvailableBytesOut == 0)
    //                    {
    //                        return ((flush == FlushType.Finish) ? BlockState.FinishStarted : BlockState.NeedMore);
    //                    }
    //                    return ((flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone);
    //                }
    //            }
    //            this.strstart += this.lookahead;
    //            this.lookahead = 0;
    //            int num2 = this.blockStart + num;
    //            if ((this.strstart == 0) || (this.strstart >= num2))
    //            {
    //                this.lookahead = this.strstart - num2;
    //                this.strstart = num2;
    //                this.flush_block_only(false);
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //            }
    //            if ((this.strstart - this.blockStart) >= (this.w_size - 0x106))
    //            {
    //                this.flush_block_only(false);
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //            }
    //        }
    //    }

    //    internal BlockState DeflateSlow(FlushType flush)
    //    {
    //        int num = 0;
    //        while (true)
    //        {
    //            bool flag;
    //            if (this.lookahead < 0x106)
    //            {
    //                this._fillWindow();
    //                if ((this.lookahead < 0x106) && (flush == FlushType.None))
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //                if (this.lookahead == 0)
    //                {
    //                    if (this.match_available != 0)
    //                    {
    //                        flag = this._tr_tally(0, this.window[this.strstart - 1] & 0xff);
    //                        this.match_available = 0;
    //                    }
    //                    this.flush_block_only(flush == FlushType.Finish);
    //                    if (this._codec.AvailableBytesOut == 0)
    //                    {
    //                        if (flush == FlushType.Finish)
    //                        {
    //                            return BlockState.FinishStarted;
    //                        }
    //                        return BlockState.NeedMore;
    //                    }
    //                    return ((flush == FlushType.Finish) ? BlockState.FinishDone : BlockState.BlockDone);
    //                }
    //            }
    //            if (this.lookahead >= 3)
    //            {
    //                this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 2] & 0xff)) & this.hash_mask;
    //                num = this.head[this.ins_h] & 0xffff;
    //                this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
    //                this.head[this.ins_h] = (short) this.strstart;
    //            }
    //            this.prev_length = this.match_length;
    //            this.prev_match = this.match_start;
    //            this.match_length = 2;
    //            if (((num != 0) && (this.prev_length < this.config.MaxLazy)) && (((this.strstart - num) & 0xffff) <= (this.w_size - 0x106)))
    //            {
    //                if (this.compressionStrategy != CompressionStrategy.HuffmanOnly)
    //                {
    //                    this.match_length = this.longest_match(num);
    //                }
    //                if ((this.match_length <= 5) && ((this.compressionStrategy == CompressionStrategy.Filtered) || ((this.match_length == 3) && ((this.strstart - this.match_start) > 0x1000))))
    //                {
    //                    this.match_length = 2;
    //                }
    //            }
    //            if ((this.prev_length >= 3) && (this.match_length <= this.prev_length))
    //            {
    //                int num2 = (this.strstart + this.lookahead) - 3;
    //                flag = this._tr_tally((this.strstart - 1) - this.prev_match, this.prev_length - 3);
    //                this.lookahead -= this.prev_length - 1;
    //                this.prev_length -= 2;
    //                do
    //                {
    //                    if (++this.strstart <= num2)
    //                    {
    //                        this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[this.strstart + 2] & 0xff)) & this.hash_mask;
    //                        num = this.head[this.ins_h] & 0xffff;
    //                        this.prev[this.strstart & this.w_mask] = this.head[this.ins_h];
    //                        this.head[this.ins_h] = (short) this.strstart;
    //                    }
    //                }
    //                while (--this.prev_length != 0);
    //                this.match_available = 0;
    //                this.match_length = 2;
    //                this.strstart++;
    //                if (flag)
    //                {
    //                    this.flush_block_only(false);
    //                    if (this._codec.AvailableBytesOut == 0)
    //                    {
    //                        return BlockState.NeedMore;
    //                    }
    //                }
    //            }
    //            else if (this.match_available != 0)
    //            {
    //                if (this._tr_tally(0, this.window[this.strstart - 1] & 0xff))
    //                {
    //                    this.flush_block_only(false);
    //                }
    //                this.strstart++;
    //                this.lookahead--;
    //                if (this._codec.AvailableBytesOut == 0)
    //                {
    //                    return BlockState.NeedMore;
    //                }
    //            }
    //            else
    //            {
    //                this.match_available = 1;
    //                this.strstart++;
    //                this.lookahead--;
    //            }
    //        }
    //    }

    //    internal int End()
    //    {
    //        if (((this.status != 0x2a) && (this.status != 0x71)) && (this.status != 0x29a))
    //        {
    //            return -2;
    //        }
    //        this.pending = null;
    //        this.head = null;
    //        this.prev = null;
    //        this.window = null;
    //        return ((this.status == 0x71) ? -3 : 0);
    //    }

    //    internal void flush_block_only(bool eof)
    //    {
    //        this._tr_flush_block((this.blockStart >= 0) ? this.blockStart : -1, this.strstart - this.blockStart, eof);
    //        this.blockStart = this.strstart;
    //        this._codec.flush_pending();
    //    }

    //    internal int Initialize(ZlibCodec codec, CompressionLevel level)
    //    {
    //        return this.Initialize(codec, level, 15);
    //    }

    //    internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits)
    //    {
    //        return this.Initialize(codec, level, bits, 8, CompressionStrategy.Default);
    //    }

    //    internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits, CompressionStrategy compressionStrategy)
    //    {
    //        return this.Initialize(codec, level, bits, 8, compressionStrategy);
    //    }

    //    internal int Initialize(ZlibCodec codec, CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
    //    {
    //        this._codec = codec;
    //        this._codec.Message = null;
    //        if ((windowBits < 9) || (windowBits > 15))
    //        {
    //            throw new ZlibException("windowBits must be in the range 9..15.");
    //        }
    //        if ((memLevel < 1) || (memLevel > 9))
    //        {
    //            throw new ZlibException(string.Format("memLevel must be in the range 1.. {0}", 9));
    //        }
    //        this._codec.dstate = this;
    //        this.w_bits = windowBits;
    //        this.w_size = ((int) 1) << this.w_bits;
    //        this.w_mask = this.w_size - 1;
    //        this.hash_bits = memLevel + 7;
    //        this.hash_size = ((int) 1) << this.hash_bits;
    //        this.hash_mask = this.hash_size - 1;
    //        this.hash_shift = ((this.hash_bits + 3) - 1) / 3;
    //        this.window = new byte[this.w_size * 2];
    //        this.prev = new short[this.w_size];
    //        this.head = new short[this.hash_size];
    //        this.lit_bufsize = ((int) 1) << (memLevel + 6);
    //        this.pending = new byte[this.lit_bufsize * 4];
    //        this._distanceOffset = this.lit_bufsize;
    //        this._lengthOffset = 3 * this.lit_bufsize;
    //        this.compressionLevel = level;
    //        this.compressionStrategy = strategy;
    //        this.Reset();
    //        return 0;
    //    }

    //    internal static bool IsSmaller(short[] tree, int n, int m, sbyte[] depth)
    //    {
    //        short num = tree[n * 2];
    //        short num2 = tree[m * 2];
    //        return ((num < num2) || ((num == num2) && (depth[n] <= depth[m])));
    //    }

    //    internal int longest_match(int cur_match)
    //    {
    //        int maxChainLength = this.config.MaxChainLength;
    //        int strstart = this.strstart;
    //        int num5 = this.prev_length;
    //        int num6 = (this.strstart > (this.w_size - 0x106)) ? (this.strstart - (this.w_size - 0x106)) : 0;
    //        int niceLength = this.config.NiceLength;
    //        int num8 = this.w_mask;
    //        int num9 = this.strstart + 0x102;
    //        byte num10 = this.window[(strstart + num5) - 1];
    //        byte num11 = this.window[strstart + num5];
    //        if (this.prev_length >= this.config.GoodLength)
    //        {
    //            maxChainLength = maxChainLength >> 2;
    //        }
    //        if (niceLength > this.lookahead)
    //        {
    //            niceLength = this.lookahead;
    //        }
    //        do
    //        {
    //            int index = cur_match;
    //            if ((((this.window[index + num5] == num11) && (this.window[(index + num5) - 1] == num10)) && (this.window[index] == this.window[strstart])) && (this.window[++index] == this.window[strstart + 1]))
    //            {
    //                strstart += 2;
    //                index++;
    //                while (((((this.window[++strstart] == this.window[++index]) && (this.window[++strstart] == this.window[++index])) && ((this.window[++strstart] == this.window[++index]) && (this.window[++strstart] == this.window[++index]))) && (((this.window[++strstart] == this.window[++index]) && (this.window[++strstart] == this.window[++index])) && ((this.window[++strstart] == this.window[++index]) && (this.window[++strstart] == this.window[++index])))) && (strstart < num9))
    //                {
    //                }
    //                int num4 = 0x102 - (num9 - strstart);
    //                strstart = num9 - 0x102;
    //                if (num4 > num5)
    //                {
    //                    this.match_start = cur_match;
    //                    num5 = num4;
    //                    if (num4 >= niceLength)
    //                    {
    //                        break;
    //                    }
    //                    num10 = this.window[(strstart + num5) - 1];
    //                    num11 = this.window[strstart + num5];
    //                }
    //            }
    //        }
    //        while (((cur_match = this.prev[cur_match & num8] & 0xffff) > num6) && (--maxChainLength != 0));
    //        if (num5 <= this.lookahead)
    //        {
    //            return num5;
    //        }
    //        return this.lookahead;
    //    }

    //    internal void pqdownheap(short[] tree, int k)
    //    {
    //        int n = this.heap[k];
    //        for (int i = k << 1; i <= this.heap_len; i = i << 1)
    //        {
    //            if ((i < this.heap_len) && IsSmaller(tree, this.heap[i + 1], this.heap[i], this.depth))
    //            {
    //                i++;
    //            }
    //            if (IsSmaller(tree, n, this.heap[i], this.depth))
    //            {
    //                break;
    //            }
    //            this.heap[k] = this.heap[i];
    //            k = i;
    //        }
    //        this.heap[k] = n;
    //    }

    //    private void put_bytes(byte[] p, int start, int len)
    //    {
    //        Array.Copy(p, start, this.pending, this.pendingCount, len);
    //        this.pendingCount += len;
    //    }

    //    internal void Reset()
    //    {
    //        this._codec.TotalBytesIn = this._codec.TotalBytesOut = 0L;
    //        this._codec.Message = null;
    //        this.pendingCount = 0;
    //        this.nextPending = 0;
    //        this.Rfc1950BytesEmitted = false;
    //        this.status = this.WantRfc1950HeaderBytes ? 0x2a : 0x71;
    //        this._codec._Adler32 = Adler.Adler32(0, null, 0, 0);
    //        this.last_flush = 0;
    //        this._InitializeTreeData();
    //        this._InitializeLazyMatch();
    //    }

    //    internal void ScanTree(short[] tree, int maxCode)
    //    {
    //        int num2 = -1;
    //        int num4 = tree[1];
    //        int num5 = 0;
    //        int num6 = 7;
    //        int num7 = 4;
    //        if (num4 == 0)
    //        {
    //            num6 = 0x8a;
    //            num7 = 3;
    //        }
    //        tree[((maxCode + 1) * 2) + 1] = 0x7fff;
    //        for (int i = 0; i <= maxCode; i++)
    //        {
    //            int num3 = num4;
    //            num4 = tree[((i + 1) * 2) + 1];
    //            if ((++num5 >= num6) || (num3 != num4))
    //            {
    //                if (num5 < num7)
    //                {
    //                    this.bl_tree[num3 * 2] = (short) (this.bl_tree[num3 * 2] + num5);
    //                }
    //                else if (num3 != 0)
    //                {
    //                    if (num3 != num2)
    //                    {
    //                        this.bl_tree[num3 * 2] = (short) (this.bl_tree[num3 * 2] + 1);
    //                    }
    //                    this.bl_tree[InternalConstants.REP_3_6 * 2] = (short) (this.bl_tree[InternalConstants.REP_3_6 * 2] + 1);
    //                }
    //                else if (num5 <= 10)
    //                {
    //                    this.bl_tree[InternalConstants.REPZ_3_10 * 2] = (short) (this.bl_tree[InternalConstants.REPZ_3_10 * 2] + 1);
    //                }
    //                else
    //                {
    //                    this.bl_tree[InternalConstants.REPZ_11_138 * 2] = (short) (this.bl_tree[InternalConstants.REPZ_11_138 * 2] + 1);
    //                }
    //                num5 = 0;
    //                num2 = num3;
    //                if (num4 == 0)
    //                {
    //                    num6 = 0x8a;
    //                    num7 = 3;
    //                }
    //                else if (num3 == num4)
    //                {
    //                    num6 = 6;
    //                    num7 = 3;
    //                }
    //                else
    //                {
    //                    num6 = 7;
    //                    num7 = 4;
    //                }
    //            }
    //        }
    //    }

    //    internal void send_all_trees(int lcodes, int dcodes, int blcodes)
    //    {
    //        this.send_bits(lcodes - 0x101, 5);
    //        this.send_bits(dcodes - 1, 5);
    //        this.send_bits(blcodes - 4, 4);
    //        for (int i = 0; i < blcodes; i++)
    //        {
    //            this.send_bits(this.bl_tree[(Tree.bl_order[i] * 2) + 1], 3);
    //        }
    //        this.send_tree(this.dyn_ltree, lcodes - 1);
    //        this.send_tree(this.dyn_dtree, dcodes - 1);
    //    }

    //    internal void send_bits(int value, int length)
    //    {
    //        int num = length;
    //        if (this.bi_valid > (0x10 - num))
    //        {
    //            this.bi_buf = (short) (this.bi_buf | ((short) ((value << this.bi_valid) & 0xffff)));
    //            this.pending[this.pendingCount++] = (byte) this.bi_buf;
    //            this.pending[this.pendingCount++] = (byte) (this.bi_buf >> 8);
    //            this.bi_buf = (short) (value >> (0x10 - this.bi_valid));
    //            this.bi_valid += num - 0x10;
    //        }
    //        else
    //        {
    //            this.bi_buf = (short) (this.bi_buf | ((short) ((value << this.bi_valid) & 0xffff)));
    //            this.bi_valid += num;
    //        }
    //    }

    //    internal void send_code(int c, short[] tree)
    //    {
    //        int index = c * 2;
    //        this.send_bits(tree[index] & 0xffff, tree[index + 1] & 0xffff);
    //    }

    //    internal void send_compressed_block(short[] ltree, short[] dtree)
    //    {
    //        int num3 = 0;
    //        if (this.last_lit != 0)
    //        {
    //            do
    //            {
    //                int index = this._distanceOffset + (num3 * 2);
    //                int dist = ((this.pending[index] << 8) & 0xff00) | (this.pending[index + 1] & 0xff);
    //                int c = this.pending[this._lengthOffset + num3] & 0xff;
    //                num3++;
    //                if (dist == 0)
    //                {
    //                    this.send_code(c, ltree);
    //                }
    //                else
    //                {
    //                    int num4 = Tree.LengthCode[c];
    //                    this.send_code((num4 + InternalConstants.LITERALS) + 1, ltree);
    //                    int length = ExtraLengthBits[num4];
    //                    if (length != 0)
    //                    {
    //                        c -= Tree.LengthBase[num4];
    //                        this.send_bits(c, length);
    //                    }
    //                    dist--;
    //                    num4 = Tree.DistanceCode(dist);
    //                    this.send_code(num4, dtree);
    //                    length = ExtraDistanceBits[num4];
    //                    if (length != 0)
    //                    {
    //                        dist -= Tree.DistanceBase[num4];
    //                        this.send_bits(dist, length);
    //                    }
    //                }
    //            }
    //            while (num3 < this.last_lit);
    //        }
    //        this.send_code(0x100, ltree);
    //        this.last_eob_len = ltree[0x201];
    //    }

    //    internal void send_tree(short[] tree, int max_code)
    //    {
    //        int num2 = -1;
    //        int num4 = tree[1];
    //        int num5 = 0;
    //        int num6 = 7;
    //        int num7 = 4;
    //        if (num4 == 0)
    //        {
    //            num6 = 0x8a;
    //            num7 = 3;
    //        }
    //        for (int i = 0; i <= max_code; i++)
    //        {
    //            int c = num4;
    //            num4 = tree[((i + 1) * 2) + 1];
    //            if ((++num5 >= num6) || (c != num4))
    //            {
    //                if (num5 < num7)
    //                {
    //                    do
    //                    {
    //                        this.send_code(c, this.bl_tree);
    //                    }
    //                    while (--num5 != 0);
    //                }
    //                else if (c != 0)
    //                {
    //                    if (c != num2)
    //                    {
    //                        this.send_code(c, this.bl_tree);
    //                        num5--;
    //                    }
    //                    this.send_code(InternalConstants.REP_3_6, this.bl_tree);
    //                    this.send_bits(num5 - 3, 2);
    //                }
    //                else if (num5 <= 10)
    //                {
    //                    this.send_code(InternalConstants.REPZ_3_10, this.bl_tree);
    //                    this.send_bits(num5 - 3, 3);
    //                }
    //                else
    //                {
    //                    this.send_code(InternalConstants.REPZ_11_138, this.bl_tree);
    //                    this.send_bits(num5 - 11, 7);
    //                }
    //                num5 = 0;
    //                num2 = c;
    //                if (num4 == 0)
    //                {
    //                    num6 = 0x8a;
    //                    num7 = 3;
    //                }
    //                else if (c == num4)
    //                {
    //                    num6 = 6;
    //                    num7 = 3;
    //                }
    //                else
    //                {
    //                    num6 = 7;
    //                    num7 = 4;
    //                }
    //            }
    //        }
    //    }

    //    internal void set_data_type()
    //    {
    //        int num = 0;
    //        int num2 = 0;
    //        int num3 = 0;
    //        while (num < 7)
    //        {
    //            num3 += this.dyn_ltree[num * 2];
    //            num++;
    //        }
    //        while (num < 0x80)
    //        {
    //            num2 += this.dyn_ltree[num * 2];
    //            num++;
    //        }
    //        while (num < InternalConstants.LITERALS)
    //        {
    //            num3 += this.dyn_ltree[num * 2];
    //            num++;
    //        }
    //        this.data_type = (num3 > (num2 >> 2)) ? ((sbyte) 0) : ((sbyte) 1);
    //    }

    //    private void SetDeflater()
    //    {
    //        switch (this.config.Flavor)
    //        {
    //            case DeflateFlavor.Store:
    //                this.DeflateFunction = new CompressFunc(this.DeflateNone);
    //                break;

    //            case DeflateFlavor.Fast:
    //                this.DeflateFunction = new CompressFunc(this.DeflateFast);
    //                break;

    //            case DeflateFlavor.Slow:
    //                this.DeflateFunction = new CompressFunc(this.DeflateSlow);
    //                break;
    //        }
    //    }

    //    internal int SetDictionary(byte[] dictionary)
    //    {
    //        int length = dictionary.Length;
    //        int sourceIndex = 0;
    //        if ((dictionary == null) || (this.status != 0x2a))
    //        {
    //            throw new ZlibException("Stream error.");
    //        }
    //        this._codec._Adler32 = Adler.Adler32(this._codec._Adler32, dictionary, 0, dictionary.Length);
    //        if (length >= 3)
    //        {
    //            if (length > (this.w_size - 0x106))
    //            {
    //                length = this.w_size - 0x106;
    //                sourceIndex = dictionary.Length - length;
    //            }
    //            Array.Copy(dictionary, sourceIndex, this.window, 0, length);
    //            this.strstart = length;
    //            this.blockStart = length;
    //            this.ins_h = this.window[0] & 0xff;
    //            this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[1] & 0xff)) & this.hash_mask;
    //            for (int i = 0; i <= (length - 3); i++)
    //            {
    //                this.ins_h = ((this.ins_h << this.hash_shift) ^ (this.window[i + 2] & 0xff)) & this.hash_mask;
    //                this.prev[i & this.w_mask] = this.head[this.ins_h];
    //                this.head[this.ins_h] = (short) i;
    //            }
    //        }
    //        return 0;
    //    }

    //    internal int SetParams(CompressionLevel level, CompressionStrategy strategy)
    //    {
    //        int num = 0;
    //        if (this.compressionLevel != level)
    //        {
    //            Config config = Config.Lookup(level);
    //            if ((config.Flavor != this.config.Flavor) && (this._codec.TotalBytesIn != 0L))
    //            {
    //                num = this._codec.Deflate(FlushType.Partial);
    //            }
    //            this.compressionLevel = level;
    //            this.config = config;
    //            this.SetDeflater();
    //        }
    //        this.compressionStrategy = strategy;
    //        return num;
    //    }

    //    internal bool WantRfc1950HeaderBytes
    //    {
    //        get
    //        {
    //            return this._WantRfc1950HeaderBytes;
    //        }
    //        set
    //        {
    //            this._WantRfc1950HeaderBytes = value;
    //        }
    //    }

    //    internal enum BlockState
    //    {
    //        NeedMore,
    //        BlockDone,
    //        FinishStarted,
    //        FinishDone
    //    }

    //    internal delegate DeflateManager.BlockState CompressFunc(FlushType flush);

    //    internal class Config
    //    {
    //        internal DeflateManager.DeflateFlavor Flavor;
    //        internal int GoodLength;
    //        internal int MaxChainLength;
    //        internal int MaxLazy;
    //        internal int NiceLength;
    //        private static readonly DeflateManager.Config[] Table = new DeflateManager.Config[] { new DeflateManager.Config(0, 0, 0, 0, DeflateManager.DeflateFlavor.Store), new DeflateManager.Config(4, 4, 8, 4, DeflateManager.DeflateFlavor.Fast), new DeflateManager.Config(4, 5, 0x10, 8, DeflateManager.DeflateFlavor.Fast), new DeflateManager.Config(4, 6, 0x20, 0x20, DeflateManager.DeflateFlavor.Fast), new DeflateManager.Config(4, 4, 0x10, 0x10, DeflateManager.DeflateFlavor.Slow), new DeflateManager.Config(8, 0x10, 0x20, 0x20, DeflateManager.DeflateFlavor.Slow), new DeflateManager.Config(8, 0x10, 0x80, 0x80, DeflateManager.DeflateFlavor.Slow), new DeflateManager.Config(8, 0x20, 0x80, 0x100, DeflateManager.DeflateFlavor.Slow), new DeflateManager.Config(0x20, 0x80, 0x102, 0x400, DeflateManager.DeflateFlavor.Slow), new DeflateManager.Config(0x20, 0x102, 0x102, 0x1000, DeflateManager.DeflateFlavor.Slow) };

    //        private Config(int goodLength, int maxLazy, int niceLength, int maxChainLength, DeflateManager.DeflateFlavor flavor)
    //        {
    //            this.GoodLength = goodLength;
    //            this.MaxLazy = maxLazy;
    //            this.NiceLength = niceLength;
    //            this.MaxChainLength = maxChainLength;
    //            this.Flavor = flavor;
    //        }

    //        public static DeflateManager.Config Lookup(CompressionLevel level)
    //        {
    //            return Table[(int) level];
    //        }
    //    }

    //    internal enum DeflateFlavor
    //    {
    //        Store,
    //        Fast,
    //        Slow
    //    }

    //    private sealed class Tree
    //    {
    //        private static readonly sbyte[] _dist_code = new sbyte[] { 
    //            0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 
    //            8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 9, 
    //            10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 
    //            11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 
    //            12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 
    //            12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 
    //            13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 
    //            13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 
    //            14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 
    //            14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 
    //            14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 
    //            14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 
    //            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 
    //            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 
    //            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 
    //            15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 
    //            0, 0, 0x10, 0x11, 0x12, 0x12, 0x13, 0x13, 20, 20, 20, 20, 0x15, 0x15, 0x15, 0x15, 
    //            0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 
    //            0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 
    //            0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 
    //            0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 
    //            0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 
    //            0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 
    //            0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 
    //            0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 
    //            0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 
    //            0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 
    //            0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 0x1c, 
    //            0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 
    //            0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 
    //            0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 
    //            0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d
    //         };
    //        internal static readonly sbyte[] bl_order = new sbyte[] { 
    //            0x10, 0x11, 0x12, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 
    //            14, 1, 15
    //         };
    //        internal const int Buf_size = 0x10;
    //        internal static readonly int[] DistanceBase = new int[] { 
    //            0, 1, 2, 3, 4, 6, 8, 12, 0x10, 0x18, 0x20, 0x30, 0x40, 0x60, 0x80, 0xc0, 
    //            0x100, 0x180, 0x200, 0x300, 0x400, 0x600, 0x800, 0xc00, 0x1000, 0x1800, 0x2000, 0x3000, 0x4000, 0x6000
    //         };
    //        internal short[] dyn_tree;
    //        private static readonly int HEAP_SIZE = ((2 * InternalConstants.L_CODES) + 1);
    //        internal static readonly int[] LengthBase = new int[] { 
    //            0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 0x10, 20, 0x18, 0x1c, 
    //            0x20, 40, 0x30, 0x38, 0x40, 80, 0x60, 0x70, 0x80, 160, 0xc0, 0xe0, 0
    //         };
    //        internal static readonly sbyte[] LengthCode = new sbyte[] { 
    //            0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 9, 9, 10, 10, 11, 11, 
    //            12, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 14, 15, 15, 15, 15, 
    //            0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 
    //            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 0x13, 
    //            20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 
    //            0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 0x15, 
    //            0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 
    //            0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 0x17, 
    //            0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 
    //            0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18, 
    //            0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 
    //            0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 
    //            0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 
    //            0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 0x1a, 
    //            0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 
    //            0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1b, 0x1c
    //         };
    //        internal int max_code;
    //        internal StaticTree staticTree;

    //        internal static int bi_reverse(int code, int len)
    //        {
    //            int num = 0;
    //            do
    //            {
    //                num |= code & 1;
    //                code = code >> 1;
    //                num = num << 1;
    //            }
    //            while (--len > 0);
    //            return (num >> 1);
    //        }

    //        internal void build_tree(DeflateManager s)
    //        {
    //            int num2;
    //            int num5;
    //            short[] tree = this.dyn_tree;
    //            short[] treeCodes = this.staticTree.treeCodes;
    //            int elems = this.staticTree.elems;
    //            int num4 = -1;
    //            s.heap_len = 0;
    //            s.heap_max = HEAP_SIZE;
    //            for (num2 = 0; num2 < elems; num2++)
    //            {
    //                if (tree[num2 * 2] != 0)
    //                {
    //                    s.heap[++s.heap_len] = num4 = num2;
    //                    s.depth[num2] = 0;
    //                }
    //                else
    //                {
    //                    tree[(num2 * 2) + 1] = 0;
    //                }
    //            }
    //            while (s.heap_len < 2)
    //            {
    //                num5 = s.heap[++s.heap_len] = (num4 < 2) ? ++num4 : 0;
    //                tree[num5 * 2] = 1;
    //                s.depth[num5] = 0;
    //                s.opt_len--;
    //                if (treeCodes != null)
    //                {
    //                    s.static_len -= treeCodes[(num5 * 2) + 1];
    //                }
    //            }
    //            this.max_code = num4;
    //            num2 = s.heap_len / 2;
    //            while (num2 >= 1)
    //            {
    //                s.pqdownheap(tree, num2);
    //                num2--;
    //            }
    //            num5 = elems;
    //            do
    //            {
    //                num2 = s.heap[1];
    //                s.heap[1] = s.heap[s.heap_len--];
    //                s.pqdownheap(tree, 1);
    //                int index = s.heap[1];
    //                s.heap[--s.heap_max] = num2;
    //                s.heap[--s.heap_max] = index;
    //                tree[num5 * 2] = (short) (tree[num2 * 2] + tree[index * 2]);
    //                s.depth[num5] = (sbyte) (Math.Max((byte) s.depth[num2], (byte) s.depth[index]) + 1);
    //                tree[(num2 * 2) + 1] = tree[(index * 2) + 1] = (short) num5;
    //                s.heap[1] = num5++;
    //                s.pqdownheap(tree, 1);
    //            }
    //            while (s.heap_len >= 2);
    //            s.heap[--s.heap_max] = s.heap[1];
    //            this.gen_bitlen(s);
    //            gen_codes(tree, num4, s.bl_count);
    //        }

    //        internal static int DistanceCode(int dist)
    //        {
    //            return ((dist < 0x100) ? _dist_code[dist] : _dist_code[0x100 + SharedUtils.URShift(dist, 7)]);
    //        }

    //        internal void gen_bitlen(DeflateManager s)
    //        {
    //            int num4;
    //            short[] numArray = this.dyn_tree;
    //            short[] treeCodes = this.staticTree.treeCodes;
    //            int[] extraBits = this.staticTree.extraBits;
    //            int extraBase = this.staticTree.extraBase;
    //            int maxLength = this.staticTree.maxLength;
    //            int num9 = 0;
    //            int index = 0;
    //            while (index <= InternalConstants.MAX_BITS)
    //            {
    //                s.bl_count[index] = 0;
    //                index++;
    //            }
    //            numArray[(s.heap[s.heap_max] * 2) + 1] = 0;
    //            int num3 = s.heap_max + 1;
    //            while (num3 < HEAP_SIZE)
    //            {
    //                num4 = s.heap[num3];
    //                index = numArray[(numArray[(num4 * 2) + 1] * 2) + 1] + 1;
    //                if (index > maxLength)
    //                {
    //                    index = maxLength;
    //                    num9++;
    //                }
    //                numArray[(num4 * 2) + 1] = (short) index;
    //                if (num4 <= this.max_code)
    //                {
    //                    s.bl_count[index] = (short) (s.bl_count[index] + 1);
    //                    int num7 = 0;
    //                    if (num4 >= extraBase)
    //                    {
    //                        num7 = extraBits[num4 - extraBase];
    //                    }
    //                    short num8 = numArray[num4 * 2];
    //                    s.opt_len += num8 * (index + num7);
    //                    if (treeCodes != null)
    //                    {
    //                        s.static_len += num8 * (treeCodes[(num4 * 2) + 1] + num7);
    //                    }
    //                }
    //                num3++;
    //            }
    //            if (num9 != 0)
    //            {
    //                do
    //                {
    //                    index = maxLength - 1;
    //                    while (s.bl_count[index] == 0)
    //                    {
    //                        index--;
    //                    }
    //                    s.bl_count[index] = (short) (s.bl_count[index] - 1);
    //                    s.bl_count[index + 1] = (short) (s.bl_count[index + 1] + 2);
    //                    s.bl_count[maxLength] = (short) (s.bl_count[maxLength] - 1);
    //                    num9 -= 2;
    //                }
    //                while (num9 > 0);
    //                for (index = maxLength; index != 0; index--)
    //                {
    //                    num4 = s.bl_count[index];
    //                    while (num4 != 0)
    //                    {
    //                        int num5 = s.heap[--num3];
    //                        if (num5 <= this.max_code)
    //                        {
    //                            if (numArray[(num5 * 2) + 1] != index)
    //                            {
    //                                s.opt_len += (index - numArray[(num5 * 2) + 1]) * numArray[num5 * 2];
    //                                numArray[(num5 * 2) + 1] = (short) index;
    //                            }
    //                            num4--;
    //                        }
    //                    }
    //                }
    //            }
    //        }

    //        internal static void gen_codes(short[] tree, int max_code, short[] bl_count)
    //        {
    //            short[] numArray = new short[InternalConstants.MAX_BITS + 1];
    //            short num = 0;
    //            for (int i = 1; i <= InternalConstants.MAX_BITS; i++)
    //            {
    //                numArray[i] = num = (short) ((num + bl_count[i - 1]) << 1);
    //            }
    //            for (int j = 0; j <= max_code; j++)
    //            {
    //                int index = tree[(j * 2) + 1];
    //                if (index != 0)
    //                {
    //                    short num5;
    //                    numArray[index] = (short) ((num5 = numArray[index]) + 1);
    //                    tree[j * 2] = (short) bi_reverse(num5, index);
    //                }
    //            }
    //        }
    //    }
    //}

    internal sealed partial class DeflateManager {
        // extra bits for each length code
        internal static readonly int[] ExtraLengthBits = new int[]
                                                             {
                                                                 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2,
                                                                 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0
                                                             };

        // extra bits for each distance code
        internal static readonly int[] ExtraDistanceBits = new int[]
                                                               {
                                                                   0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6,
                                                                   7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13
                                                               };

        internal enum BlockState {
            NeedMore = 0, // block not completed, need more input or more output
            BlockDone, // block flush performed
            FinishStarted, // finish started, need only more output at next deflate
            FinishDone // finish done, accept no more input or output
        }

        internal enum DeflateFlavor {
            Store,
            Fast,
            Slow
        }

        private const int MEM_LEVEL_MAX = 9;
        private const int MEM_LEVEL_DEFAULT = 8;

        internal delegate BlockState CompressFunc(FlushType flush);

        internal class Config {
            // Use a faster search when the previous match is longer than this
            internal int GoodLength; // reduce lazy search above this match length

            // Attempt to find a better match only when the current match is
            // strictly smaller than this value. This mechanism is used only for
            // compression levels >= 4.  For levels 1,2,3: MaxLazy is actually
            // MaxInsertLength. (See DeflateFast)

            internal int MaxLazy; // do not perform lazy search above this match length

            internal int NiceLength; // quit search above this match length

            // To speed up deflation, hash chains are never searched beyond this
            // length.  A higher limit improves compression ratio but degrades the speed.

            internal int MaxChainLength;

            internal DeflateFlavor Flavor;

            private Config(int goodLength, int maxLazy, int niceLength, int maxChainLength, DeflateFlavor flavor) {
                this.GoodLength = goodLength;
                this.MaxLazy = maxLazy;
                this.NiceLength = niceLength;
                this.MaxChainLength = maxChainLength;
                this.Flavor = flavor;
            }

            public static Config Lookup(CompressionLevel level) {
                return Table[(int)level];
            }


            static Config() {
                Table = new Config[]
                            {
                                new Config(0, 0, 0, 0, DeflateFlavor.Store),
                                new Config(4, 4, 8, 4, DeflateFlavor.Fast),
                                new Config(4, 5, 16, 8, DeflateFlavor.Fast),
                                new Config(4, 6, 32, 32, DeflateFlavor.Fast),
                                new Config(4, 4, 16, 16, DeflateFlavor.Slow),
                                new Config(8, 16, 32, 32, DeflateFlavor.Slow),
                                new Config(8, 16, 128, 128, DeflateFlavor.Slow),
                                new Config(8, 32, 128, 256, DeflateFlavor.Slow),
                                new Config(32, 128, 258, 1024, DeflateFlavor.Slow),
                                new Config(32, 258, 258, 4096, DeflateFlavor.Slow),
                            };
            }

            private static readonly Config[] Table;
        }


        private CompressFunc DeflateFunction;

        private static readonly System.String[] _ErrorMessage = new System.String[]
                                                                    {
                                                                        "need dictionary",
                                                                        "stream end",
                                                                        "",
                                                                        "file error",
                                                                        "stream error",
                                                                        "data error",
                                                                        "insufficient memory",
                                                                        "buffer error",
                                                                        "incompatible version",
                                                                        ""
                                                                    };

        // preset dictionary flag in zlib header
        private const int PRESET_DICT = 0x20;

        private const int INIT_STATE = 42;
        private const int BUSY_STATE = 113;
        private const int FINISH_STATE = 666;

        // The deflate compression method
        private const int Z_DEFLATED = 8;

        private const int STORED_BLOCK = 0;
        private const int STATIC_TREES = 1;
        private const int DYN_TREES = 2;

        // The three kinds of block type
        private const int Z_BINARY = 0;
        private const int Z_ASCII = 1;
        private const int Z_UNKNOWN = 2;

        private const int Buf_size = 8 * 2;

        private const int MIN_MATCH = 3;
        private const int MAX_MATCH = 258;

        private const int MIN_LOOKAHEAD = (MAX_MATCH + MIN_MATCH + 1);

        private static readonly int HEAP_SIZE = (2 * InternalConstants.L_CODES + 1);

        private const int END_BLOCK = 256;

        internal ZlibCodec _codec; // the zlib encoder/decoder
        internal int status; // as the name implies
        internal byte[] pending; // output still pending - waiting to be compressed
        internal int nextPending; // index of next pending byte to output to the stream
        internal int pendingCount; // number of bytes in the pending buffer

        internal sbyte data_type; // UNKNOWN, BINARY or ASCII
        internal int last_flush; // value of flush param for previous deflate call

        internal int w_size; // LZ77 window size (32K by default)
        internal int w_bits; // log2(w_size)  (8..16)
        internal int w_mask; // w_size - 1

        //internal byte[] dictionary;
        internal byte[] window;

        // Sliding window. Input bytes are read into the second half of the window,
        // and move to the first half later to keep a dictionary of at least wSize
        // bytes. With this organization, matches are limited to a distance of
        // wSize-MAX_MATCH bytes, but this ensures that IO is always
        // performed with a length multiple of the block size. 
        //
        // To do: use the user input buffer as sliding window.

        internal int window_size;
        // Actual size of window: 2*wSize, except when the user input buffer
        // is directly used as sliding window.

        internal short[] prev;
        // Link to older string with same hash index. To limit the size of this
        // array to 64K, this link is maintained only for the last 32K strings.
        // An index in this array is thus a window index modulo 32K.

        private short[] head; // Heads of the hash chains or NIL.

        private int ins_h; // hash index of string to be inserted
        private int hash_size; // number of elements in hash table
        private int hash_bits; // log2(hash_size)
        private int hash_mask; // hash_size-1

        // Number of bits by which ins_h must be shifted at each input
        // step. It must be such that after MIN_MATCH steps, the oldest
        // byte no longer takes part in the hash key, that is:
        // hash_shift * MIN_MATCH >= hash_bits
        private int hash_shift;

        // Window position at the beginning of the current output block. Gets
        // negative when the window is moved backwards.

        private int blockStart;

        private Config config;
        private int match_length; // length of best match
        private int prev_match; // previous match
        private int match_available; // set if previous match exists
        private int strstart; // start of string to insert into.....????
        private int match_start; // start of matching string
        private int lookahead; // number of valid bytes ahead in window

        // Length of the best match at previous step. Matches not greater than this
        // are discarded. This is used in the lazy match evaluation.
        private int prev_length;

        // Insert new strings in the hash table only if the match length is not
        // greater than this length. This saves time but degrades compression.
        // max_insert_length is used only for compression levels <= 3.

        private CompressionLevel compressionLevel; // compression level (1..9)
        private CompressionStrategy compressionStrategy; // favor or force Huffman coding


        private short[] dyn_ltree; // literal and length tree
        private short[] dyn_dtree; // distance tree
        private short[] bl_tree; // Huffman tree for bit lengths

        private Tree treeLiterals = new Tree(); // desc for literal tree
        private Tree treeDistances = new Tree(); // desc for distance tree
        private Tree treeBitLengths = new Tree(); // desc for bit length tree

        // number of codes at each bit length for an optimal tree
        private short[] bl_count = new short[InternalConstants.MAX_BITS + 1];

        // heap used to build the Huffman trees
        private int[] heap = new int[2 * InternalConstants.L_CODES + 1];

        private int heap_len; // number of elements in the heap
        private int heap_max; // element of largest frequency

        // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
        // The same heap array is used to build all trees.

        // Depth of each subtree used as tie breaker for trees of equal frequency
        private sbyte[] depth = new sbyte[2 * InternalConstants.L_CODES + 1];

        private int _lengthOffset; // index for literals or lengths 


        // Size of match buffer for literals/lengths.  There are 4 reasons for
        // limiting lit_bufsize to 64K:
        //   - frequencies can be kept in 16 bit counters
        //   - if compression is not successful for the first block, all input
        //     data is still in the window so we can still emit a stored block even
        //     when input comes from standard input.  (This can also be done for
        //     all blocks if lit_bufsize is not greater than 32K.)
        //   - if compression is not successful for a file smaller than 64K, we can
        //     even emit a stored file instead of a stored block (saving 5 bytes).
        //     This is applicable only for zip (not gzip or zlib).
        //   - creating new Huffman trees less frequently may not provide fast
        //     adaptation to changes in the input data statistics. (Take for
        //     example a binary file with poorly compressible code followed by
        //     a highly compressible string table.) Smaller buffer sizes give
        //     fast adaptation but have of course the overhead of transmitting
        //     trees more frequently.

        internal int lit_bufsize;

        internal int last_lit; // running index in l_buf

        // Buffer for distances. To simplify the code, d_buf and l_buf have
        // the same number of elements. To use different lengths, an extra flag
        // array would be necessary.

        internal int _distanceOffset; // index into pending; points to distance data??

        internal int opt_len; // bit length of current block with optimal trees
        internal int static_len; // bit length of current block with static trees
        internal int matches; // number of string matches in current block
        internal int last_eob_len; // bit length of EOB code for last block

        // Output buffer. bits are inserted starting at the bottom (least
        // significant bits).
        internal short bi_buf;

        // Number of valid bits in bi_buf.  All bits above the last valid bit
        // are always zero.
        internal int bi_valid;


        internal DeflateManager() {
            dyn_ltree = new short[HEAP_SIZE * 2];
            dyn_dtree = new short[(2 * InternalConstants.D_CODES + 1) * 2]; // distance tree
            bl_tree = new short[(2 * InternalConstants.BL_CODES + 1) * 2]; // Huffman tree for bit lengths
        }


        // lm_init
        private void _InitializeLazyMatch() {
            window_size = 2 * w_size;

            // clear the hash - workitem 9063
            Array.Clear(head, 0, hash_size);
            //for (int i = 0; i < hash_size; i++) head[i] = 0;

            config = Config.Lookup(compressionLevel);
            SetDeflater();

            strstart = 0;
            blockStart = 0;
            lookahead = 0;
            match_length = prev_length = MIN_MATCH - 1;
            match_available = 0;
            ins_h = 0;
        }

        // Initialize the tree data structures for a new zlib stream.
        private void _InitializeTreeData() {
            treeLiterals.dyn_tree = dyn_ltree;
            treeLiterals.staticTree = StaticTree.Literals;

            treeDistances.dyn_tree = dyn_dtree;
            treeDistances.staticTree = StaticTree.Distances;

            treeBitLengths.dyn_tree = bl_tree;
            treeBitLengths.staticTree = StaticTree.BitLengths;

            bi_buf = 0;
            bi_valid = 0;
            last_eob_len = 8; // enough lookahead for inflate

            // Initialize the first block of the first file:
            _InitializeBlocks();
        }

        internal void _InitializeBlocks() {
            // Initialize the trees.
            for (int i = 0; i < InternalConstants.L_CODES; i++)
                dyn_ltree[i * 2] = 0;
            for (int i = 0; i < InternalConstants.D_CODES; i++)
                dyn_dtree[i * 2] = 0;
            for (int i = 0; i < InternalConstants.BL_CODES; i++)
                bl_tree[i * 2] = 0;

            dyn_ltree[END_BLOCK * 2] = 1;
            opt_len = static_len = 0;
            last_lit = matches = 0;
        }

        // Restore the heap property by moving down the tree starting at node k,
        // exchanging a node with the smallest of its two sons if necessary, stopping
        // when the heap property is re-established (each father smaller than its
        // two sons).
        internal void pqdownheap(short[] tree, int k) {
            int v = heap[k];
            int j = k << 1; // left son of k
            while (j <= heap_len) {
                // Set j to the smallest of the two sons:
                if (j < heap_len && IsSmaller(tree, heap[j + 1], heap[j], depth)) {
                    j++;
                }
                // Exit if v is smaller than both sons
                if (IsSmaller(tree, v, heap[j], depth))
                    break;

                // Exchange v with the smallest son
                heap[k] = heap[j];
                k = j;
                // And continue down the tree, setting j to the left son of k
                j <<= 1;
            }
            heap[k] = v;
        }

        internal static bool IsSmaller(short[] tree, int n, int m, sbyte[] depth) {
            short tn2 = tree[n * 2];
            short tm2 = tree[m * 2];
            return (tn2 < tm2 || (tn2 == tm2 && depth[n] <= depth[m]));
        }


        // Scan a literal or distance tree to determine the frequencies of the codes
        // in the bit length tree.
        internal void ScanTree(short[] tree, int maxCode) {
            int n; // iterates over all tree elements
            int prevlen = -1; // last emitted length
            int curlen; // length of current code
            int nextlen = (int)tree[0 * 2 + 1]; // length of next code
            int count = 0; // repeat count of the current code
            int max_count = 7; // max repeat count
            int min_count = 4; // min repeat count

            if (nextlen == 0) {
                max_count = 138;
                min_count = 3;
            }
            tree[(maxCode + 1) * 2 + 1] = (short)0x7fff; // guard //??

            for (n = 0; n <= maxCode; n++) {
                curlen = nextlen;
                nextlen = (int)tree[(n + 1) * 2 + 1];
                if (++count < max_count && curlen == nextlen) {
                    continue;
                }
                else if (count < min_count) {
                    bl_tree[curlen * 2] = (short)(bl_tree[curlen * 2] + count);
                }
                else if (curlen != 0) {
                    if (curlen != prevlen)
                        bl_tree[curlen * 2]++;
                    bl_tree[InternalConstants.REP_3_6 * 2]++;
                }
                else if (count <= 10) {
                    bl_tree[InternalConstants.REPZ_3_10 * 2]++;
                }
                else {
                    bl_tree[InternalConstants.REPZ_11_138 * 2]++;
                }
                count = 0;
                prevlen = curlen;
                if (nextlen == 0) {
                    max_count = 138;
                    min_count = 3;
                }
                else if (curlen == nextlen) {
                    max_count = 6;
                    min_count = 3;
                }
                else {
                    max_count = 7;
                    min_count = 4;
                }
            }
        }

        // Construct the Huffman tree for the bit lengths and return the index in
        // bl_order of the last bit length code to send.
        internal int BuildBlTree() {
            int max_blindex; // index of last bit length code of non zero freq

            // Determine the bit length frequencies for literal and distance trees
            ScanTree(dyn_ltree, treeLiterals.max_code);
            ScanTree(dyn_dtree, treeDistances.max_code);

            // Build the bit length tree:
            treeBitLengths.build_tree(this);
            // opt_len now includes the length of the tree representations, except
            // the lengths of the bit lengths codes and the 5+5+4 bits for the counts.

            // Determine the number of bit length codes to send. The pkzip format
            // requires that at least 4 bit length codes be sent. (appnote.txt says
            // 3 but the actual value used is 4.)
            for (max_blindex = InternalConstants.BL_CODES - 1; max_blindex >= 3; max_blindex--) {
                if (bl_tree[Tree.bl_order[max_blindex] * 2 + 1] != 0)
                    break;
            }
            // Update opt_len to include the bit length tree and counts
            opt_len += 3 * (max_blindex + 1) + 5 + 5 + 4;

            return max_blindex;
        }


        // Send the header for a block using dynamic Huffman trees: the counts, the
        // lengths of the bit length codes, the literal tree and the distance tree.
        // IN assertion: lcodes >= 257, dcodes >= 1, blcodes >= 4.
        internal void send_all_trees(int lcodes, int dcodes, int blcodes) {
            int rank; // index in bl_order

            send_bits(lcodes - 257, 5); // not +255 as stated in appnote.txt
            send_bits(dcodes - 1, 5);
            send_bits(blcodes - 4, 4); // not -3 as stated in appnote.txt
            for (rank = 0; rank < blcodes; rank++) {
                send_bits(bl_tree[Tree.bl_order[rank] * 2 + 1], 3);
            }
            send_tree(dyn_ltree, lcodes - 1); // literal tree
            send_tree(dyn_dtree, dcodes - 1); // distance tree
        }

        // Send a literal or distance tree in compressed form, using the codes in
        // bl_tree.
        internal void send_tree(short[] tree, int max_code) {
            int n; // iterates over all tree elements
            int prevlen = -1; // last emitted length
            int curlen; // length of current code
            int nextlen = tree[0 * 2 + 1]; // length of next code
            int count = 0; // repeat count of the current code
            int max_count = 7; // max repeat count
            int min_count = 4; // min repeat count

            if (nextlen == 0) {
                max_count = 138;
                min_count = 3;
            }

            for (n = 0; n <= max_code; n++) {
                curlen = nextlen;
                nextlen = tree[(n + 1) * 2 + 1];
                if (++count < max_count && curlen == nextlen) {
                    continue;
                }
                else if (count < min_count) {
                    do {
                        send_code(curlen, bl_tree);
                    } while (--count != 0);
                }
                else if (curlen != 0) {
                    if (curlen != prevlen) {
                        send_code(curlen, bl_tree);
                        count--;
                    }
                    send_code(InternalConstants.REP_3_6, bl_tree);
                    send_bits(count - 3, 2);
                }
                else if (count <= 10) {
                    send_code(InternalConstants.REPZ_3_10, bl_tree);
                    send_bits(count - 3, 3);
                }
                else {
                    send_code(InternalConstants.REPZ_11_138, bl_tree);
                    send_bits(count - 11, 7);
                }
                count = 0;
                prevlen = curlen;
                if (nextlen == 0) {
                    max_count = 138;
                    min_count = 3;
                }
                else if (curlen == nextlen) {
                    max_count = 6;
                    min_count = 3;
                }
                else {
                    max_count = 7;
                    min_count = 4;
                }
            }
        }

        // Output a block of bytes on the stream.
        // IN assertion: there is enough room in pending_buf.
        private void put_bytes(byte[] p, int start, int len) {
            Array.Copy(p, start, pending, pendingCount, len);
            pendingCount += len;
        }

#if NOTNEEDED        
        private void put_byte(byte c)
        {
            pending[pendingCount++] = c;
        }
        internal void put_short(int b)
        {
            unchecked
            {
                pending[pendingCount++] = (byte)b;
                pending[pendingCount++] = (byte)(b >> 8);
            }
        }
        internal void putShortMSB(int b)
        {
            unchecked
            {
                pending[pendingCount++] = (byte)(b >> 8);
                pending[pendingCount++] = (byte)b;
            }
        }
#endif

        internal void send_code(int c, short[] tree) {
            int c2 = c * 2;
            send_bits((tree[c2] & 0xffff), (tree[c2 + 1] & 0xffff));
        }

        internal void send_bits(int value, int length) {
            int len = length;
            unchecked {
                if (bi_valid > (int)Buf_size - len) {
                    //int val = value;
                    //      bi_buf |= (val << bi_valid);

                    bi_buf |= (short)((value << bi_valid) & 0xffff);
                    //put_short(bi_buf);
                    pending[pendingCount++] = (byte)bi_buf;
                    pending[pendingCount++] = (byte)(bi_buf >> 8);


                    bi_buf = (short)((uint)value >> (Buf_size - bi_valid));
                    bi_valid += len - Buf_size;
                }
                else {
                    //      bi_buf |= (value) << bi_valid;
                    bi_buf |= (short)((value << bi_valid) & 0xffff);
                    bi_valid += len;
                }
            }
        }

        // Send one empty static block to give enough lookahead for inflate.
        // This takes 10 bits, of which 7 may remain in the bit buffer.
        // The current inflate code requires 9 bits of lookahead. If the
        // last two codes for the previous block (real code plus EOB) were coded
        // on 5 bits or less, inflate may have only 5+3 bits of lookahead to decode
        // the last real code. In this case we send two empty static blocks instead
        // of one. (There are no problems if the previous block is stored or fixed.)
        // To simplify the code, we assume the worst case of last real code encoded
        // on one bit only.
        internal void _tr_align() {
            send_bits(STATIC_TREES << 1, 3);
            send_code(END_BLOCK, StaticTree.lengthAndLiteralsTreeCodes);

            bi_flush();

            // Of the 10 bits for the empty block, we have already sent
            // (10 - bi_valid) bits. The lookahead for the last real code (before
            // the EOB of the previous block) was thus at least one plus the length
            // of the EOB plus what we have just sent of the empty static block.
            if (1 + last_eob_len + 10 - bi_valid < 9) {
                send_bits(STATIC_TREES << 1, 3);
                send_code(END_BLOCK, StaticTree.lengthAndLiteralsTreeCodes);
                bi_flush();
            }
            last_eob_len = 7;
        }


        // Save the match info and tally the frequency counts. Return true if
        // the current block must be flushed.
        internal bool _tr_tally(int dist, int lc) {
            pending[_distanceOffset + last_lit * 2] = unchecked((byte)((uint)dist >> 8));
            pending[_distanceOffset + last_lit * 2 + 1] = unchecked((byte)dist);
            pending[_lengthOffset + last_lit] = unchecked((byte)lc);
            last_lit++;

            if (dist == 0) {
                // lc is the unmatched char
                dyn_ltree[lc * 2]++;
            }
            else {
                matches++;
                // Here, lc is the match length - MIN_MATCH
                dist--; // dist = match distance - 1
                dyn_ltree[(Tree.LengthCode[lc] + InternalConstants.LITERALS + 1) * 2]++;
                dyn_dtree[Tree.DistanceCode(dist) * 2]++;
            }

            if ((last_lit & 0x1fff) == 0 && (int)compressionLevel > 2) {
                // Compute an upper bound for the compressed length
                int out_length = last_lit << 3;
                int in_length = strstart - blockStart;
                int dcode;
                for (dcode = 0; dcode < InternalConstants.D_CODES; dcode++) {
                    out_length =
                        (int)(out_length + (int)dyn_dtree[dcode * 2] * (5L + DeflateManager.ExtraDistanceBits[dcode]));
                }
                out_length >>= 3;
                if ((matches < (last_lit / 2)) && out_length < in_length / 2)
                    return true;
            }

            return (last_lit == lit_bufsize - 1) || (last_lit == lit_bufsize);
            // dinoch - wraparound?
            // We avoid equality with lit_bufsize because of wraparound at 64K
            // on 16 bit machines and because stored blocks are restricted to
            // 64K-1 bytes.
        }


        // Send the block data compressed using the given Huffman trees
        internal void send_compressed_block(short[] ltree, short[] dtree) {
            int distance; // distance of matched string
            int lc; // match length or unmatched char (if dist == 0)
            int lx = 0; // running index in l_buf
            int code; // the code to send
            int extra; // number of extra bits to send

            if (last_lit != 0) {
                do {
                    int ix = _distanceOffset + lx * 2;
                    distance = ((pending[ix] << 8) & 0xff00) |
                               (pending[ix + 1] & 0xff);
                    lc = (pending[_lengthOffset + lx]) & 0xff;
                    lx++;

                    if (distance == 0) {
                        send_code(lc, ltree); // send a literal byte
                    }
                    else {
                        // literal or match pair 
                        // Here, lc is the match length - MIN_MATCH
                        code = Tree.LengthCode[lc];

                        // send the length code
                        send_code(code + InternalConstants.LITERALS + 1, ltree);
                        extra = DeflateManager.ExtraLengthBits[code];
                        if (extra != 0) {
                            // send the extra length bits
                            lc -= Tree.LengthBase[code];
                            send_bits(lc, extra);
                        }
                        distance--; // dist is now the match distance - 1
                        code = Tree.DistanceCode(distance);

                        // send the distance code
                        send_code(code, dtree);

                        extra = DeflateManager.ExtraDistanceBits[code];
                        if (extra != 0) {
                            // send the extra distance bits
                            distance -= Tree.DistanceBase[code];
                            send_bits(distance, extra);
                        }
                    }

                    // Check that the overlay between pending and d_buf+l_buf is ok:
                } while (lx < last_lit);
            }

            send_code(END_BLOCK, ltree);
            last_eob_len = ltree[END_BLOCK * 2 + 1];
        }


        // Set the data type to ASCII or BINARY, using a crude approximation:
        // binary if more than 20% of the bytes are <= 6 or >= 128, ascii otherwise.
        // IN assertion: the fields freq of dyn_ltree are set and the total of all
        // frequencies does not exceed 64K (to fit in an int on 16 bit machines).
        internal void set_data_type() {
            int n = 0;
            int ascii_freq = 0;
            int bin_freq = 0;
            while (n < 7) {
                bin_freq += dyn_ltree[n * 2];
                n++;
            }
            while (n < 128) {
                ascii_freq += dyn_ltree[n * 2];
                n++;
            }
            while (n < InternalConstants.LITERALS) {
                bin_freq += dyn_ltree[n * 2];
                n++;
            }
            data_type = (sbyte)(bin_freq > (ascii_freq >> 2) ? Z_BINARY : Z_ASCII);
        }


        // Flush the bit buffer, keeping at most 7 bits in it.
        internal void bi_flush() {
            if (bi_valid == 16) {
                pending[pendingCount++] = (byte)bi_buf;
                pending[pendingCount++] = (byte)(bi_buf >> 8);
                bi_buf = 0;
                bi_valid = 0;
            }
            else if (bi_valid >= 8) {
                //put_byte((byte)bi_buf);
                pending[pendingCount++] = (byte)bi_buf;
                bi_buf >>= 8;
                bi_valid -= 8;
            }
        }

        // Flush the bit buffer and align the output on a byte boundary
        internal void bi_windup() {
            if (bi_valid > 8) {
                pending[pendingCount++] = (byte)bi_buf;
                pending[pendingCount++] = (byte)(bi_buf >> 8);
            }
            else if (bi_valid > 0) {
                //put_byte((byte)bi_buf);
                pending[pendingCount++] = (byte)bi_buf;
            }
            bi_buf = 0;
            bi_valid = 0;
        }

        // Copy a stored block, storing first the length and its
        // one's complement if requested.
        internal void copy_block(int buf, int len, bool header) {
            bi_windup(); // align on byte boundary
            last_eob_len = 8; // enough lookahead for inflate

            if (header)
                unchecked {
                    //put_short((short)len);
                    pending[pendingCount++] = (byte)len;
                    pending[pendingCount++] = (byte)(len >> 8);
                    //put_short((short)~len);
                    pending[pendingCount++] = (byte)~len;
                    pending[pendingCount++] = (byte)(~len >> 8);
                }

            put_bytes(window, buf, len);
        }

        internal void flush_block_only(bool eof) {
            _tr_flush_block(blockStart >= 0 ? blockStart : -1, strstart - blockStart, eof);
            blockStart = strstart;
            _codec.flush_pending();
        }

        // Copy without compression as much as possible from the input stream, return
        // the current block state.
        // This function does not insert new strings in the dictionary since
        // uncompressible data is probably not useful. This function is used
        // only for the level=0 compression option.
        // NOTE: this function should be optimized to avoid extra copying from
        // window to pending_buf.
        internal BlockState DeflateNone(FlushType flush) {
            // Stored blocks are limited to 0xffff bytes, pending is limited
            // to pending_buf_size, and each stored block has a 5 byte header:

            int max_block_size = 0xffff;
            int max_start;

            if (max_block_size > pending.Length - 5) {
                max_block_size = pending.Length - 5;
            }

            // Copy as much as possible from input to output:
            while (true) {
                // Fill the window as much as possible:
                if (lookahead <= 1) {
                    _fillWindow();
                    if (lookahead == 0 && flush == FlushType.None)
                        return BlockState.NeedMore;
                    if (lookahead == 0)
                        break; // flush the current block
                }

                strstart += lookahead;
                lookahead = 0;

                // Emit a stored block if pending will be full:
                max_start = blockStart + max_block_size;
                if (strstart == 0 || strstart >= max_start) {
                    // strstart == 0 is possible when wraparound on 16-bit machine
                    lookahead = (int)(strstart - max_start);
                    strstart = (int)max_start;

                    flush_block_only(false);
                    if (_codec.AvailableBytesOut == 0)
                        return BlockState.NeedMore;
                }

                // Flush if we may have to slide, otherwise block_start may become
                // negative and the data will be gone:
                if (strstart - blockStart >= w_size - MIN_LOOKAHEAD) {
                    flush_block_only(false);
                    if (_codec.AvailableBytesOut == 0)
                        return BlockState.NeedMore;
                }
            }

            flush_block_only(flush == FlushType.Finish);
            if (_codec.AvailableBytesOut == 0)
                return (flush == FlushType.Finish) ? BlockState.FinishStarted : BlockState.NeedMore;

            return flush == FlushType.Finish ? BlockState.FinishDone : BlockState.BlockDone;
        }


        // Send a stored block
        internal void _tr_stored_block(int buf, int stored_len, bool eof) {
            send_bits((STORED_BLOCK << 1) + (eof ? 1 : 0), 3); // send block type
            copy_block(buf, stored_len, true); // with header
        }

        // Determine the best encoding for the current block: dynamic trees, static
        // trees or store, and output the encoded block to the zip file.
        internal void _tr_flush_block(int buf, int stored_len, bool eof) {
            int opt_lenb, static_lenb; // opt_len and static_len in bytes
            int max_blindex = 0; // index of last bit length code of non zero freq

            // Build the Huffman trees unless a stored block is forced
            if (compressionLevel > 0) {
                // Check if the file is ascii or binary
                if (data_type == Z_UNKNOWN)
                    set_data_type();

                // Construct the literal and distance trees
                treeLiterals.build_tree(this);

                treeDistances.build_tree(this);

                // At this point, opt_len and static_len are the total bit lengths of
                // the compressed block data, excluding the tree representations.

                // Build the bit length tree for the above two trees, and get the index
                // in bl_order of the last bit length code to send.
                max_blindex = BuildBlTree();

                // Determine the best encoding. Compute first the block length in bytes
                opt_lenb = (opt_len + 3 + 7) >> 3;
                static_lenb = (static_len + 3 + 7) >> 3;

                if (static_lenb <= opt_lenb)
                    opt_lenb = static_lenb;
            }
            else {
                opt_lenb = static_lenb = stored_len + 5; // force a stored block
            }

            if (stored_len + 4 <= opt_lenb && buf != -1) {
                // 4: two words for the lengths
                // The test buf != NULL is only necessary if LIT_BUFSIZE > WSIZE.
                // Otherwise we can't have processed more than WSIZE input bytes since
                // the last block flush, because compression would have been
                // successful. If LIT_BUFSIZE <= WSIZE, it is never too late to
                // transform a block into a stored block.
                _tr_stored_block(buf, stored_len, eof);
            }
            else if (static_lenb == opt_lenb) {
                send_bits((STATIC_TREES << 1) + (eof ? 1 : 0), 3);
                send_compressed_block(StaticTree.lengthAndLiteralsTreeCodes, StaticTree.distTreeCodes);
            }
            else {
                send_bits((DYN_TREES << 1) + (eof ? 1 : 0), 3);
                send_all_trees(treeLiterals.max_code + 1, treeDistances.max_code + 1, max_blindex + 1);
                send_compressed_block(dyn_ltree, dyn_dtree);
            }

            // The above check is made mod 2^32, for files larger than 512 MB
            // and uLong implemented on 32 bits.

            _InitializeBlocks();

            if (eof) {
                bi_windup();
            }
        }

        // Fill the window when the lookahead becomes insufficient.
        // Updates strstart and lookahead.
        //
        // IN assertion: lookahead < MIN_LOOKAHEAD
        // OUT assertions: strstart <= window_size-MIN_LOOKAHEAD
        //    At least one byte has been read, or avail_in == 0; reads are
        //    performed for at least two bytes (required for the zip translate_eol
        //    option -- not supported here).
        private void _fillWindow() {
            int n, m;
            int p;
            int more; // Amount of free space at the end of the window.

            do {
                more = (window_size - lookahead - strstart);

                // Deal with !@#$% 64K limit:
                if (more == 0 && strstart == 0 && lookahead == 0) {
                    more = w_size;
                }
                else if (more == -1) {
                    // Very unlikely, but possible on 16 bit machine if strstart == 0
                    // and lookahead == 1 (input done one byte at time)
                    more--;

                    // If the window is almost full and there is insufficient lookahead,
                    // move the upper half to the lower one to make room in the upper half.
                }
                else if (strstart >= w_size + w_size - MIN_LOOKAHEAD) {
                    Array.Copy(window, w_size, window, 0, w_size);
                    match_start -= w_size;
                    strstart -= w_size; // we now have strstart >= MAX_DIST
                    blockStart -= w_size;

                    // Slide the hash table (could be avoided with 32 bit values
                    // at the expense of memory usage). We slide even when level == 0
                    // to keep the hash table consistent if we switch back to level > 0
                    // later. (Using level 0 permanently is not an optimal usage of
                    // zlib, so we don't care about this pathological case.)

                    n = hash_size;
                    p = n;
                    do {
                        m = (head[--p] & 0xffff);
                        head[p] = (short)((m >= w_size) ? (m - w_size) : 0);
                    } while (--n != 0);

                    n = w_size;
                    p = n;
                    do {
                        m = (prev[--p] & 0xffff);
                        prev[p] = (short)((m >= w_size) ? (m - w_size) : 0);
                        // If n is not on any hash chain, prev[n] is garbage but
                        // its value will never be used.
                    } while (--n != 0);
                    more += w_size;
                }

                if (_codec.AvailableBytesIn == 0)
                    return;

                // If there was no sliding:
                //    strstart <= WSIZE+MAX_DIST-1 && lookahead <= MIN_LOOKAHEAD - 1 &&
                //    more == window_size - lookahead - strstart
                // => more >= window_size - (MIN_LOOKAHEAD-1 + WSIZE + MAX_DIST-1)
                // => more >= window_size - 2*WSIZE + 2
                // In the BIG_MEM or MMAP case (not yet supported),
                //   window_size == input_size + MIN_LOOKAHEAD  &&
                //   strstart + s->lookahead <= input_size => more >= MIN_LOOKAHEAD.
                // Otherwise, window_size == 2*WSIZE so more >= 2.
                // If there was sliding, more >= WSIZE. So in all cases, more >= 2.

                n = _codec.read_buf(window, strstart + lookahead, more);
                lookahead += n;

                // Initialize the hash value now that we have some input:
                if (lookahead >= MIN_MATCH) {
                    ins_h = window[strstart] & 0xff;
                    ins_h = (((ins_h) << hash_shift) ^ (window[strstart + 1] & 0xff)) & hash_mask;
                }
                // If the whole input has less than MIN_MATCH bytes, ins_h is garbage,
                // but this is not important since only literal bytes will be emitted.
            } while (lookahead < MIN_LOOKAHEAD && _codec.AvailableBytesIn != 0);
        }

        // Compress as much as possible from the input stream, return the current
        // block state.
        // This function does not perform lazy evaluation of matches and inserts
        // new strings in the dictionary only for unmatched strings or for short
        // matches. It is used only for the fast compression options.
        internal BlockState DeflateFast(FlushType flush) {
            //    short hash_head = 0; // head of the hash chain
            int hash_head = 0; // head of the hash chain
            bool bflush; // set if current block must be flushed

            while (true) {
                // Make sure that we always have enough lookahead, except
                // at the end of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.
                if (lookahead < MIN_LOOKAHEAD) {
                    _fillWindow();
                    if (lookahead < MIN_LOOKAHEAD && flush == FlushType.None) {
                        return BlockState.NeedMore;
                    }
                    if (lookahead == 0)
                        break; // flush the current block
                }

                // Insert the string window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:
                if (lookahead >= MIN_MATCH) {
                    ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;

                    //  prev[strstart&w_mask]=hash_head=head[ins_h];
                    hash_head = (head[ins_h] & 0xffff);
                    prev[strstart & w_mask] = head[ins_h];
                    head[ins_h] = unchecked((short)strstart);
                }

                // Find the longest match, discarding those <= prev_length.
                // At this point we have always match_length < MIN_MATCH

                if (hash_head != 0L && ((strstart - hash_head) & 0xffff) <= w_size - MIN_LOOKAHEAD) {
                    // To simplify the code, we prevent matches with the string
                    // of window index 0 (in particular we have to avoid a match
                    // of the string with itself at the start of the input file).
                    if (compressionStrategy != CompressionStrategy.HuffmanOnly) {
                        match_length = longest_match(hash_head);
                    }
                    // longest_match() sets match_start
                }
                if (match_length >= MIN_MATCH) {
                    //        check_match(strstart, match_start, match_length);

                    bflush = _tr_tally(strstart - match_start, match_length - MIN_MATCH);

                    lookahead -= match_length;

                    // Insert new strings in the hash table only if the match length
                    // is not too large. This saves time but degrades compression.
                    if (match_length <= config.MaxLazy && lookahead >= MIN_MATCH) {
                        match_length--; // string at strstart already in hash table
                        do {
                            strstart++;

                            ins_h = ((ins_h << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                            //      prev[strstart&w_mask]=hash_head=head[ins_h];
                            hash_head = (head[ins_h] & 0xffff);
                            prev[strstart & w_mask] = head[ins_h];
                            head[ins_h] = unchecked((short)strstart);

                            // strstart never exceeds WSIZE-MAX_MATCH, so there are
                            // always MIN_MATCH bytes ahead.
                        } while (--match_length != 0);
                        strstart++;
                    }
                    else {
                        strstart += match_length;
                        match_length = 0;
                        ins_h = window[strstart] & 0xff;

                        ins_h = (((ins_h) << hash_shift) ^ (window[strstart + 1] & 0xff)) & hash_mask;
                        // If lookahead < MIN_MATCH, ins_h is garbage, but it does not
                        // matter since it will be recomputed at next deflate call.
                    }
                }
                else {
                    // No match, output a literal byte

                    bflush = _tr_tally(0, window[strstart] & 0xff);
                    lookahead--;
                    strstart++;
                }
                if (bflush) {
                    flush_block_only(false);
                    if (_codec.AvailableBytesOut == 0)
                        return BlockState.NeedMore;
                }
            }

            flush_block_only(flush == FlushType.Finish);
            if (_codec.AvailableBytesOut == 0) {
                if (flush == FlushType.Finish)
                    return BlockState.FinishStarted;
                else
                    return BlockState.NeedMore;
            }
            return flush == FlushType.Finish ? BlockState.FinishDone : BlockState.BlockDone;
        }

        // Same as above, but achieves better compression. We use a lazy
        // evaluation for matches: a match is finally adopted only if there is
        // no better match at the next window position.
        internal BlockState DeflateSlow(FlushType flush) {
            //    short hash_head = 0;    // head of hash chain
            int hash_head = 0; // head of hash chain
            bool bflush; // set if current block must be flushed

            // Process the input block.
            while (true) {
                // Make sure that we always have enough lookahead, except
                // at the end of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.

                if (lookahead < MIN_LOOKAHEAD) {
                    _fillWindow();
                    if (lookahead < MIN_LOOKAHEAD && flush == FlushType.None)
                        return BlockState.NeedMore;

                    if (lookahead == 0)
                        break; // flush the current block
                }

                // Insert the string window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:

                if (lookahead >= MIN_MATCH) {
                    ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                    //  prev[strstart&w_mask]=hash_head=head[ins_h];
                    hash_head = (head[ins_h] & 0xffff);
                    prev[strstart & w_mask] = head[ins_h];
                    head[ins_h] = unchecked((short)strstart);
                }

                // Find the longest match, discarding those <= prev_length.
                prev_length = match_length;
                prev_match = match_start;
                match_length = MIN_MATCH - 1;

                if (hash_head != 0 && prev_length < config.MaxLazy &&
                    ((strstart - hash_head) & 0xffff) <= w_size - MIN_LOOKAHEAD) {
                    // To simplify the code, we prevent matches with the string
                    // of window index 0 (in particular we have to avoid a match
                    // of the string with itself at the start of the input file).

                    if (compressionStrategy != CompressionStrategy.HuffmanOnly) {
                        match_length = longest_match(hash_head);
                    }
                    // longest_match() sets match_start

                    if (match_length <= 5 && (compressionStrategy == CompressionStrategy.Filtered ||
                                              (match_length == MIN_MATCH && strstart - match_start > 4096))) {
                        // If prev_match is also MIN_MATCH, match_start is garbage
                        // but we will ignore the current match anyway.
                        match_length = MIN_MATCH - 1;
                    }
                }

                // If there was a match at the previous step and the current
                // match is not better, output the previous match:
                if (prev_length >= MIN_MATCH && match_length <= prev_length) {
                    int max_insert = strstart + lookahead - MIN_MATCH;
                    // Do not insert strings in hash table beyond this.

                    //          check_match(strstart-1, prev_match, prev_length);

                    bflush = _tr_tally(strstart - 1 - prev_match, prev_length - MIN_MATCH);

                    // Insert in hash table all strings up to the end of the match.
                    // strstart-1 and strstart are already inserted. If there is not
                    // enough lookahead, the last two strings are not inserted in
                    // the hash table.
                    lookahead -= (prev_length - 1);
                    prev_length -= 2;
                    do {
                        if (++strstart <= max_insert) {
                            ins_h = (((ins_h) << hash_shift) ^ (window[(strstart) + (MIN_MATCH - 1)] & 0xff)) &
                                    hash_mask;
                            //prev[strstart&w_mask]=hash_head=head[ins_h];
                            hash_head = (head[ins_h] & 0xffff);
                            prev[strstart & w_mask] = head[ins_h];
                            head[ins_h] = unchecked((short)strstart);
                        }
                    } while (--prev_length != 0);
                    match_available = 0;
                    match_length = MIN_MATCH - 1;
                    strstart++;

                    if (bflush) {
                        flush_block_only(false);
                        if (_codec.AvailableBytesOut == 0)
                            return BlockState.NeedMore;
                    }
                }
                else if (match_available != 0) {
                    // If there was no match at the previous position, output a
                    // single literal. If there was a match but the current match
                    // is longer, truncate the previous match to a single literal.

                    bflush = _tr_tally(0, window[strstart - 1] & 0xff);

                    if (bflush) {
                        flush_block_only(false);
                    }
                    strstart++;
                    lookahead--;
                    if (_codec.AvailableBytesOut == 0)
                        return BlockState.NeedMore;
                }
                else {
                    // There is no previous match to compare with, wait for
                    // the next step to decide.

                    match_available = 1;
                    strstart++;
                    lookahead--;
                }
            }

            if (match_available != 0) {
                bflush = _tr_tally(0, window[strstart - 1] & 0xff);
                match_available = 0;
            }
            flush_block_only(flush == FlushType.Finish);

            if (_codec.AvailableBytesOut == 0) {
                if (flush == FlushType.Finish)
                    return BlockState.FinishStarted;
                else
                    return BlockState.NeedMore;
            }

            return flush == FlushType.Finish ? BlockState.FinishDone : BlockState.BlockDone;
        }


        internal int longest_match(int cur_match) {
            int chain_length = config.MaxChainLength; // max hash chain length
            int scan = strstart; // current string
            int match; // matched string
            int len; // length of current match
            int best_len = prev_length; // best match length so far
            int limit = strstart > (w_size - MIN_LOOKAHEAD) ? strstart - (w_size - MIN_LOOKAHEAD) : 0;

            int niceLength = config.NiceLength;

            // Stop when cur_match becomes <= limit. To simplify the code,
            // we prevent matches with the string of window index 0.

            int wmask = w_mask;

            int strend = strstart + MAX_MATCH;
            byte scan_end1 = window[scan + best_len - 1];
            byte scan_end = window[scan + best_len];

            // The code is optimized for HASH_BITS >= 8 and MAX_MATCH-2 multiple of 16.
            // It is easy to get rid of this optimization if necessary.

            // Do not waste too much time if we already have a good match:
            if (prev_length >= config.GoodLength) {
                chain_length >>= 2;
            }

            // Do not look for matches beyond the end of the input. This is necessary
            // to make deflate deterministic.
            if (niceLength > lookahead)
                niceLength = lookahead;

            do {
                match = cur_match;

                // Skip to next match if the match length cannot increase
                // or if the match length is less than 2:
                if (window[match + best_len] != scan_end ||
                    window[match + best_len - 1] != scan_end1 ||
                    window[match] != window[scan] ||
                    window[++match] != window[scan + 1])
                    continue;

                // The check at best_len-1 can be removed because it will be made
                // again later. (This heuristic is not always a win.)
                // It is not necessary to compare scan[2] and match[2] since they
                // are always equal when the other bytes match, given that
                // the hash keys are equal and that HASH_BITS >= 8.
                scan += 2;
                match++;

                // We check for insufficient lookahead only every 8th comparison;
                // the 256th check will be made at strstart+258.
                do {
                } while (window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] &&
                         window[++scan] == window[++match] && scan < strend);

                len = MAX_MATCH - (int)(strend - scan);
                scan = strend - MAX_MATCH;

                if (len > best_len) {
                    match_start = cur_match;
                    best_len = len;
                    if (len >= niceLength)
                        break;
                    scan_end1 = window[scan + best_len - 1];
                    scan_end = window[scan + best_len];
                }
            } while ((cur_match = (prev[cur_match & wmask] & 0xffff)) > limit && --chain_length != 0);

            if (best_len <= lookahead)
                return best_len;
            return lookahead;
        }


        private bool Rfc1950BytesEmitted = false;
        private bool _WantRfc1950HeaderBytes = true;

        internal bool WantRfc1950HeaderBytes {
            get { return _WantRfc1950HeaderBytes; }
            set { _WantRfc1950HeaderBytes = value; }
        }


        internal int Initialize(ZlibCodec codec, CompressionLevel level) {
            return Initialize(codec, level, ZlibConstants.WindowBitsMax);
        }

        internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits) {
            return Initialize(codec, level, bits, MEM_LEVEL_DEFAULT, CompressionStrategy.Default);
        }

        internal int Initialize(ZlibCodec codec, CompressionLevel level, int bits,
                                CompressionStrategy compressionStrategy) {
            return Initialize(codec, level, bits, MEM_LEVEL_DEFAULT, compressionStrategy);
        }

        internal int Initialize(ZlibCodec codec, CompressionLevel level, int windowBits, int memLevel,
                                CompressionStrategy strategy) {
            _codec = codec;
            _codec.Message = null;

            // validation
            if (windowBits < 9 || windowBits > 15)
                throw new ZlibException("windowBits must be in the range 9..15.");

            if (memLevel < 1 || memLevel > MEM_LEVEL_MAX)
                throw new ZlibException(String.Format("memLevel must be in the range 1.. {0}", MEM_LEVEL_MAX));

            _codec.dstate = this;

            w_bits = windowBits;
            w_size = 1 << w_bits;
            w_mask = w_size - 1;

            hash_bits = memLevel + 7;
            hash_size = 1 << hash_bits;
            hash_mask = hash_size - 1;
            hash_shift = ((hash_bits + MIN_MATCH - 1) / MIN_MATCH);

            window = new byte[w_size * 2];
            prev = new short[w_size];
            head = new short[hash_size];

            // for memLevel==8, this will be 16384, 16k
            lit_bufsize = 1 << (memLevel + 6);

            // Use a single array as the buffer for data pending compression,
            // the output distance codes, and the output length codes (aka tree).  
            // orig comment: This works just fine since the average
            // output size for (length,distance) codes is <= 24 bits.
            pending = new byte[lit_bufsize * 4];
            _distanceOffset = lit_bufsize;
            _lengthOffset = (1 + 2) * lit_bufsize;

            // So, for memLevel 8, the length of the pending buffer is 65536. 64k.
            // The first 16k are pending bytes.
            // The middle slice, of 32k, is used for distance codes. 
            // The final 16k are length codes.

            this.compressionLevel = level;
            this.compressionStrategy = strategy;

            Reset();
            return ZlibConstants.Z_OK;
        }


        internal void Reset() {
            _codec.TotalBytesIn = _codec.TotalBytesOut = 0;
            _codec.Message = null;
            //strm.data_type = Z_UNKNOWN;

            pendingCount = 0;
            nextPending = 0;

            Rfc1950BytesEmitted = false;

            status = (WantRfc1950HeaderBytes) ? INIT_STATE : BUSY_STATE;
            _codec._Adler32 = Adler.Adler32(0, null, 0, 0);

            last_flush = (int)FlushType.None;

            _InitializeTreeData();
            _InitializeLazyMatch();
        }


        internal int End() {
            if (status != INIT_STATE && status != BUSY_STATE && status != FINISH_STATE) {
                return ZlibConstants.Z_STREAM_ERROR;
            }
            // Deallocate in reverse order of allocations:
            pending = null;
            head = null;
            prev = null;
            window = null;
            // free
            // dstate=null;
            return status == BUSY_STATE ? ZlibConstants.Z_DATA_ERROR : ZlibConstants.Z_OK;
        }


        private void SetDeflater() {
            switch (config.Flavor) {
                case DeflateFlavor.Store:
                    DeflateFunction = DeflateNone;
                    break;
                case DeflateFlavor.Fast:
                    DeflateFunction = DeflateFast;
                    break;
                case DeflateFlavor.Slow:
                    DeflateFunction = DeflateSlow;
                    break;
            }
        }


        internal int SetParams(CompressionLevel level, CompressionStrategy strategy) {
            int result = ZlibConstants.Z_OK;

            if (compressionLevel != level) {
                Config newConfig = Config.Lookup(level);

                // change in the deflate flavor (Fast vs slow vs none)?
                if (newConfig.Flavor != config.Flavor && _codec.TotalBytesIn != 0) {
                    // Flush the last buffer:
                    result = _codec.Deflate(FlushType.Partial);
                }

                compressionLevel = level;
                config = newConfig;
                SetDeflater();
            }

            // no need to flush with change in strategy?  Really? 
            compressionStrategy = strategy;

            return result;
        }


        internal int SetDictionary(byte[] dictionary) {
            int length = dictionary.Length;
            int index = 0;

            if (dictionary == null || status != INIT_STATE)
                throw new ZlibException("Stream error.");

            _codec._Adler32 = Adler.Adler32(_codec._Adler32, dictionary, 0, dictionary.Length);

            if (length < MIN_MATCH)
                return ZlibConstants.Z_OK;
            if (length > w_size - MIN_LOOKAHEAD) {
                length = w_size - MIN_LOOKAHEAD;
                index = dictionary.Length - length; // use the tail of the dictionary
            }
            Array.Copy(dictionary, index, window, 0, length);
            strstart = length;
            blockStart = length;

            // Insert all strings in the hash table (except for the last two bytes).
            // s->lookahead stays null, so s->ins_h will be recomputed at the next
            // call of fill_window.

            ins_h = window[0] & 0xff;
            ins_h = (((ins_h) << hash_shift) ^ (window[1] & 0xff)) & hash_mask;

            for (int n = 0; n <= length - MIN_MATCH; n++) {
                ins_h = (((ins_h) << hash_shift) ^ (window[(n) + (MIN_MATCH - 1)] & 0xff)) & hash_mask;
                prev[n & w_mask] = head[ins_h];
                head[ins_h] = (short)n;
            }
            return ZlibConstants.Z_OK;
        }


        internal int Deflate(FlushType flush) {
            int old_flush;

            if (_codec.OutputBuffer == null ||
                (_codec.InputBuffer == null && _codec.AvailableBytesIn != 0) ||
                (status == FINISH_STATE && flush != FlushType.Finish)) {
                _codec.Message = _ErrorMessage[ZlibConstants.Z_NEED_DICT - (ZlibConstants.Z_STREAM_ERROR)];
                throw new ZlibException(String.Format("Something is fishy. [{0}]", _codec.Message));
                //return ZlibConstants.Z_STREAM_ERROR;
            }
            if (_codec.AvailableBytesOut == 0) {
                _codec.Message = _ErrorMessage[ZlibConstants.Z_NEED_DICT - (ZlibConstants.Z_BUF_ERROR)];
                throw new ZlibException("OutputBuffer is full (AvailableBytesOut == 0)");
                //return ZlibConstants.Z_BUF_ERROR;
            }

            old_flush = last_flush;
            last_flush = (int)flush;

            // Write the zlib (rfc1950) header bytes
            if (status == INIT_STATE) {
                int header = (Z_DEFLATED + ((w_bits - 8) << 4)) << 8;
                int level_flags = (((int)compressionLevel - 1) & 0xff) >> 1;

                if (level_flags > 3)
                    level_flags = 3;
                header |= (level_flags << 6);
                if (strstart != 0)
                    header |= PRESET_DICT;
                header += 31 - (header % 31);

                status = BUSY_STATE;
                //putShortMSB(header);
                unchecked {
                    pending[pendingCount++] = (byte)(header >> 8);
                    pending[pendingCount++] = (byte)header;
                }
                // Save the adler32 of the preset dictionary:
                if (strstart != 0) {
                    ////putShortMSB((int)(SharedUtils.URShift(_codec._Adler32, 16)));
                    //putShortMSB((int)((UInt64)_codec._Adler32 >> 16));
                    //putShortMSB((int)(_codec._Adler32 & 0xffff));
                    pending[pendingCount++] = (byte)((_codec._Adler32 & 0xFF000000) >> 24);
                    pending[pendingCount++] = (byte)((_codec._Adler32 & 0x00FF0000) >> 16);
                    pending[pendingCount++] = (byte)((_codec._Adler32 & 0x0000FF00) >> 8);
                    pending[pendingCount++] = (byte)(_codec._Adler32 & 0x000000FF);
                }
                _codec._Adler32 = Adler.Adler32(0, null, 0, 0);
            }

            // Flush as much pending output as possible
            if (pendingCount != 0) {
                _codec.flush_pending();
                if (_codec.AvailableBytesOut == 0) {
                    //System.out.println("  avail_out==0");
                    // Since avail_out is 0, deflate will be called again with
                    // more output space, but possibly with both pending and
                    // avail_in equal to zero. There won't be anything to do,
                    // but this is not an error situation so make sure we
                    // return OK instead of BUF_ERROR at next call of deflate:
                    last_flush = -1;
                    return ZlibConstants.Z_OK;
                }

                // Make sure there is something to do and avoid duplicate consecutive
                // flushes. For repeated and useless calls with Z_FINISH, we keep
                // returning Z_STREAM_END instead of Z_BUFF_ERROR.
            }
            else if (_codec.AvailableBytesIn == 0 &&
                     (int)flush <= old_flush &&
                     flush != FlushType.Finish) {
                // workitem 8557
                // Not sure why this needs to be an error.
                // pendingCount == 0, which means there's nothing to deflate.
                // And the caller has not asked for a FlushType.Finish, but...
                // that seems very non-fatal.  We can just say "OK" and do nthing.

                // _codec.Message = z_errmsg[ZlibConstants.Z_NEED_DICT - (ZlibConstants.Z_BUF_ERROR)];
                // throw new ZlibException("AvailableBytesIn == 0 && flush<=old_flush && flush != FlushType.Finish");

                return ZlibConstants.Z_OK;
            }

            // User must not provide more input after the first FINISH:
            if (status == FINISH_STATE && _codec.AvailableBytesIn != 0) {
                _codec.Message = _ErrorMessage[ZlibConstants.Z_NEED_DICT - (ZlibConstants.Z_BUF_ERROR)];
                throw new ZlibException("status == FINISH_STATE && _codec.AvailableBytesIn != 0");
            }


            // Start a new block or continue the current one.
            if (_codec.AvailableBytesIn != 0 || lookahead != 0 || (flush != FlushType.None && status != FINISH_STATE)) {
                BlockState bstate = DeflateFunction(flush);

                if (bstate == BlockState.FinishStarted || bstate == BlockState.FinishDone) {
                    status = FINISH_STATE;
                }
                if (bstate == BlockState.NeedMore || bstate == BlockState.FinishStarted) {
                    if (_codec.AvailableBytesOut == 0) {
                        last_flush = -1; // avoid BUF_ERROR next call, see above
                    }
                    return ZlibConstants.Z_OK;
                    // If flush != Z_NO_FLUSH && avail_out == 0, the next call
                    // of deflate should use the same flush parameter to make sure
                    // that the flush is complete. So we don't have to output an
                    // empty block here, this will be done at next call. This also
                    // ensures that for a very small output buffer, we emit at most
                    // one empty block.
                }

                if (bstate == BlockState.BlockDone) {
                    if (flush == FlushType.Partial) {
                        _tr_align();
                    }
                    else {
                        // FlushType.Full or FlushType.Sync
                        _tr_stored_block(0, 0, false);
                        // For a full flush, this empty block will be recognized
                        // as a special marker by inflate_sync().
                        if (flush == FlushType.Full) {
                            // clear hash (forget the history)
                            for (int i = 0; i < hash_size; i++)
                                head[i] = 0;
                        }
                    }
                    _codec.flush_pending();
                    if (_codec.AvailableBytesOut == 0) {
                        last_flush = -1; // avoid BUF_ERROR at next call, see above
                        return ZlibConstants.Z_OK;
                    }
                }
            }

            if (flush != FlushType.Finish)
                return ZlibConstants.Z_OK;

            if (!WantRfc1950HeaderBytes || Rfc1950BytesEmitted)
                return ZlibConstants.Z_STREAM_END;

            // Write the zlib trailer (adler32)
            pending[pendingCount++] = (byte)((_codec._Adler32 & 0xFF000000) >> 24);
            pending[pendingCount++] = (byte)((_codec._Adler32 & 0x00FF0000) >> 16);
            pending[pendingCount++] = (byte)((_codec._Adler32 & 0x0000FF00) >> 8);
            pending[pendingCount++] = (byte)(_codec._Adler32 & 0x000000FF);
            //putShortMSB((int)(SharedUtils.URShift(_codec._Adler32, 16)));
            //putShortMSB((int)(_codec._Adler32 & 0xffff));

            _codec.flush_pending();

            // If avail_out is zero, the application will call deflate again
            // to flush the rest.

            Rfc1950BytesEmitted = true; // write the trailer only once!

            return pendingCount != 0 ? ZlibConstants.Z_OK : ZlibConstants.Z_STREAM_END;
        }
    }
    internal sealed partial class DeflateManager {
        #region Nested type: Tree

        private sealed class Tree {
            internal const int Buf_size = 8 * 2;
            private static readonly int HEAP_SIZE = (2 * InternalConstants.L_CODES + 1);


            internal static readonly sbyte[] bl_order = new sbyte[]
                                                            {
                                                                16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2,
                                                                14,
                                                                1, 15
                                                            };


            // The lengths of the bit length codes are sent in order of decreasing
            // probability, to avoid transmitting the lengths for unused bit
            // length codes.

            // see definition of array dist_code below
            //internal const int DIST_CODE_LEN = 512;

            private static readonly sbyte[] _dist_code = new sbyte[]
                                                             {
                                                                 0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7,
                                                                 8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 9,
                                                                 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
                                                                 10, 10,
                                                                 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
                                                                 11, 11,
                                                                 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
                                                                 12, 12,
                                                                 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
                                                                 12, 12,
                                                                 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
                                                                 13, 13,
                                                                 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
                                                                 13, 13,
                                                                 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
                                                                 14, 14,
                                                                 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
                                                                 14, 14,
                                                                 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
                                                                 14, 14,
                                                                 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
                                                                 14, 14,
                                                                 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
                                                                 15, 15,
                                                                 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
                                                                 15, 15,
                                                                 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
                                                                 15, 15,
                                                                 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
                                                                 15, 15,
                                                                 0, 0, 16, 17, 18, 18, 19, 19, 20, 20, 20, 20, 21, 21,
                                                                 21, 21,
                                                                 22, 22, 22, 22, 22, 22, 22, 22, 23, 23, 23, 23, 23, 23,
                                                                 23, 23,
                                                                 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24,
                                                                 24, 24,
                                                                 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25,
                                                                 25, 25,
                                                                 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26,
                                                                 26, 26,
                                                                 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26,
                                                                 26, 26,
                                                                 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
                                                                 27, 27,
                                                                 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
                                                                 27, 27,
                                                                 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                                                                 28, 28,
                                                                 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                                                                 28, 28,
                                                                 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                                                                 28, 28,
                                                                 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28,
                                                                 28, 28,
                                                                 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
                                                                 29, 29,
                                                                 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
                                                                 29, 29,
                                                                 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
                                                                 29, 29,
                                                                 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29, 29,
                                                                 29, 29
                                                             };

            internal static readonly sbyte[] LengthCode = new sbyte[]
                                                              {
                                                                  0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 9, 9, 10, 10, 11, 11,
                                                                  12, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 14, 15, 15
                                                                  , 15, 15,
                                                                  16, 16, 16, 16, 16, 16, 16, 16, 17, 17, 17, 17, 17, 17
                                                                  , 17, 17,
                                                                  18, 18, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19, 19
                                                                  , 19, 19,
                                                                  20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20, 20
                                                                  , 20, 20,
                                                                  21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21
                                                                  , 21, 21,
                                                                  22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22, 22
                                                                  , 22, 22,
                                                                  23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23, 23
                                                                  , 23, 23,
                                                                  24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24
                                                                  , 24, 24,
                                                                  24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24, 24
                                                                  , 24, 24,
                                                                  25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25
                                                                  , 25, 25,
                                                                  25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25, 25
                                                                  , 25, 25,
                                                                  26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26
                                                                  , 26, 26,
                                                                  26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26, 26
                                                                  , 26, 26,
                                                                  27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27
                                                                  , 27, 27,
                                                                  27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27
                                                                  , 27, 28
                                                              };


            internal static readonly int[] LengthBase = new[]
                                                            {
                                                                0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 16, 20, 24, 28,
                                                                32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 0
                                                            };


            internal static readonly int[] DistanceBase = new[]
                                                              {
                                                                  0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128,
                                                                  192,
                                                                  256, 384, 512, 768, 1024, 1536, 2048, 3072, 4096, 6144
                                                                  , 8192, 12288, 16384, 24576
                                                              };


            internal short[] dyn_tree; // the dynamic tree
            internal int max_code; // largest code with non zero frequency
            internal StaticTree staticTree; // the corresponding static tree

            /// <summary>
            /// Map from a distance to a distance code.
            /// </summary>
            /// <remarks> 
            /// No side effects. _dist_code[256] and _dist_code[257] are never used.
            /// </remarks>
            internal static int DistanceCode(int dist) {
                return (dist < 256)
                           ? _dist_code[dist]
                           : _dist_code[256 + SharedUtils.URShift(dist, 7)];
            }

            // Compute the optimal bit lengths for a tree and update the total bit length
            // for the current block.
            // IN assertion: the fields freq and dad are set, heap[heap_max] and
            //    above are the tree nodes sorted by increasing frequency.
            // OUT assertions: the field len is set to the optimal bit length, the
            //     array bl_count contains the frequencies for each bit length.
            //     The length opt_len is updated; static_len is also updated if stree is
            //     not null.
            internal void gen_bitlen(DeflateManager s) {
                short[] tree = dyn_tree;
                short[] stree = staticTree.treeCodes;
                int[] extra = staticTree.extraBits;
                int base_Renamed = staticTree.extraBase;
                int max_length = staticTree.maxLength;
                int h; // heap index
                int n, m; // iterate over the tree elements
                int bits; // bit length
                int xbits; // extra bits
                short f; // frequency
                int overflow = 0; // number of elements with bit length too large

                for (bits = 0; bits <= InternalConstants.MAX_BITS; bits++)
                    s.bl_count[bits] = 0;

                // In a first pass, compute the optimal bit lengths (which may
                // overflow in the case of the bit length tree).
                tree[s.heap[s.heap_max] * 2 + 1] = 0; // root of the heap

                for (h = s.heap_max + 1; h < HEAP_SIZE; h++) {
                    n = s.heap[h];
                    bits = tree[tree[n * 2 + 1] * 2 + 1] + 1;
                    if (bits > max_length) {
                        bits = max_length;
                        overflow++;
                    }
                    tree[n * 2 + 1] = (short)bits;
                    // We overwrite tree[n*2+1] which is no longer needed

                    if (n > max_code)
                        continue; // not a leaf node

                    s.bl_count[bits]++;
                    xbits = 0;
                    if (n >= base_Renamed)
                        xbits = extra[n - base_Renamed];
                    f = tree[n * 2];
                    s.opt_len += f * (bits + xbits);
                    if (stree != null)
                        s.static_len += f * (stree[n * 2 + 1] + xbits);
                }
                if (overflow == 0)
                    return;

                // This happens for example on obj2 and pic of the Calgary corpus
                // Find the first bit length which could increase:
                do {
                    bits = max_length - 1;
                    while (s.bl_count[bits] == 0)
                        bits--;
                    s.bl_count[bits]--; // move one leaf down the tree
                    s.bl_count[bits + 1] = (short)(s.bl_count[bits + 1] + 2); // move one overflow item as its brother
                    s.bl_count[max_length]--;
                    // The brother of the overflow item also moves one step up,
                    // but this does not affect bl_count[max_length]
                    overflow -= 2;
                } while (overflow > 0);

                for (bits = max_length; bits != 0; bits--) {
                    n = s.bl_count[bits];
                    while (n != 0) {
                        m = s.heap[--h];
                        if (m > max_code)
                            continue;
                        if (tree[m * 2 + 1] != bits) {
                            s.opt_len = (int)(s.opt_len + (bits - (long)tree[m * 2 + 1]) * tree[m * 2]);
                            tree[m * 2 + 1] = (short)bits;
                        }
                        n--;
                    }
                }
            }

            // Construct one Huffman tree and assigns the code bit strings and lengths.
            // Update the total bit length for the current block.
            // IN assertion: the field freq is set for all tree elements.
            // OUT assertions: the fields len and code are set to the optimal bit length
            //     and corresponding code. The length opt_len is updated; static_len is
            //     also updated if stree is not null. The field max_code is set.
            internal void build_tree(DeflateManager s) {
                short[] tree = dyn_tree;
                short[] stree = staticTree.treeCodes;
                int elems = staticTree.elems;
                int n, m; // iterate over heap elements
                int max_code = -1; // largest code with non zero frequency
                int node; // new node being created

                // Construct the initial heap, with least frequent element in
                // heap[1]. The sons of heap[n] are heap[2*n] and heap[2*n+1].
                // heap[0] is not used.
                s.heap_len = 0;
                s.heap_max = HEAP_SIZE;

                for (n = 0; n < elems; n++) {
                    if (tree[n * 2] != 0) {
                        s.heap[++s.heap_len] = max_code = n;
                        s.depth[n] = 0;
                    }
                    else {
                        tree[n * 2 + 1] = 0;
                    }
                }

                // The pkzip format requires that at least one distance code exists,
                // and that at least one bit should be sent even if there is only one
                // possible code. So to avoid special checks later on we force at least
                // two codes of non zero frequency.
                while (s.heap_len < 2) {
                    node = s.heap[++s.heap_len] = (max_code < 2 ? ++max_code : 0);
                    tree[node * 2] = 1;
                    s.depth[node] = 0;
                    s.opt_len--;
                    if (stree != null)
                        s.static_len -= stree[node * 2 + 1];
                    // node is 0 or 1 so it does not have extra bits
                }
                this.max_code = max_code;

                // The elements heap[heap_len/2+1 .. heap_len] are leaves of the tree,
                // establish sub-heaps of increasing lengths:

                for (n = s.heap_len / 2; n >= 1; n--)
                    s.pqdownheap(tree, n);

                // Construct the Huffman tree by repeatedly combining the least two
                // frequent nodes.

                node = elems; // next internal node of the tree
                do {
                    // n = node of least frequency
                    n = s.heap[1];
                    s.heap[1] = s.heap[s.heap_len--];
                    s.pqdownheap(tree, 1);
                    m = s.heap[1]; // m = node of next least frequency

                    s.heap[--s.heap_max] = n; // keep the nodes sorted by frequency
                    s.heap[--s.heap_max] = m;

                    // Create a new node father of n and m
                    tree[node * 2] = unchecked((short)(tree[n * 2] + tree[m * 2]));
                    s.depth[node] = (sbyte)(Math.Max((byte)s.depth[n], (byte)s.depth[m]) + 1);
                    tree[n * 2 + 1] = tree[m * 2 + 1] = (short)node;

                    // and insert the new node in the heap
                    s.heap[1] = node++;
                    s.pqdownheap(tree, 1);
                } while (s.heap_len >= 2);

                s.heap[--s.heap_max] = s.heap[1];

                // At this point, the fields freq and dad are set. We can now
                // generate the bit lengths.

                gen_bitlen(s);

                // The field len is now set, we can generate the bit codes
                gen_codes(tree, max_code, s.bl_count);
            }

            // Generate the codes for a given tree and bit counts (which need not be
            // optimal).
            // IN assertion: the array bl_count contains the bit length statistics for
            // the given tree and the field len is set for all tree elements.
            // OUT assertion: the field code is set for all tree elements of non
            //     zero code length.
            internal static void gen_codes(short[] tree, int max_code, short[] bl_count) {
                var next_code = new short[InternalConstants.MAX_BITS + 1]; // next code value for each bit length
                short code = 0; // running code value
                int bits; // bit index
                int n; // code index

                // The distribution counts are first used to generate the code values
                // without bit reversal.
                for (bits = 1; bits <= InternalConstants.MAX_BITS; bits++)
                    unchecked {
                        next_code[bits] = code = (short)((code + bl_count[bits - 1]) << 1);
                    }

                // Check that the bit counts in bl_count are consistent. The last code
                // must be all ones.
                //Assert (code + bl_count[MAX_BITS]-1 == (1<<MAX_BITS)-1,
                //        "inconsistent bit counts");
                //Tracev((stderr,"\ngen_codes: max_code %d ", max_code));

                for (n = 0; n <= max_code; n++) {
                    int len = tree[n * 2 + 1];
                    if (len == 0)
                        continue;
                    // Now reverse the bits
                    tree[n * 2] = unchecked((short)(bi_reverse(next_code[len]++, len)));
                }
            }

            // Reverse the first len bits of a code, using straightforward code (a faster
            // method would use a table)
            // IN assertion: 1 <= len <= 15
            internal static int bi_reverse(int code, int len) {
                int res = 0;
                do {
                    res |= code & 1;
                    code >>= 1; //SharedUtils.URShift(code, 1);
                    res <<= 1;
                } while (--len > 0);
                return res >> 1;
            }
        }

        #endregion
    }
}

