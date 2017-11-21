//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.CustomDecisionService.ClientLibraryExample
{
    public class DecisionContext
    {
        /// <summary>
        /// From SharedContext (REST)
        /// </summary>
        public DemographicNamespace Demographics { get; set; }

        /// <summary>
        /// From SharedContext (REST)
        /// </summary>
        public LocationNamespace Location { get; set; }

        /// <summary>
        /// The action array must be annotated as _multi property.
        /// </summary>
        [JsonProperty("_multi")]
        public ActionDependentFeatures[] Actions { get; set; }
    }

    public class DemographicNamespace
    {
        public string Gender { get; set; }
    }

    public class LocationNamespace
    {
        public string Country { get; set; }

        public string City { get; set; }
    }

    public class ActionDependentFeatures
    {
        public TopicNamespace Topic { get; set; }
    }

    public class TopicNamespace
    {
        public string Category { get; set; }
    }
}
