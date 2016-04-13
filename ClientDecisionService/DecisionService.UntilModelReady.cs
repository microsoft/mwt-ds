using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DecisionService_UntilModelReady
    {
        private static DecisionServiceClient<TAction, TContext, TPolicyValue>
            CreateClient<TAction, TContext, TPolicyValue, TClientType>(
            DecisionServiceUntilModelReadySpecification<TAction, TContext, TPolicyValue, TClientType> spec,
            IFullExplorer<TAction> fullExplorer)
        {
            var client = spec.Parent;
            var newClient = new DecisionServiceClient<TAction, TContext, TPolicyValue>(client);
            newClient.InitialExplorer = fullExplorer;

            // remove ownership
            client.client = null;

            return client;
        }

        // Move into specification stage
        public static DecisionServiceUntilModelReadySpecification<TAction, TContext, TPolicyValue, TClientType>
            WithExploitUntilModelReady<TAction, TContext, TPolicyValue, TClientType>
            (this DecisionServiceClientBase<TAction, TContext, TPolicyValue, TClientType> that) 
        {
            return new DecisionServiceUntilModelReadySpecification<TAction, TContext, TPolicyValue, TClientType> 
            {
                Parent = that
            };
        }

        public static DecisionServiceClientWithDefaultAction<TContext, TAction, TPolicyValue>
            WithDefaultAction<TContext, TAction, TPolicyValue, TClientType>
            (this DecisionServiceUntilModelReadySpecification<TAction, TContext, TPolicyValue, TClientType> that)
        {
            var newClient = new DecisionServiceClientWithDefaultAction<TContext, TAction, TPolicyValue>(that.Parent);
            
            // invalidate the original object
            that.Parent.client = null;

            return newClient;
        }

        // Policy exploration strategies

        public static DecisionServiceClient<int, TContext, TPolicyValue>
            WithUniformRandomExploration<TContext, TPolicyValue, TClientType>
            (this DecisionServiceUntilModelReadySpecification<int, TContext, TPolicyValue, TClientType> that)
        {
            return CreateClient(that, new UniformRandomExploration());
        }

        // Ranking exploration strategies

        public static DecisionServiceClient<int[], TContext, TPolicyValue>
            WithPermutationExploration<TContext, TPolicyValue, TClientType>
            (this DecisionServiceUntilModelReadySpecification<int[], TContext, TPolicyValue, TClientType> that)
        {
            return CreateClient(that, new PermutationExplorer());
        }

        public static DecisionServiceClient<int[], TContext, TPolicyValue>
            WithTopSlotExploration<TContext, TPolicyValue, TClientType>
            (this DecisionServiceUntilModelReadySpecification<int[], TContext, TPolicyValue, TClientType> that)
        {
            return CreateClient(that, new PermutationExplorer(1));
        }
    }

    /// <summary>
    /// Type to transport generic parameters and enable type specialization through extension methods.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DecisionServiceUntilModelReadySpecification<TAction, TContext, TPolicyValue, TClientType>
    {
        internal DecisionServiceClientBase<TAction, TContext, TPolicyValue, TClientType> Parent;
    }
}
