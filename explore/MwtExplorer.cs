using System;

namespace MultiWorldTesting.SingleAction
{
    /// <summary>
	/// The top level MwtExplorer class.  Using this makes sure that the
	/// right bits are recorded and good random actions are chosen.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
	public class MwtExplorer<TContext>
	{
        private ulong appId;
	    private IRecorder<TContext> recorder;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
		/// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, IRecorder<TContext> recorder)
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
        public uint ChooseAction(IExplorer<TContext> explorer, UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            ulong seed = MurMurHash3.ComputeIdHash(uniqueKey.Key);

            DecisionTuple decisionTuple = explorer.ChooseAction(seed + this.appId, context, numActionsVariable);

            if (decisionTuple.ShouldRecord)
            {
                this.recorder.Record(context, decisionTuple.Action, decisionTuple.Probability, uniqueKey);
            }

            return decisionTuple.Action;
        }

        /// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
        /// </summary>
        /// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
        /// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
        /// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public uint ChooseAction(IExplorer<TContext> explorer, string uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return this.ChooseAction(explorer, new UniqueEventID { Key = uniqueKey }, context, numActionsVariable);
        }
	};
}

namespace MultiWorldTesting.MultiAction
{
    /// <summary>
    /// The top level MwtExplorer class.  Using this makes sure that the
    /// right bits are recorded and good random actions are chosen.
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public class MwtExplorer<TContext>
    {
        private ulong appId;
        private IRecorder<TContext> recorder;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="appId">This should be unique to each experiment to avoid correlation bugs.</param>
        /// <param name="recorder">A user-specified class for recording the appropriate bits for use in evaluation and learning.</param>
        public MwtExplorer(string appId, IRecorder<TContext> recorder)
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
        /// <returns>A list of unsigned 32-bit integers representing the 1-based chosen actions.</returns>
        public uint[] ChooseAction(IExplorer<TContext> explorer, UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            ulong seed = MurMurHash3.ComputeIdHash(uniqueKey.Key);

            DecisionTuple decisionTuple = explorer.ChooseAction(seed + this.appId, context, numActionsVariable);

            if (decisionTuple.ShouldRecord)
            {
                this.recorder.Record(context, decisionTuple.Actions, decisionTuple.Probability, uniqueKey);
            }

            return decisionTuple.Actions;
        }

        /// <summary>
        /// Choose an action (or decision to take) given the exploration algorithm and context.
        /// </summary>
        /// <param name="explorer">An existing exploration algorithm (one of the above) which uses the default policy as a callback.</param>
        /// <param name="uniqueKey">A unique identifier for the experimental unit. This could be a user id, a session id, etc...</param>
        /// <param name="context">The context upon which a decision is made. See SimpleContext above for an example.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned 32-bit integer representing the 1-based chosen action.</returns>
        public uint[] ChooseAction(IExplorer<TContext> explorer, string uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return this.ChooseAction(explorer, new UniqueEventID { Key = uniqueKey }, context, numActionsVariable);
        }
    };
}