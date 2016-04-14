
namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Incomplete decision service client, just a vehicle to transport TAction
    /// </summary>
    /// <typeparam name="TAction"></typeparam>
    public class DecisionServiceClientSpecification<TAction>
    {
        internal int? NumberOfActions;

        internal DecisionServiceConfiguration Config;
    }

    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionService
    {
        public static DecisionServiceClientSpecification<int> WithPolicy(DecisionServiceConfiguration config, int numberOfActions)
        { 
            return new DecisionServiceClientSpecification<int>()
            {
                Config = config,
                NumberOfActions = numberOfActions
            };
        }

        public static DecisionServiceClientSpecification<int[]> WithRanker(DecisionServiceConfiguration config)
        { 
            return new DecisionServiceClientSpecification<int[]>()
            {
                Config = config
            };
        }
    }
}
