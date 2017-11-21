namespace Microsoft.CustomDecisionService.RESTExample
{
    public class SharedContext
    {
        public DemographicNamespace Demographics { get; set; }

        public LocationNamespace Location { get; set; }
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

    public class CustomReward
    {
        public int A { get; set; }
    }
}
