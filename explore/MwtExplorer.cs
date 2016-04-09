using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class MwtExplorer
    {
        public static MwtExplorer<TContext, TValue, TMapperValue> Create<TContext, TValue, TMapperValue>(
            string appId, 
            IRecorder<TContext, TValue> recorder, 
            IExplorer<TValue, TMapperValue> explorer,
            IFullExplorer<TContext, TValue> initialExplorer = null)
        {
            return new MwtExplorer<TContext, TValue, TMapperValue>(appId, recorder, explorer, initialExplorer);
        }
    }

    /// <summary>
	/// The top level MwtExplorer class.  Using this makes sure that the
	/// right bits are recorded and good random actions are chosen.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public sealed class MwtExplorer<TContext, TValue, TMapperValue> : IDisposable
	{
        private ulong appId;
        private IRecorder<TContext, TValue> recorder;
        private IFullExplorer<TContext, TValue> initialExplorer;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, 
            IRecorder<TContext, TValue> recorder, 
            IExplorer<TValue, TMapperValue> explorer,
            IFullExplorer<TContext, TValue> initialExplorer = null)
		{
            this.appId = MurMurHash3.ComputeIdHash(appId);
            // TODO: check for null
            this.recorder = recorder;
            this.Explorer = explorer;
            // TODO: check for null
            this.initialExplorer = initialExplorer;
        }

        public IExplorer<TValue, TMapperValue> Explorer { get; set; }

        public IContextMapper<TContext, TMapperValue> Policy { get; set; }

		/// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
		/// </summary>
		/// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
		/// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
		/// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TValue ChooseAction(UniqueEventID uniqueKey, TContext context, TValue initialAction)
        {
            // Note: thread-safe atomic reference access
            var policy = this.Policy;
            if (policy == null)
            {
                // policy not ready & initial action provided
                this.Log(uniqueKey, 
                    context, 
                    ExplorerDecision.Create(initialAction, new GenericExplorerState { Probability = 1 }, true));

                return initialAction;
            }

            ulong saltedSeed = MurMurHash3.ComputeIdHash(uniqueKey.Key) + this.appId;

            var policyDecision = policy.MapContext(context);
            var explorerDecision = this.Explorer.MapContext(saltedSeed, policyDecision.Value);
            
            this.Log(uniqueKey, context, explorerDecision, policyDecision);

            return explorerDecision.Value;
        }

        public TValue ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            ulong saltedSeed = MurMurHash3.ComputeIdHash(uniqueKey.Key) + this.appId;

            var policy = this.Policy;
            ExplorerDecision<TValue> explorerDecision;
            PolicyDecision<TMapperValue> policyDecision = null;

            if (policy == null)
                explorerDecision = this.initialExplorer.Explore(saltedSeed, context);
            else
            {
                policyDecision = policy.MapContext(context);
                explorerDecision = this.Explorer.MapContext(saltedSeed, policyDecision.Value);
            }

            this.Log(uniqueKey, context, explorerDecision, policyDecision != null ? policyDecision.MapperState : null);

            return explorerDecision.Value;
        }

        private void Log(UniqueEventID uniqueKey, TContext context, ExplorerDecision<TValue> explorerDecision, object policyState = null)
        {
            if (explorerDecision.ShouldRecord)
            {
                this.recorder.Record(context, explorerDecision.Value, explorerDecision.ExplorerState, policyState, uniqueKey);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                var disposable = this.Explorer as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                    this.Explorer = null;
                }

                disposable = this.Policy as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                    this.Policy = null;
                }

                disposable = this.recorder as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                    this.recorder = null;
                }
            }
        }
    }
}