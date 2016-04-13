using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class MwtExplorer
    {
        public static MwtExplorer<TContext, TAction, TPolicyValue> Create<TContext, TAction, TPolicyValue>(
            string appId, 
            IRecorder<TContext, TAction> recorder, 
            IExplorer<TAction, TPolicyValue> explorer,
            IFullExplorer<TAction> initialExplorer = null,
            INumberOfActionsProvider<TContext> numActionsProvider = null)
        {
            return new MwtExplorer<TContext, TAction, TPolicyValue>(appId, recorder, explorer, initialExplorer, numActionsProvider);
        }
    }

    /// <summary>
	/// The top level MwtExplorer class.  Using this makes sure that the
	/// right bits are recorded and good random actions are chosen.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public sealed class MwtExplorer<TContext, TAction, TPolicyValue> : IDisposable
	{
        private ulong appId;
        private IRecorder<TContext, TAction> recorder;
        private INumberOfActionsProvider<TContext> numActionsProvider;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, 
            IRecorder<TContext, TAction> recorder, 
            IExplorer<TAction, TPolicyValue> explorer,
            IFullExplorer<TAction> initialExplorer = null,
            INumberOfActionsProvider<TContext> numActionsProvider = null)
		{
            this.appId = MurMurHash3.ComputeIdHash(appId);
            // TODO: check for null
            this.recorder = recorder;
            this.Explorer = explorer;
            // TODO: check for null
            this.InitialExplorer = initialExplorer;
            this.numActionsProvider = numActionsProvider;

            if (this.InitialExplorer != null && this.numActionsProvider == null)
            {
                throw new ArgumentNullException("numActionsProvider");
            }
        }

        public IExplorer<TAction, TPolicyValue> Explorer { get; set; }

        public IFullExplorer<TAction> InitialExplorer { get; set; }

        public IContextMapper<TContext, TPolicyValue> Policy { get; set; }

		/// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
		/// </summary>
		/// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
		/// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
		/// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context, TAction initialAction)
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

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            ulong saltedSeed = MurMurHash3.ComputeIdHash(uniqueKey.Key) + this.appId;

            var policy = this.Policy;
            ExplorerDecision<TAction> explorerDecision;
            PolicyDecision<TPolicyValue> policyDecision = null;

            if (policy == null)
                explorerDecision = this.InitialExplorer.Explore(saltedSeed, this.numActionsProvider.GetNumberOfActions(context));
            else
            {
                policyDecision = policy.MapContext(context);
                explorerDecision = this.Explorer.MapContext(saltedSeed, policyDecision.Value);
            }

            this.Log(uniqueKey, context, explorerDecision, policyDecision != null ? policyDecision.MapperState : null);

            return explorerDecision.Value;
        }

        private void Log(UniqueEventID uniqueKey, TContext context, ExplorerDecision<TAction> explorerDecision, object policyState = null)
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