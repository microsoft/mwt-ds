using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class MwtExplorer
    {
        public static MwtExplorer<TContext, TValue, TExplorerState, TMapperValue> Create<TContext, TValue, TExplorerState, TMapperValue>(
            string appId, 
            IRecorder<TContext, TValue, TExplorerState> recorder, 
            IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
        {
            return new MwtExplorer<TContext, TValue, TExplorerState, TMapperValue>(appId, recorder, explorer);
        }
    }

    /// <summary>
	/// The top level MwtExplorer class.  Using this makes sure that the
	/// right bits are recorded and good random actions are chosen.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public sealed class MwtExplorer<TContext, TValue, TExplorerState, TMapperValue> : IDisposable
	{
        private ulong appId;
        private IRecorder<TContext, TValue, TExplorerState> recorder;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, IRecorder<TContext, TValue, TExplorerState> recorder, IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
		{
            this.appId = MurMurHash3.ComputeIdHash(appId);
            this.recorder = recorder;
            this.Explorer = explorer;
        }

        public IExplorer<TContext, TValue, TExplorerState, TMapperValue> Explorer { get; set; }

		/// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
		/// </summary>
		/// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
		/// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
		/// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TValue MapContext(UniqueEventID uniqueKey, TContext context)
        {
            ulong seed = MurMurHash3.ComputeIdHash(uniqueKey.Key);

            var decision = this.Explorer.MapContext(seed + this.appId, context);

            if (decision.ShouldRecord)
                this.recorder.Record(context, decision.Value, decision.ExplorerState, decision.MapperDecision.MapperState, uniqueKey);

            return decision.Value;
        }

        /// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
        /// </summary>
        /// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
        /// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
        /// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TValue MapContext(string uniqueKey, TContext context)
        {
            return this.MapContext(new UniqueEventID { Key = uniqueKey }, context);
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