using Microsoft.Research.MultiWorldTesting.ExploreLibrary;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class MultiPolicy<TContext, TValue> : IContextMapper<TContext, TValue>
    {
        private IContextMapper<TContext, TValue> vwPolicy;
        private IContextMapper<TContext, TValue> initialPolicy;
        private IContextMapper<TContext, TValue> activePolicy;

        public MultiPolicy(IContextMapper<TContext, TValue> vwPolicy, IContextMapper<TContext, TValue> initialPolicy)
        {
            this.vwPolicy = vwPolicy;
            this.initialPolicy = initialPolicy;
            this.activePolicy = this.initialPolicy;
        }

        public void ModelUpdated()
        {
            this.initialPolicy = this.vwPolicy;
        }

        public Decision<TValue> MapContext(TContext context)
        {
            return this.initialPolicy.MapContext(context);
        }
    }

    public static class MultiPolicy
    {
        public static MultiPolicy<TContext, TValue> Create<TContext, TValue>(
            IContextMapper<TContext, TValue> vwPolicy,
            IContextMapper<TContext, TValue> initialPolicy)
        {
            return new MultiPolicy<TContext, TValue>(vwPolicy, initialPolicy);
        }
    }
}
