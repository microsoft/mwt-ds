using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
	/// The top level MwtExplorer class.  Using this makes sure that the
	/// right bits are recorded and good random actions are chosen.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public class MwtExplorer<TContext, TValue, TExplorerState, TMapperValue, TMapperState>
	{
        private ulong appId;
        private IRecorder<TContext, TValue, TExplorerState, TMapperValue, TMapperState> recorder;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, IRecorder<TContext, TValue, TExplorerState, TMapperValue, TMapperState> recorder)
		{
            this.appId = MurMurHash3.ComputeIdHash(appId);
            this.recorder = recorder;
        }

		/// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
		/// </summary>
		/// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
		/// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
		/// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TValue ChooseAction(IExplorer<TContext, TValue, TExplorerState, TMapperValue, TMapperState> explorer, UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            ulong seed = MurMurHash3.ComputeIdHash(uniqueKey.Key);

            var decisionTuple = explorer.MapContext(seed + this.appId, context, numActionsVariable);

            if (decisionTuple.ShouldRecord)
            {
                this.recorder.Record(context, decisionTuple, uniqueKey);
            }

            return decisionTuple.Value;
        }

        /// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
        /// </summary>
        /// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
        /// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
        /// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public TValue ChooseAction(IExplorer<TContext, TValue, TExplorerState, TMapperValue, TMapperState> explorer, string uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return this.ChooseAction(explorer, new UniqueEventID { Key = uniqueKey }, context, numActionsVariable);
        }
	}
}