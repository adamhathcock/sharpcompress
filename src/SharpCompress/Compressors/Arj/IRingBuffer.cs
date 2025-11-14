namespace SharpCompress.Compressors.Arj
{
    public interface IRingBuffer
    {
        int BufferSize { get; }

        int Cursor { get; }
        void SetCursor(int pos);

        void Push(byte value);

        HistoryIterator IterFromOffset(int offset);
        HistoryIterator IterFromPos(int pos);

        byte this[int index] { get; }
    }
}
