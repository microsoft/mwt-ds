using System;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
    /// Represents a recorder that exposes a method to record exploration data based on generic contexts. 
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    /// <remarks>
    /// Exploration data is specified as a set of tuples (context, action, probability, key) as described below. An 
    /// application passes an IRecorder object to the @MwtExplorer constructor. See 
    /// @StringRecorder for a sample IRecorder object.
    /// </remarks>
    public interface IRecorder<TContext, TAction, TExplorerState, TPolicyAction, TPolicyState>
    {
        /// <summary>
        /// Records the exploration data associated with a given decision.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="action">Chosen by an exploration algorithm given context.</param>
        /// <param name="probability">The probability of the chosen action given context.</param>
        /// <param name="uniqueKey">A user-defined identifer for the decision.</param>
        /// <param name="modelId">Optional; The Id of the model used to make predictions/decisions, if any exists at decision time.</param>
        /// <param name="isExplore">Optional; Indicates whether the decision was generated purely from exploration (vs. exploitation).</param>
        void Record(TContext context, Decision<TAction, TExplorerState, TPolicyAction, TPolicyState> decision, UniqueEventID uniqueKey); // string modelId = null, bool? isExplore = null
    }

    public interface IExplorer<TContext, TAction, TExplorerState, TPolicyAction, TPolicyState>
    {
        /// <summary>
        /// Determines the action to take and the probability with which it was chosen, for a
        /// given context. 
        /// </summary>
        /// <param name="saltedSeed">A PRG seed based on a unique id information provided by the user.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>
        /// A <see cref="DecisionTuple"/> object including the action to take, the probability it was chosen, 
        /// and a flag indicating whether to record this decision.
        /// </returns>
        Decision<TAction, TExplorerState, TPolicyAction, TPolicyState> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue);

        void EnableExplore(bool explore);
    }

    public interface IContextMapper<TContext, TAction, TPolicyState>
    {
        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.</returns>
        PolicyDecision<TAction, TPolicyState> MapContext(TContext context, uint numActionsVariable = uint.MaxValue);
    }

    public interface IPolicy<TContext, TPolicyState> : IContextMapper<TContext, uint, TPolicyState>
    {
    }

    public interface IRanker<TContext, TPolicyState> : IContextMapper<TContext, uint[], TPolicyState>
    {
    }

    public interface IScorer<TContext, TPolicyState> : IContextMapper<TContext, float[], TPolicyState>
    {
    }

    public interface IConsumePolicy<TContext, TAction, TPolicyState>
    {
        void UpdatePolicy(IContextMapper<TContext, TAction, TPolicyState> newPolicy);
    }

    public interface IConsumePolicies<TContext, TAction, TPolicyState>
    {
        void UpdatePolicy(IContextMapper<TContext, TAction, TPolicyState>[] newPolicies);
    }

    public interface IStringContext
    {
        string ToString();
    }
}