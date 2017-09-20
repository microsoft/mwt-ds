namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
    /// Base for explorer implementations.
    /// </summary>
    /// <typeparam name="TAction"></typeparam>
    /// <typeparam name="TPolicyValue"></typeparam>
    public abstract class BaseExplorer<TAction, TPolicyValue>
        : IExplorer<TAction, TPolicyValue>
    {
        protected bool explore;

        protected BaseExplorer()
        {
            this.explore = true;
        }

        /// <summary>
        /// Enable or disables exploration.
        /// </summary>
        /// <param name="explore">True to enable exploration.</param>
        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        /// <summary>
        /// Maps a context to a decision.
        /// </summary>
        public abstract ExplorerDecision<TAction> MapContext(PRG prg, TPolicyValue policyAction, int numActions);
    }
}
