using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    /// <summary>
    /// Represents the type of application event.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Represents an interaction with a single action.
        /// Interactions are generated at the point of decision-making and are usually represented as a 4-tuple of
        /// (chosen action(s), probability, context, key)
        /// </summary>
        SingleActionInteraction = 0,

        /// <summary>
        /// Observations represent observed outcomes that are associated with interactions.
        /// These, for example, can be as simple as a single number indicating whether the outcome was positive or negative,
        /// to more generic structure which can be later used for reward metric analysis.
        /// </summary>
        Observation,

        /// <summary>
        /// Represents an interaction with multiple actions.
        /// Interactions are generated at the point of decision-making and are usually represented as a 4-tuple of
        /// (chosen action(s), probability, context, key)
        /// </summary>
        MultiActionInteraction
    }

    /// <summary>
    /// Base interface for an application event.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// The type of event.
        /// </summary>
        [JsonProperty(PropertyName = "t")]
        EventType Type { get; }

        /// <summary>
        /// The unique experimental unit key that this event belongs to.
        /// </summary>
        [JsonIgnore]
        string Key { get; set; }

        /// <summary>
        /// The unique time stamp of this event.
        /// </summary>
        [JsonIgnore]
        DateTime TimeStamp { get; set; }
    }

    /// <summary>
    /// Represents an interaction event that are usually represented as a 4-tuple of
    /// (chosen action(s), probability, context, key).
    /// </summary>
    public abstract class Interaction : IEvent
    {
        /// <summary>
        /// The type of event.
        /// </summary>
        public abstract EventType Type { get; }

        /// <summary>
        /// Gets or sets the unique experimental unit key that this event belongs to.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the time stamp of the event.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the probability of choosing the action (before it was chosen).
        /// </summary>
        [JsonProperty(PropertyName = "p")]
        public double Probability { get; set; }

        /// <summary>
        /// Gets or sets the context structure with relevant information for the current interaction.
        /// </summary>
        [JsonProperty(PropertyName = "c", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(RawStringConverter))]
        public object Context { get; set; }

        /// <summary>
        /// The Id of the model used to make predictions/decisions, if any exists at decision time.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ModelId { get; set; }

        /// <summary>
        /// Indicates whether the decision was generated purely from exploration (vs. exploitation).
        /// This value is only relevant to Epsilon Greedy or Tau First algorithms.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsExplore { get; set; }
    }

    /// <summary>
    /// Represents an interaction event with a single action.
    /// </summary>
    public class SingleActionInteraction : Interaction
    {
        /// <summary>
        /// Gets the type of event.
        /// </summary>
        public override EventType Type
        {
            get
            {
                return EventType.SingleActionInteraction;
            }
        }

        /// <summary>
        /// Gets or sets the action to take for this interaction.
        /// </summary>
        [JsonProperty(PropertyName = "a")]
        public uint Action { get; set; }
    }

    /// <summary>
    /// Represents an interaction event with multiple actions.
    /// </summary>
    public class MultiActionInteraction : Interaction
    {
        /// <summary>
        /// Gets the type of event.
        /// </summary>
        public override EventType Type
        {
            get
            {
                return EventType.MultiActionInteraction;
            }
        }

        /// <summary>
        /// Gets or sets the action to take for this interaction.
        /// </summary>
        [JsonProperty(PropertyName = "a")]
        public uint[] Actions { get; set; }
    }

    /// <summary>
    /// Represents an observed outcome that is associated with some interaction.
    /// </summary>
    public class Observation : IEvent
    {
        /// <summary>
        /// Gets the type of event.
        /// </summary>
        public EventType Type
        {
            get
            {
                return EventType.Observation;
            }
        }

        /// <summary>
        /// Gets or sets the unique experimental unit key that this event belongs to.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the time stamp of the event.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the value of the observation.
        /// </summary>
        /// <remarks>
        /// Observation values can be as simple as a single number indicating whether the outcome was positive or negative,
        /// or more generic structure which can be later used for reward metric analysis.
        /// </remarks>
        [JsonProperty(PropertyName = "v", NullValueHandling = NullValueHandling.Ignore)]
        public object Value { get; set; }
    }

    /// <summary>
    /// Represents the fragment to be uploaded.
    /// </summary>
    internal class ExperimentalUnitFragment
    {
        /// <summary>
        /// The unique experimental unit key.
        /// </summary>
        [JsonProperty(PropertyName = "i")]
        public string Key { get; set; }

        /// <summary>
        /// The event value.
        /// </summary>
        [JsonProperty(PropertyName = "v")]
        public object Value { get; set; }
    }

    /// <summary>
    /// Represents a batch of events to be uploaded.
    /// </summary>
    internal class EventBatch
    {
        /// <summary>
        /// The auto-generated Id of the batch to be uploaded.
        /// </summary>
        [JsonProperty(PropertyName = "i")]
        public System.Guid Id { get; set; }

        /// <summary>
        /// The list of json-serialized events to be uploaded.
        /// </summary>
        [JsonProperty(PropertyName = "j")]
        public IList<string> JsonEvents { get; set; }
    }
}
