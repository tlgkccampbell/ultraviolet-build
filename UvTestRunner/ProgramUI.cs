using System;
using System.Collections.Generic;
using System.Linq;
using UvTestRunner.Services;

namespace UvTestRunner
{
    public static class ProgramUI
    {
        static ProgramUI()
        {
            for (int i = 0; i < ConsoleHeight; i++)
            {
                buffer[i] = new Char[ConsoleWidth];
            }

            Console.CursorVisible = false;
            Console.BufferWidth = Console.WindowWidth = ConsoleWidth;
            Console.BufferHeight = Console.WindowHeight = ConsoleHeight;
        }

        public static void Clear()
        {
            lock (SyncObject)
            {
                for (int y = 0; y < ConsoleHeight; y++)
                {
                    backgroundColors[y] = ConsoleColor.Black;
                    foregroundColors[y] = ConsoleColor.Gray;

                    for (int x = 0; x < ConsoleWidth; x++)
                    {
                        buffer[y][x] = ' ';
                    }
                }

                line = 0;

                UpdateFooters();
                UpdateHeaders();

                FlushBuffer();
            }
        }

        public static void ClearLine()
        {
            lock (SyncObject)
            {
                for (int x = 0; x < ConsoleWidth; x++)
                    buffer[line][x] = ' ';
            }
        }

        public static void ClearLine(Int32 line)
        {
            lock (SyncObject)
            {
                for (int x = 0; x < ConsoleWidth; x++)
                    buffer[line][x] = ' ';
            }
        }

        public static void FlushBuffer()
        {
            FlushBuffer(0, ConsoleHeight);
        }

        public static void FlushBuffer(Int32 start, Int32 count)
        {
            if (start < 0 || start >= ConsoleHeight)
                throw new ArgumentOutOfRangeException("start");

            if (count < 0 || start + count > ConsoleHeight)
                throw new ArgumentOutOfRangeException("count");

            lock (SyncObject)
            {
                for (int i = (start + count) - 1; i >= start; i--)
                {
                    Console.SetCursorPosition(0, start);

                    Console.BackgroundColor = backgroundColors[i];
                    Console.ForegroundColor = foregroundColors[i];

                    Console.Write(buffer[i]);
                    Console.MoveBufferArea(0, start, ConsoleWidth, 1, 0, i);
                }
            }
        }

        public static void FlushQueuedMessages()
        {
            lock (SyncObject)
            {
                var msgStart = 2;
                var msgEnd = ConsoleHeight - 2;
                var msgCount = msgEnd - msgStart;

                for (int i = msgStart; i < msgEnd; i++)
                    ClearLine(i);

                var msgLines = queuedMessages.SelectMany(msg => SplitMessageIntoLines(msg, MaxQueuedMessageWidth))
                    .Select(line => line.PadRight(ConsoleWidth)).ToArray();
                var msgLinesToShow = Math.Min(msgLines.Length, MaxQueuedMessageCount);

                Console.SetCursorPosition(0, 2);
                Console.ResetColor();
                Console.Write(String.Join(String.Empty, msgLines, msgLines.Length - msgLinesToShow, msgLinesToShow));
            }
        }

        public static void Write(Int32 x, Int32 y, String message)
        {
            if (x < 0 || x >= ConsoleWidth)
                throw new ArgumentOutOfRangeException("x");

            if (y < 0 || y >= ConsoleHeight)
                throw new ArgumentOutOfRangeException("y");

            lock (SyncObject)
            {
                for (int i = 0; i < message.Length; i++)
                {
                    buffer[y][x] = message[i];

                    if (++x >= ConsoleWidth)
                    {
                        x = 0;

                        if (++y >= ConsoleHeight)
                            break;
                    }
                }
            }
        }

        public static void MoveToLine(Int32 line)
        {
            if (line < 0 || line >= ConsoleHeight)
                throw new ArgumentOutOfRangeException("line");

            lock (SyncObject)
            {
                ProgramUI.line = line;
            }
        }

        public static void WriteBanner(ConsoleColor backgroundColor, ConsoleColor foregroundColor, String text)
        {
            backgroundColors[line] = backgroundColor;
            foregroundColors[line] = foregroundColor;

            ClearLine();
            WriteCentered(text);
        }

        public static void WriteCentered(String text)
        {
            lock (SyncObject)
            {
                var x = (ConsoleWidth - text.Length) / 2;
                var y = line;

                for (int i = 0; i < text.Length; i++)
                {
                    if (x >= ConsoleWidth)
                        break;

                    if (x >= 0)
                    {
                        buffer[y][x] = text[i];
                    }
                    x++;
                }
            }
        }

        public static void WriteLeftJustified(String text)
        {
            lock (SyncObject)
            {
                var x = 0;
                var y = line;

                for (int i = 0; i < text.Length; i++)
                {
                    if (x >= ConsoleWidth)
                        break;

                    buffer[y][x++] = text[i];
                }
            }
        }

        public static void WriteRightJustified(String text)
        {
            lock (SyncObject)
            {
                var x = ConsoleWidth - text.Length;
                var y = line;

                for (int i = 0; i < text.Length; i++)
                {
                    if (x >= ConsoleWidth)
                        break;

                    if (x >= 0)
                    {
                        buffer[y][x] = text[i];
                    }
                    x++;
                }
            }
        }

        public static void UpdateHeaders(Boolean flushHeader = false, Boolean flushSubHeader = false)
        {
            lock (SyncObject)
            {
                MoveToLine(0);
                WriteBanner(Settings.Default.ColorBright, ConsoleColor.White, "UvTestRunner");

                MoveToLine(1);
                WriteBanner(Settings.Default.ColorDark, ConsoleColor.White, String.Empty);
                WriteRightJustified(TestRunQueueService.Instance.QueueLength + " test runs queued ");

                if (flushHeader)
                    FlushBuffer(0, 1);

                if (flushSubHeader)
                    FlushBuffer(1, 1);
            }
        }

        public static void UpdateFooters()
        {
            lock (SyncObject)
            {
                MoveToLine(ConsoleHeight - 1);
                WriteBanner(Settings.Default.ColorBright, ConsoleColor.White, String.Empty);
            }
        }

        public static void QueueMessage(String message)
        {
            lock (SyncObject)
            {
                while (queuedMessages.Count >= MaxQueuedMessageCount)
                    queuedMessages.RemoveFirst();

                queuedMessages.AddLast(String.Format(" * {0}", message));
            }
        }

        public static void HandleTestRunEnqueued(Int64 id, String workingDirectory)
        {
            UpdateHeaders(flushSubHeader: true);
            QueueMessage(String.Format("Enqueued test run #{0} [{1}] for processing.", id, workingDirectory));
            FlushQueuedMessages();
        }

        public static void HandleTestRunConsumed(Int64 id, String workingDirectory)
        {
            UpdateHeaders(flushSubHeader: true);
            QueueMessage(String.Format("Started test run #{0} [{1}] at {2:yyyy-MM-dd HH:mm:ss}.", id, workingDirectory, DateTime.Now));
            FlushQueuedMessages();
        }

        public static void HandleTestRunComplete(Int64 id, String workingDirectory)
        {
            UpdateHeaders(flushSubHeader: true);
            QueueMessage(String.Format("Finished test run #{0} [{1}] at {2:yyyy-MM-dd HH:mm:ss}.", id, workingDirectory, DateTime.Now));
            FlushQueuedMessages();
        }

        public static void HandleTestRunDoesNotExist(Int64 id)
        {
            UpdateHeaders(flushSubHeader: true);
            QueueMessage(String.Format("Attempted to process test run #{0}, but it no longer exists.", id));
            FlushQueuedMessages();
        }

        private static IEnumerable<String> SplitMessageIntoLines(String message, Int32 size)
        {
            for (int i = 0; i < message.Length; i += size)
            {
                yield return message.Substring(i, Math.Min(size, message.Length - i));
            }
        }

        // Thread synchronization object.
        private static readonly Object SyncObject = new Object();

        // A set of buffers representing the console.
        private static readonly Char[][] buffer = new Char[ConsoleHeight][];
        private static readonly ConsoleColor[] foregroundColors = new ConsoleColor[ConsoleHeight];
        private static readonly ConsoleColor[] backgroundColors = new ConsoleColor[ConsoleWidth];
        private const Int32 ConsoleWidth = 80;
        private const Int32 ConsoleHeight = 25;
        private static Int32 line;

        // The set of messages which are currently being displayed.
        private const Int32 MaxQueuedMessageCount = ConsoleHeight - 3;
        private const Int32 MaxQueuedMessageWidth = ConsoleWidth;
        private static readonly LinkedList<String> queuedMessages = new LinkedList<String>();
    }
}
