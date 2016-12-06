using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;
using System.Diagnostics;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Joins interactions with rewards locally, using an in-memory cache. The public API of this
    /// logger is thread-safe.
    /// </summary>
    /// <typeparam name="TContext">The Context type</typeparam>
    /// <typeparam name="TAction">The Action type</typeparam>
    internal class InMemoryLogger<TContext, TAction> : IRecorder<TContext, TAction>, ILogger, IDisposable
    {
        /// <summary>
        /// An exploration datapoint, containing the context, action, probability, and reward
        /// of a given event. In other words, the <x,a,r,p> tuple.
        /// </summary>
        internal class DataPoint : IEvent
        {
            public string Key { get; set; }
            // Contains the context, action, and probability (at least)
            public Interaction InteractData { get; set; }
            public float Reward { get; set; }
            // TODO: This can be used to support custom reward functions
            public ConcurrentBag<object> Outcomes = new ConcurrentBag<object>();
            // Used to control expiration for fixed-duration events
            public DateTime ExpiresAt = DateTime.MaxValue;
        }

        private bool disposed = false;

        // The experimental unit duration, or how long to wait for reward information before 
        // completing an event (TimeSpan.MaxValue means wait forever)
        private TimeSpan experimentalUnit;
        // Stores pending (incomplete) events, either for a fixed experimental unit duration or for 
        // manual completion
        private ConcurrentDictionary<string, DataPoint> pendingData;
        // A queue and timer to expire events based on the experimental unit duration
        private ConcurrentQueue<DataPoint> completionQueue;
        private Timer completionTimer;
        // Stores expired or manually-completed events 
        private ConcurrentDictionary<string, DataPoint> completeData = new ConcurrentDictionary<string, DataPoint>();
        // If no reward information is received, this value will be used
        private float defaultReward;

        /// <summary>
        /// Creates a new in-memory logger for exploration data
        /// </summary>
        /// <param name="expUnit">The experimental unit duration, or how long to wait for reward 
        /// information. Set this to TimeSpan.MaxValue for infinite duration (events never expire
        /// and must be completed manually.</param>
        /// <param name="defaultReward">Reward value to use when no reward signal is received</param>
        public InMemoryLogger(TimeSpan expUnit, float defaultReward = (float)0.0)
        {
            this.experimentalUnit = expUnit;
            this.defaultReward = defaultReward;
            pendingData = new ConcurrentDictionary<string,DataPoint>();
            // We only need the completion queue/timer if events are being completed automatically
            // (by experimental duration)
            if (experimentalUnit != TimeSpan.MaxValue)
            {
                completionQueue = new ConcurrentQueue<DataPoint>();
                completionTimer = new Timer(experimentalUnit.TotalMilliseconds);
                completionTimer.Elapsed += completeExpiredEvents;
                completionTimer.AutoReset = false;
                completionTimer.Start();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposed) { return; }

            if (disposing)
            {
                if (completionTimer != null)
                {
                    completionTimer.Dispose();
                    completionTimer = null;
                }
            }
            disposed = true;
        }

        private void completeExpiredEvents(object sender, ElapsedEventArgs e)
        {
            DataPoint dp;
            // At most one call to this handler can be in progress (and no one else dequeues
            // events), so we can safely dequeue
            while (completionQueue.TryPeek(out dp) && dp.ExpiresAt <= DateTime.Now)
            {
                if (completionQueue.TryDequeue(out dp))
                {
                    DataPoint dpActual;
                    bool keyLost = false;
                    // The key should exist unless the event was manually completed 
                    if (!pendingData.TryGetValue(dp.Key, out dpActual))
                    {
                        keyLost = true;
                    }
                    else
                    {
                        if (dp.Equals(dpActual))
                        {
                            // The removal must succeed, otherwise some corruption has occurred 
                            if (!pendingData.TryRemove(dp.Key, out dpActual))
                            {
                                keyLost = true;
                            }
                            else
                            {
                                completeData.AddOrUpdate(dp.Key, dpActual, (k, oldDp) => dpActual);
                            }
                        }
                        else
                        {
                            Trace.TraceWarning("Event with key {0} points to a new object, not completing", dp.Key);
                        }
                    }
                    if (keyLost)
                    {
                        Trace.TraceWarning("Event with key {0} missing (was it completed manually?)", dp.Key);
                    }
                }
            }

            // Reschedule the timer
            completionTimer.Interval = (dp != null) ? (dp.ExpiresAt - DateTime.Now).TotalMilliseconds : experimentalUnit.TotalMilliseconds;
            completionTimer.Start();
        }

        public void Record(TContext context, TAction value, object explorerState, object mapperState, string uniqueKey)
        {
            DataPoint dp = new DataPoint
            {
                Key = uniqueKey,
                InteractData = new Interaction
                {
                    Key = uniqueKey,
                    Context = context,
                    Value = value,
                    ExplorerState = explorerState,
                    MapperState = mapperState
                },
                Reward = defaultReward
            };
            if (experimentalUnit != TimeSpan.MaxValue)
            {
                dp.ExpiresAt = DateTime.Now.Add(experimentalUnit);
                // Add the datapoint to the dictionary of pending events 
                pendingData.AddOrUpdate(dp.Key, dp, (k, oldDp) => dp);
                // Also add it to the completion queue so it is expired at the right time
                completionQueue.Enqueue(dp);
                if (completionQueue.Count == 0)
                {
                    // We might overwrite a valid interval due to concurrency, but the worst that 
                    // happens is some events are completed a little later than they should (which
                    // is already possible due to tick resolution)
                    completionTimer.Interval = experimentalUnit.TotalMilliseconds;
                }
            }
            else
            {
                // Add the datapoint to the dictionary of pending events 
                pendingData.AddOrUpdate(dp.Key, dp, (k, oldDp) => dp);
            }
        }

        public void ReportReward(string uniqueKey, float reward)
        {
            DataPoint dp;
            if (pendingData.TryGetValue(uniqueKey, out dp))
            {
                // Guaranteed atomic by the language
                dp.Reward = reward;
            }
            else
            {
                Trace.TraceWarning("Could not find event with key {0}", uniqueKey);
            }
        }

        public void ReportRewardAndComplete(string uniqueKey, float reward)
        {
            DataPoint dp;
            // Attempt to remove and complete the event
            if (pendingData.TryRemove(uniqueKey, out dp))
            {
                // Guaranteed atomic by the language
                dp.Reward = reward;
                completeData.AddOrUpdate(dp.Key, dp, (k, oldDp) => dp);
            }
            else
            {
                Trace.TraceWarning("Could not find event with key {0}", uniqueKey);
            }
        }

        public void ReportOutcome(string uniqueKey, object outcome)
        {
            DataPoint dp;
            if (pendingData.TryGetValue(uniqueKey, out dp))
            {
                // Added to a concurrent bag, so thread-safe
                dp.Outcomes.Add(outcome);
            }
        }

        public DataPoint[] FlushCompleteEvents()
        {
            DataPoint temp;
            // Get a snapshot of the complete events, then iterate through and try to remove each
            // one, returning only the successfully removed ones. This ensures each data point is
            // returned at most once.
            var datapoints = completeData.ToArray();
            List<DataPoint> removed = new List<DataPoint>();
            foreach (var dp in datapoints)
            {
                if (completeData.TryRemove(dp.Key, out temp))
                {
                    removed.Add(temp);
                }
            }
            return removed.ToArray();
        }
    }
}
