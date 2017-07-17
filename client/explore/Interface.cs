using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
    public interface IRecorder<in TContext, in TAction>
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
        void Record(TContext context, TAction value, object explorerState, object mapperState, string uniqueKey); 
    }

    public interface IExplorer<TAction, TPolicyValue>
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
        ExplorerDecision<TAction> MapContext(PRG prg, TPolicyValue policyAction, int numActions); 

        void EnableExplore(bool explore);
    }

    public interface IInitialExplorer<TPolicyValue, TActionValue>
    {
        TPolicyValue Explore(TActionValue defaultValues);
    }

    public interface IFullExplorer<TAction>
    {
        ExplorerDecision<TAction> Explore(PRG random, int numActions); 
    }

    public interface INumberOfActionsProvider<in TContext>
    {
        int GetNumberOfActions(TContext context);
    }

    public class ConstantActionsProvider<TContext> : INumberOfActionsProvider<TContext>
    {
        private int numActions;

        public ConstantActionsProvider(int numActions)
        {
            this.numActions = numActions;
        }

        public int GetNumberOfActions(TContext context)
        {
            return this.numActions;
        }
    }

    public interface IContextMapper<in TContext, TPolicyValue>
    {
        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        Task<PolicyDecision<TPolicyValue>> MapContextAsync(TContext context);
    }

    public interface IUpdatable<TModel>
    {
        void Update(TModel model);
    }

    public interface IPolicy<in TContext> : IContextMapper<TContext, int>
    {
    }

    public interface IRanker<in TContext> : IContextMapper<TContext, int[]>
    {
    }

    public interface IScorer<in TContext> : IContextMapper<TContext, float[]>
    {
    }

    public class ActionProbability
    {
        public int Action { get; set; }

        public float Probability { get; set; }
    }
}