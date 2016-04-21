using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class MwtExplorer
    {
        public static MwtExplorer<TContext, TAction, TPolicyValue> Create<TContext, TAction, TPolicyValue>(
            string appId, 
            INumberOfActionsProvider<TContext> numActionsProvider,
            IRecorder<TContext, TAction> recorder, 
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> policy = null,
            IFullExplorer<TAction> initialExplorer = null
            )
        {
            var mwt = new MwtExplorer<TContext, TAction, TPolicyValue>(appId, numActionsProvider, recorder, explorer, initialExplorer);
            mwt.Policy = policy;
            return mwt;
        }

        public static MwtExplorer<TContext, TAction, TPolicyValue> Create<TContext, TAction, TPolicyValue>(
            string appId,
            int numActions,
            IRecorder<TContext, TAction> recorder,
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> policy = null,
            IFullExplorer<TAction> initialExplorer = null)
        {
            var mwt = new MwtExplorer<TContext, TAction, TPolicyValue>(appId, new ConstantActionsProvider<TContext>(numActions), recorder, explorer, initialExplorer);
            mwt.Policy = policy;
            return mwt;
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
        private IExplorer<TAction, TPolicyValue> explorer;
        private INumberOfActionsProvider<TContext> numActionsProvider;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, 
            INumberOfActionsProvider<TContext> numActionsProvider,
            IRecorder<TContext, TAction> recorder, 
            IExplorer<TAction, TPolicyValue> explorer,
            IFullExplorer<TAction> initialExplorer = null
            )
		{
            this.appId = MurMurHash3.ComputeIdHash(appId);

            if (recorder == null)
                throw new ArgumentNullException("recorder");
            this.Recorder = recorder;

            if (explorer == null)
                throw new ArgumentNullException("explorer");
            this.Explorer = explorer;

            this.InitialExplorer = initialExplorer;
            this.numActionsProvider = numActionsProvider;

            if (this.InitialExplorer != null && this.numActionsProvider == null)
            {
                throw new ArgumentNullException("numActionsProvider");
            }
        }

        public IExplorer<TAction, TPolicyValue> Explorer
        {
            get
            {
                return this.explorer;
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException("Explorer");

                this.explorer = value;
            }
        }

        public IFullExplorer<TAction> InitialExplorer { get; set; }

        public IContextMapper<TContext, TPolicyValue> Policy { get; set; }

        public IRecorder<TContext, TAction> Recorder
        {
            get
            {
                return this.recorder;
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException("Recorder");

                this.recorder = value;
            }
        }

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
            PRG random = new PRG(saltedSeed);

            var policyDecision = policy.MapContext(context);
            var explorerDecision = this.Explorer.MapContext(random, policyDecision.Value, this.numActionsProvider.GetNumberOfActions(context));
            
            this.Log(uniqueKey, context, explorerDecision, policyDecision);

            return explorerDecision.Value;
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            ulong saltedSeed = MurMurHash3.ComputeIdHash(uniqueKey.Key) + this.appId;
            PRG random = new PRG(saltedSeed);

            var policy = this.Policy;
            ExplorerDecision<TAction> explorerDecision;
            PolicyDecision<TPolicyValue> policyDecision = null;

            if (policy == null)
                explorerDecision = this.InitialExplorer.Explore(random, this.numActionsProvider.GetNumberOfActions(context));
            else
            {
                policyDecision = policy.MapContext(context);
                explorerDecision = this.Explorer.MapContext(random, policyDecision.Value, this.numActionsProvider.GetNumberOfActions(context));
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