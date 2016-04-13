using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public static class DecisionService_Explorer
    {
        public static DecisionServiceClientBase<int, TContext, TPolicyAction, TClientType> 
            WithEpsilonGreedy<TContext, TPolicyAction, TClientType>
            (this DecisionServiceClientBase<int, TContext, TPolicyAction, TClientType> that, float epsilon)
        {
            that.Explorer = new EpsilonGreedyExplorer(epsilon);
            return that;
        }

        // TODO: add exploration strategies

        public static DecisionServiceClientBase<int[], TContext, TPolicyAction, TClientType> 
            WithTopSlotEpsilonGreedy<TContext, TPolicyAction, TClientType>
            (this DecisionServiceClientBase<int[], TContext, TPolicyAction, TClientType> that, float epsilon)
        {
            that.Explorer = new TopSlotExplorer(new EpsilonGreedyExplorer(epsilon));
            return that;
        }

        static void Demo()
        {
            // var ds2 = DecisionService.WithPolicyDefaults(null, 5).WithJson();

            var ds = DecisionService.WithPolicy(null, 5)
                .WithJson();
                // 
                .WithExploitUntilModelReady(new MyPolicy()) // UseThis And Discard previous same setting slot
                //.WithUniformRandomUntilModelReady();
                //.WithPermutationUntilModelReady();
                .WithRecorder(my IRecorder());
                

            var ss = DecisionService.WithPolicy(null, 1).With<string>().ChooseAction()
                .WithExploitUntilModelReady() // << this doesn't do anything except filtering the n
                .
                // next method list
                .
        
        }
    }
}
