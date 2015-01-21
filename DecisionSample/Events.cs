using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;

namespace DecisionSample
{
    public enum EventType
    { 
        Interaction = 0,
        Observation
    }
    public interface IEvent
    {
        [JsonProperty(PropertyName = "t")]
        EventType Type { get; }

        [JsonProperty(PropertyName = "i")]
        string ID { get; set; }

        int Measure();
    }

    public class Interaction : IEvent
    {
        public EventType Type
        {
            get
            {
                return EventType.Interaction;
            }
        }

        public string ID { get; set; }

        [JsonProperty(PropertyName = "a")]
        public int Action { get; set; }

        [JsonProperty(PropertyName = "p")]
        public double Probability { get; set; }

        [JsonProperty(PropertyName = "c")]
        public string Context { get; set; }


        public int Measure()
        {
            // TODO: Reflection? potential perf hit
            return 
                sizeof(EventType) +
                sizeof(int) + 
                sizeof(double) + 
                Encoding.Unicode.GetByteCount(ID) +
                Encoding.Unicode.GetByteCount(Context);
        }
    }

    public class Observation : IEvent
    {
        public EventType Type
        {
            get
            {
                return EventType.Observation;
            }
        }

        public string ID { get; set; }

        [JsonProperty(PropertyName = "v")]
        public string Value { get; set; }

        public int Measure()
        {
            // TODO: Reflection? potential perf hit
            return
                sizeof(EventType) +
                Encoding.Unicode.GetByteCount(ID) +
                Encoding.Unicode.GetByteCount(Value);
        }
    }

    public class EventBatch
    {
        [JsonProperty(PropertyName = "e")]
        public IList<IEvent> Events { get; set; }

        [JsonProperty(PropertyName = "d")]
        public long ExperimentalUnitDurationInSeconds { get; set; }
    }
}
