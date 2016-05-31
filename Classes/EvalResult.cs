using Newtonsoft.Json;
using System;

namespace DecisionServicePrivateWeb.Classes
{
    /// <summary>
    /// Represents a single policy evaluation result.
    /// </summary>
    public class EvalResult
    {
        [JsonProperty(PropertyName = "name")]
        public string PolicyName { get; set; }

        [JsonProperty(PropertyName = "lastenqueuedtime")]
        public DateTime LastWindowTime { get; set; }

        [JsonProperty(PropertyName = "averagecost")]
        public float AverageCost { get; set; }

        [JsonProperty(PropertyName = "window")]
        public string WindowType { get; set; }
    }
}