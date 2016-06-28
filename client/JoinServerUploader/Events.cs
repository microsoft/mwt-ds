using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    /// <summary>
    /// Base interface for an application event.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// The unique experimental unit key that this event belongs to.
        /// </summary>
        [JsonProperty(PropertyName = "EventId")]
        string Key { get; set; }
    }

    /// <summary>
    /// Represents an interaction event that are usually represented as a 4-tuple of
    /// (chosen action(s), probability, context, key).
    /// </summary>
    [JsonConverter(typeof(InteractionJsonConverter))]
    public class Interaction: IEvent
    {
        /// <summary>
        /// Gets or sets the unique experimental unit key that this event belongs to.
        /// </summary>
        public string Key { get; set; }

        // int, int[]
        public object Value { get; set; }

        public object Context { get; set; }

        public object ExplorerState { get; set; }

        public object MapperState { get; set; }

        public float? ProbabilityOfDrop { get; set; }

        public static Interaction CreateEpsilonGreedy<TContext>(string key, TContext context, int action, float probability)
        {
            return Interaction.Create(key, context, action, new GenericExplorerState { Probability = probability });
        }
        
        // TODO: add other exploration types

        internal static Interaction Create<TAction, TContext>(
            string key, TContext context, TAction value, object exploreState)
        {
            return new Interaction 
            { 
                Key = key,
                Context = context,
                ExplorerState = exploreState,
                Value = value
            };
        }

    }

    /// <summary>
    /// Represents an observed outcome that is associated with some interaction.
    /// </summary>
    public class Observation : IEvent
    {
        /// <summary>
        /// Gets or sets the unique experimental unit key that this event belongs to.
        /// </summary>
        public string Key { get; set; }

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
