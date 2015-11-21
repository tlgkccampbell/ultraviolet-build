using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Owin.Hosting;
using UvTestRunner.Services;

namespace UvTestRunner
{
    public class Program
    {
        private static void Main(String[] args)
        {
            CreateEventLog();
            LogInfo("UvTestRunner was started.");

            Console.Title = String.Format("UvTestRunner [{0}]", Settings.Default.WebApiUrl);
            Console.CancelKeyPress += Console_CancelKeyPress;

            ProgramUI.Clear();
            ProgramUI.QueueMessage("UvTestRunner was started.");
            ProgramUI.FlushQueuedMessages();

            running = true;

            ThreadPool.QueueUserWorkItem(ConsumeEnqueuedTestRuns);

            using (WebApp.Start<Startup>(url: Settings.Default.WebApiUrl))
            {
                while (running)
                {
                    Thread.Sleep(100);

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.P)
                        {
                            var paused = TestRunQueueService.Instance.QueueIsPaused = !TestRunQueueService.Instance.QueueIsPaused;

                            ProgramUI.QueueMessage(paused ? "Queue processing paused by user." : "Queue processing resumed by user.");
                            ProgramUI.FlushQueuedMessages();
                        }
                    }
                }
            }

            LogInfo("UvTestRunner was closed.");
        }

        private static void CreateEventLog()
        {
            if (EventLog.SourceExists("UvTestRunner"))
                return;

            try
            {
                EventLog.CreateEventSource("UvTestRunner", "Application");
            }
            catch (ArgumentException) { }
        }

        private static void LogInfo(String message)
        {
            using (var log = new EventLog("Application"))
            {
                log.Source = "UvTestRunner";
                log.WriteEntry(message, EventLogEntryType.Information);
            }
        }

        private static void LogWarning(String message)
        {
            using (var log = new EventLog("Application"))
            {
                log.Source = "UvTestRunner";
                log.WriteEntry(message, EventLogEntryType.Warning);
            }
        }

        private static void LogError(String message)
        {
            using (var log = new EventLog("Application"))
            {
                log.Source = "UvTestRunner";
                log.WriteEntry(message, EventLogEntryType.Error);
            }
        }
        
        private static void Console_CancelKeyPress(Object sender, ConsoleCancelEventArgs e)
        {
            lock (SyncObject)
            {
                running = false;
            }
            e.Cancel = true;
        }

        private static void ConsumeEnqueuedTestRuns(Object state)
        {
            while (true)
            {
                lock (SyncObject)
                {
                    if (!running)
                        break;
                }

                TestRunQueueService.Instance.Consume();
                Thread.Sleep(100);
            }
        }

        private static readonly Object SyncObject = new Object();
        private static Boolean running;
    }
}
