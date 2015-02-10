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
        [JsonConverter(typeof(RawStringConverter))]
        public string Context { get; set; }

        // TODO : change to measure by serialized string

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

    public class ExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "i")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "v")]
        public object Value { get; set; }
    }

    public class EventBatch
    {
        [JsonProperty(PropertyName = "i")]
        public System.Guid ID { get; set; }

        [JsonProperty(PropertyName = "j")]
        public IList<string> JsonEvents { get; set; }
    }
}
