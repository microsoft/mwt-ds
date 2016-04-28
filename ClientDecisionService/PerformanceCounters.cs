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

                var counterCollection = new CounterCreationDataCollection();

                counterCollection.Add(new CounterCreationData() { CounterName = "ExamplesQueue", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                counterCollection.Add(new CounterCreationData() { CounterName = "ExamplesTotal", CounterHelp = "", CounterType = PerformanceCounterType.NumberOfItems64 });
                counterCollection.Add(new CounterCreationData() { CounterName = "ExamplesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond32 });
                counterCollection.Add(new CounterCreationData() { CounterName = "ExamplesBytesSec", CounterHelp = "", CounterType = PerformanceCounterType.RateOfCountsPerSecond64 });
                counterCollection.Add(new CounterCreationData() { CounterName = "AverageExampleSize", CounterHelp = "", CounterType = PerformanceCounterType.AverageCount64 });
                counterCollection.Add(new CounterCreationData() { CounterName = "AverageExampleSizeBase", CounterHelp = "", CounterType = PerformanceCounterType.AverageBase });

                PerformanceCounterCategory.Create(category, "Decision Service Client", PerformanceCounterCategoryType.MultiInstance, counterCollection);
            }
            catch (Exception e)
            {
                Trace.TraceError("Failed to initialize performance counters: {0}. {1}", e.Message, e.StackTrace);
            }
        }

        private readonly bool initialized;

        private PerformanceCounter examplesQueue;
        private PerformanceCounter examplesTotal;
        private PerformanceCounter examplesPerSec;
        private PerformanceCounter examplesBytesPerSec;
        private PerformanceCounter averageExampleSize;
        private PerformanceCounter averageExampleSizeBase;

        internal PerformanceCounters(string mwtToken)
        {
            try 
	        {	        
		        this.examplesQueue = new PerformanceCounter(category, "ExamplesQueue", mwtToken, false);
                this.examplesTotal = new PerformanceCounter(category, "ExamplesTotal", mwtToken, false);
                this.examplesPerSec = new PerformanceCounter(category, "ExamplesSec", mwtToken, false);
                this.examplesBytesPerSec = new PerformanceCounter(category, "ExamplesBytesSec", mwtToken, false);
                this.averageExampleSize = new PerformanceCounter(category, "AverageExampleSize", mwtToken, false);
                this.averageExampleSizeBase = new PerformanceCounter(category, "AverageExampleSizeBase", mwtToken, false);
                
                this.examplesQueue.RawValue = 0;
                this.examplesTotal.RawValue = 0;

                this.initialized = true;
	        }
	        catch (Exception e)
	        {
                this.initialized = false;
		        Trace.TraceError("Failed to initialize performance counters: {0}. {1}", e.Message, e.StackTrace);
	        }
        }

        internal void ReportExample(int eventCount, int sumSize)
        {
            if (this.initialized)
            {
                this.examplesQueue.IncrementBy(-eventCount);

                this.examplesTotal.IncrementBy(eventCount);
                this.examplesPerSec.IncrementBy(eventCount);

                this.examplesBytesPerSec.IncrementBy(sumSize);

                this.averageExampleSize.IncrementBy(sumSize);
                this.averageExampleSizeBase.IncrementBy(eventCount);
            }
        }

        internal void ReportExampleQueue(int queueSize)
        {
            if (this.initialized)
            {
                this.examplesQueue.RawValue = queueSize;
            }
        }
    }
}
