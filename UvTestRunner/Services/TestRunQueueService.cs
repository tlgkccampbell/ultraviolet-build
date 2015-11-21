using System;
using System.Collections.Generic;

namespace UvTestRunner.Services
{
    /// <summary>
    /// Represents a service which manages a queue of test run instances.
    /// </summary>
    public class TestRunQueueService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunQueueService"/> class.
        /// </summary>
        private TestRunQueueService() { }

        /// <summary>
        /// Creates a new pending test run.
        /// </summary>
        /// <param name="workingDirectory">The current working directory for the build agent.</param>
        /// <returns>The identifier of the test run within the database.</returns>
        public Int64 Create(String workingDirectory)
        {
            var id = testRunnerService.CreateTestRun(workingDirectory);
            Enqueue(id, workingDirectory);
            return id;
        }

        /// <summary>
        /// Enqueues a test run for processing.
        /// </summary>
        /// <param name="workingDirectory">The current working directory for the build agent.</param>
        /// <param name="id">The test run identifier to enqueue.</param>
        public void Enqueue(Int64 id, String workingDirectory)
        {
            lock (queue)
            {
                queue.Enqueue(id);
                ProgramUI.HandleTestRunEnqueued(id, workingDirectory);
            }
        }

        /// <summary>
        /// Spawns a test run from the queue, if a run is pending.
        /// </summary>
        public void Consume()
        {
            Int64 id;
            lock (queue)
            {
                if (queueIsPaused)
                    return;

                if (queue.Count == 0)
                    return;

                id = queue.Dequeue();
            }

            var testRun = testRunnerService.GetByID(id);
            if (testRun == null)
            {
                ProgramUI.HandleTestRunDoesNotExist(id);
                return;
            }
            ProgramUI.HandleTestRunConsumed(testRun.ID, testRun.WorkingDirectory);
            testRunnerService.Run(testRun);
            ProgramUI.HandleTestRunComplete(testRun.ID, testRun.WorkingDirectory);
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="TestRunQueueService"/> class.
        /// </summary>
        public static TestRunQueueService Instance
        {
            get { return instance; }
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether the queue is paused.
        /// </summary>
        public Boolean QueueIsPaused
        {
            get { lock (queue) { return queueIsPaused; } }
            set { lock (queue) { queueIsPaused = value; } }
        }

        /// <summary>
        /// Gets a value indicating whether the queue currently has any pending items.
        /// </summary>
        public Boolean QueueHasItems
        {
            get { lock(queue) { return queue.Count > 0; } }
        }

        /// <summary>
        /// Gets the number of items in the queue.
        /// </summary>
        public Int64 QueueLength
        {
            get { lock (queue) { return queue.Count; } }
        }

        // The class' singleton instance.
        private static readonly TestRunQueueService instance = new TestRunQueueService();

        // State values.
        private readonly Queue<Int64> queue = new Queue<Int64>();
        private Boolean queueIsPaused;

        // The service responsible for running tests.
        private readonly TestRunService testRunnerService = new TestRunService();
    }
}
