using MultiWorldTesting;
using System;

namespace DecisionSample
{
    internal class DecisionServicePolicy<TContext> : IPolicy<TContext>, IDisposable
    {
        // Recorder should talk to the Policy to pass over latest model version
        public uint ChooseAction(TContext context)
        {
            return 0;
        }

        public void Dispose() { }
    }

}
