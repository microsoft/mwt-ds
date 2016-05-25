using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class PerformanceCounters
    {
        private const string category = "Decision Service Client";

        static PerformanceCounters()
        {
            try
            {
                // only if the counters below are changed
                //if (PerformanceCounterCategory.Exists(category))
                //    PerformanceCounterCategory.Delete(category);

                if (!PerformanceCounterCategory.Exists(category))
                {
                    var counterCollection = new CounterCreationDataCollection();

                    counterCollection.Add(new CounterCreationData() { CounterName = "InteractionExamplesQueue", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "InteractionExamplesTotal", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "InteractionExamplesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond32 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "InteractionExamplesBytesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "AverageInteractionExampleSize", CounterHelp = "", CounterType = PerformanceCounterType.AverageCount64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "AverageInteractionExampleSizeBase", CounterHelp = "", CounterType = PerformanceCounterType.AverageBase });

                    counterCollection.Add(new CounterCreationData() { CounterName = "ObservationExamplesQueue", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "ObservationExamplesTotal", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "ObservationExamplesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond32 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "ObservationExamplesBytesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "AverageObservationExampleSize", CounterHelp = "", CounterType = PerformanceCounterType.AverageCount64 });
                    counterCollection.Add(new CounterCreationData() { CounterName = "AverageObservationExampleSizeBase", CounterHelp = "", CounterType = PerformanceCounterType.AverageBase });

                    PerformanceCounterCategory.Create(category, "Decision Service Client", PerformanceCounterCategoryType.MultiInstance, counterCollection);
                }
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Failed to initialize performance counters. Run the process elevated. {0}.", e.Message);
            }
        }

        private readonly bool initialized;

        private PerformanceCounter interactionExamplesQueue;
        private PerformanceCounter interactionExamplesTotal;
        private PerformanceCounter interactionExamplesPerSec;
        private PerformanceCounter interactionExamplesBytesPerSec;
        private PerformanceCounter averageInteractionExampleSize;
        private PerformanceCounter averageInteractionExampleSizeBase;

        private PerformanceCounter observationExamplesQueue;
        private PerformanceCounter observationExamplesTotal;
        private PerformanceCounter observationExamplesPerSec;
        private PerformanceCounter observationExamplesBytesPerSec;
        private PerformanceCounter averageObservationExampleSize;
        private PerformanceCounter averageObservationExampleSizeBase;


        internal PerformanceCounters(string mwtToken)
        {
            try 
	        {	        
		        this.interactionExamplesQueue = new PerformanceCounter(category, "InteractionExamplesQueue", mwtToken, false);
                this.interactionExamplesTotal = new PerformanceCounter(category, "InteractionExamplesTotal", mwtToken, false);
                this.interactionExamplesPerSec = new PerformanceCounter(category, "InteractionExamplesSec", mwtToken, false);
                this.interactionExamplesBytesPerSec = new PerformanceCounter(category, "InteractionExamplesBytesSec", mwtToken, false);
                this.averageInteractionExampleSize = new PerformanceCounter(category, "AverageInteractionExampleSize", mwtToken, false);
                this.averageInteractionExampleSizeBase = new PerformanceCounter(category, "AverageInteractionExampleSizeBase", mwtToken, false);
                
                this.interactionExamplesQueue.RawValue = 0;
                this.interactionExamplesTotal.RawValue = 0;

                this.observationExamplesQueue = new PerformanceCounter(category, "ObservationExamplesQueue", mwtToken, false);
                this.observationExamplesTotal = new PerformanceCounter(category, "ObservationExamplesTotal", mwtToken, false);
                this.observationExamplesPerSec = new PerformanceCounter(category, "ObservationExamplesSec", mwtToken, false);
                this.observationExamplesBytesPerSec = new PerformanceCounter(category, "ObservationExamplesBytesSec", mwtToken, false);
                this.averageObservationExampleSize = new PerformanceCounter(category, "AverageObservationExampleSize", mwtToken, false);
                this.averageObservationExampleSizeBase = new PerformanceCounter(category, "AverageObservationExampleSizeBase", mwtToken, false);

                this.observationExamplesQueue.RawValue = 0;
                this.observationExamplesTotal.RawValue = 0;

                this.initialized = true;
	        }
	        catch (Exception e)
	        {
                this.initialized = false;
		        Trace.TraceError("Failed to initialize performance counters: {0}. {1}", e.Message, e.StackTrace);
	        }
        }

        internal void ReportInteraction(int eventCount, int sumSize)
        {
            if (this.initialized)
            {
                this.interactionExamplesQueue.IncrementBy(-eventCount);

                this.interactionExamplesTotal.IncrementBy(eventCount);
                this.interactionExamplesPerSec.IncrementBy(eventCount);

                this.interactionExamplesBytesPerSec.IncrementBy(sumSize);

                this.averageInteractionExampleSize.IncrementBy(sumSize);
                this.averageInteractionExampleSizeBase.IncrementBy(eventCount);
            }
        }

        internal void ReportObservation(int eventCount, int sumSize)
        {
            if (this.initialized)
            {
                this.observationExamplesQueue.IncrementBy(-eventCount);

                this.observationExamplesTotal.IncrementBy(eventCount);
                this.observationExamplesPerSec.IncrementBy(eventCount);

                this.observationExamplesBytesPerSec.IncrementBy(sumSize);

                this.averageObservationExampleSize.IncrementBy(sumSize);
                this.averageObservationExampleSizeBase.IncrementBy(eventCount);
            }
        }

        internal void ReportInteractionExampleQueue(int queueSize)
        {
            if (this.initialized)
            {
                this.interactionExamplesQueue.RawValue = queueSize;
            }
        }

        internal void ReportObservationExampleQueue(int queueSize)
        {
            if (this.initialized)
            {
                this.observationExamplesQueue.RawValue = queueSize;
            }
        }
    }
}
