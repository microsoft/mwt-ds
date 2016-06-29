using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
        /// <summary>
    /// Performance counters reporting various metrics.
    /// </summary>
    public sealed class PerformanceCounters : IDisposable
    {
        public class PerformanceCounterTypeAttribute : Attribute
        {
            public PerformanceCounterTypeAttribute(PerformanceCounterType type, string name = null)
            {
                this.Type = type;
                this.Name = name;
            }

            public PerformanceCounterType Type { get; private set; }

            public string Name { get; private set; }
        }

        private const string category = "Decision Service Client";

        static PerformanceCounters()
        {
            try
            {
                if (PerformanceCounterCategory.Exists(category))
                    PerformanceCounterCategory.Delete(category);

                // order to be sure that *Base follows counter
                var props = typeof(PerformanceCounters)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(PerformanceCounter))
                    .OrderBy(p => p.Name).ToList();

                var counterCollection = new CounterCreationDataCollection();

                foreach (var p in props)
                {
                    var attr = (PerformanceCounterTypeAttribute)p.GetCustomAttributes(typeof(PerformanceCounterTypeAttribute), true).First();
                    counterCollection.Add(new CounterCreationData() { CounterName = p.Name, CounterHelp = string.Empty, CounterType = attr.Type });
                }

                PerformanceCounterCategory.Create(category, "Online Trainer Perf Counters", PerformanceCounterCategoryType.MultiInstance, counterCollection);
            }
            catch (Exception e)
            {
                new TelemetryClient().TrackException(e);
            }
        }

        private readonly bool initialized;

        public PerformanceCounters(string instance)
        {
            try
            {
                var perfCollectorModule = new PerformanceCollectorModule();
                var props = typeof(PerformanceCounters)
                    .GetProperties()
                    .Where(p => p.PropertyType == typeof(PerformanceCounter));

                var all = new List<PerformanceCounter>();
                foreach (var p in props)
                {
                    var counter = new PerformanceCounter(category, p.Name, instance, false);
                    p.SetValue(this, counter);
                    counter.RawValue = 0;
                    all.Add(counter);

                    if (!p.Name.EndsWith("Base", StringComparison.Ordinal))
                    {
                        var perfCounterSpec = string.Format(CultureInfo.InvariantCulture, "\\{0}({1})\\{2}", category, instance, p.Name);
                        var reportAs = p.Name
                            .Replace('_', ' ')
                            .Replace("Per", "/");

                        perfCollectorModule.Counters.Add(new PerformanceCounterCollectionRequest(perfCounterSpec, reportAs));
                    }
                }

                perfCollectorModule.Initialize(TelemetryConfiguration.Active);
            }
            catch (Exception e)
            {
                this.initialized = false;
                new TelemetryClient().TrackException(e);
            }

            this.initialized = true;
        }

        public void Dispose()
        {
            foreach (var p in typeof(PerformanceCounters).GetProperties())
            {
                var perfCounter = (IDisposable)p.GetValue(this);
                
                if (perfCounter != null)
                {
                    perfCounter.Dispose();
                    p.SetValue(this, null);
                }
            }
        }

        [PerformanceCounterType(PerformanceCounterType.NumberOfItems64)]
        public PerformanceCounter InteractionExamplesQueue {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.NumberOfItems64)]
        public PerformanceCounter InteractionExamplesTotal {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.RateOfCountsPerSecond32)]
        public PerformanceCounter InteractionExamplesPerSec {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.RateOfCountsPerSecond64)]
        public PerformanceCounter InteractionExamplesBytesPerSec { get; private set; }

        [PerformanceCounterType(PerformanceCounterType.AverageCount64)]
        public PerformanceCounter AverageInteractionExampleSize {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.AverageBase)]
        public PerformanceCounter AverageInteractionExampleSizeBase {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.NumberOfItems64)]
        public PerformanceCounter ObservationExamplesQueue {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.NumberOfItems64)]
        public PerformanceCounter ObservationExamplesTotal {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.RateOfCountsPerSecond32)]
        public PerformanceCounter ObservationExamplesPerSec { get; private set; }

        [PerformanceCounterType(PerformanceCounterType.RateOfCountsPerSecond64)]
        public PerformanceCounter ObservationExamplesBytesPerSec { get; private set; }

        [PerformanceCounterType(PerformanceCounterType.AverageCount64)]
        public PerformanceCounter AverageObservationExampleSize {get; private set; }

        [PerformanceCounterType(PerformanceCounterType.AverageBase)]
        public PerformanceCounter AverageObservationExampleSizeBase {get; private set; }


        internal void ReportInteraction(int eventCount, int sumSize)
        {
            if (this.initialized)
            {
                this.InteractionExamplesQueue.IncrementBy(-eventCount);

                this.InteractionExamplesTotal.IncrementBy(eventCount);
                this.InteractionExamplesPerSec.IncrementBy(eventCount);

                this.InteractionExamplesBytesPerSec.IncrementBy(sumSize);

                this.AverageInteractionExampleSize.IncrementBy(sumSize);
                this.AverageInteractionExampleSizeBase.IncrementBy(eventCount);
            }
        }

        internal void ReportObservation(int eventCount, int sumSize)
        {
            if (this.initialized)
            {
                this.ObservationExamplesQueue.IncrementBy(-eventCount);

                this.ObservationExamplesTotal.IncrementBy(eventCount);
                this.ObservationExamplesPerSec.IncrementBy(eventCount);

                this.ObservationExamplesBytesPerSec.IncrementBy(sumSize);

                this.AverageObservationExampleSize.IncrementBy(sumSize);
                this.AverageObservationExampleSizeBase.IncrementBy(eventCount);
            }
        }

        internal void ReportInteractionExampleQueue(int queueSize)
        {
            if (this.initialized)
            {
                this.InteractionExamplesQueue.RawValue = queueSize;
            }
        }

        internal void ReportObservationExampleQueue(int queueSize)
        {
            if (this.initialized)
            {
                this.ObservationExamplesQueue.RawValue = queueSize;
            }
        }
    }
}
