using Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core;
using System;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
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
    public interface IRecorder<TContext>
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
        void Record(TContext context, uint action, float probability, UniqueEventID uniqueKey, string modelId = null, bool? isExplore = null);
    };

    /// <summary>
    /// Exposes a method for choosing an action given a generic context. IPolicy objects are 
    /// passed to (and invoked by) exploration algorithms to specify the default policy behavior.
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public interface IPolicy<TContext>
    {
	    /// <summary>
	    /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
	    /// </summary>
	    /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.</returns>
        PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue);
    };

    /// <summary>
    /// Exposes a method for specifying a score (weight) for each action given a generic context. 
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public interface IScorer<TContext>
    {
	    /// <summary>
	    /// Determines the score of each action for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
	    /// </summary>
	    /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>Vector of scores indexed by action (1-based).</returns>
        List<float> ScoreActions(TContext context, uint numActionsVariable = uint.MaxValue);
    };

    public interface IExplorer<TContext>
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
        DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue);

        void EnableExplore(bool explore);
    };

    public interface IConsumePolicy<TContext>
    {
        void UpdatePolicy(IPolicy<TContext> newPolicy);
    };

    public interface IConsumePolicies<TContext>
    {
        void UpdatePolicy(IPolicy<TContext>[] newPolicies);
    };

    public interface IConsumeScorer<TContext>
    {
        void UpdateScorer(IScorer<TContext> newScorer);
    };

    public interface IStringContext
    {
	    string ToString();
    };
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction
{
    /// <summary>
    /// Represents a recorder that exposes a method to record exploration data based on generic contexts. 
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    /// <remarks>
    /// Exploration data is specified as a set of tuples (context, actions, probability, key) as described below. An 
    /// application passes an IRecorder object to the @MwtExplorer constructor. See 
    /// @StringRecorder for a sample IRecorder object.
    /// </remarks>
    public interface IRecorder<TContext>
    {
        /// <summary>
        /// Records the exploration data associated with a given decision.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="actions">Chosen by an exploration algorithm given context.</param>
        /// <param name="probability">The probability of the chosen action given context.</param>
        /// <param name="uniqueKey">A user-defined identifer for the decision.</param>
        /// <param name="modelId">Optional; The Id of the model used to make predictions/decisions, if any exists at decision time.</param>
        /// <param name="isExplore">Optional; Indicates whether the decision was generated purely from exploration (vs. exploitation).</param>
        void Record(TContext context, uint[] actions, float probability, UniqueEventID uniqueKey, string modelId = null, bool? isExplore = null);
    };

    /// <summary>
    /// Exposes a method for choosing an action given a generic context. IPolicy objects are 
    /// passed to (and invoked by) exploration algorithms to specify the default policy behavior.
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public interface IPolicy<TContext>
    {
        /// <summary>
        /// Determines the actions to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>A decision tuple containing the index array of the actions to take (1-based), and the Id of the model or policy used to make the decision.</returns>
        PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue);
    };

    /// <summary>
    /// Exposes a method for specifying a score (weight) for each action given a generic context. 
    /// </summary>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public interface IScorer<TContext>
    {
        /// <summary>
        /// Determines the score of each action for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>Vector of scores indexed by action (1-based).</returns>
        List<float> ScoreActions(TContext context, uint numActionsVariable = uint.MaxValue);
    };

    public interface IExplorer<TContext>
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
        DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue);

        void EnableExplore(bool explore);
    };

    public interface IConsumePolicy<TContext>
    {
        void UpdatePolicy(IPolicy<TContext> newPolicy);
    };

    public interface IConsumePolicies<TContext>
    {
        void UpdatePolicy(IPolicy<TContext>[] newPolicies);
    };

    public interface IConsumeScorer<TContext>
    {
        void UpdateScorer(IScorer<TContext> newScorer);
    };

    public interface IStringContext
    {
        string ToString();
    };
}