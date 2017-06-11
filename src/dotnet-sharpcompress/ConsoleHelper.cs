using System;

namespace SharpCompress
{
    public static class ConsoleHelper
    {
        private class ConsoleTextPush : IDisposable
        {
            private readonly ConsoleColor _restoreColor;

            public ConsoleTextPush(ConsoleColor displayColor)
            {
                _restoreColor = Console.ForegroundColor;
                Console.ForegroundColor = displayColor;
            }

            public void Dispose()
            {
                Console.ForegroundColor = _restoreColor;
            }
        }

        public static IDisposable PushForeground(ConsoleColor color)
        {
            return new ConsoleTextPush(color);
        }
        public static IDisposable PushError()
        {
            return PushForeground(ConsoleColor.Red);
        }
    }
}