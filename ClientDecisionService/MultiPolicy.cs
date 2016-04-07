using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class MultiPolicy<TContext, TValue> : IContextMapper<TContext, TValue>, INumberOfActionsProvider<TContext>, IDisposable
    {
        private IContextMapper<TContext, TValue> vwPolicy;
        private IContextMapper<TContext, TValue> initialPolicy;
        private IContextMapper<TContext, TValue> activePolicy;
        private INumberOfActionsProvider<TContext> numberOfActionsProvider;

        public MultiPolicy(IContextMapper<TContext, TValue> vwPolicy, IContextMapper<TContext, TValue> initialPolicy = null)
        {
            this.vwPolicy = vwPolicy;
            this.numberOfActionsProvider = this.vwPolicy as INumberOfActionsProvider<TContext>;

            this.initialPolicy = initialPolicy == null ? new NullPolicy() : initialPolicy;
            this.activePolicy = this.initialPolicy;
        }

        private sealed class NullPolicy : IContextMapper<TContext, TValue>
        {
            public Decision<TValue> MapContext(TContext context)
            {
                return null;
            }
        }
        
        public void ModelUpdated()
        {
            var disposable = this.initialPolicy as IDisposable;
            if (disposable != null)
                disposable.Dispose();

            this.activePolicy = this.vwPolicy;
        }

        public Decision<TValue> MapContext(TContext context)
        {
            return this.activePolicy.MapContext(context);
        }

        public int GetNumberOfActions(TContext context)
        {
            if (this.numberOfActionsProvider == null)
                throw new InvalidOperationException(string.Format("Underlying policy '{0}' does not implement INumberOfActionsProvider", this.vwPolicy.GetType()));

            return this.numberOfActionsProvider.GetNumberOfActions(context);
        }

        /// <summary>
        /// Dispose the object and clean up any resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                var disposable = this.initialPolicy as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                this.initialPolicy = null;

                disposable = this.vwPolicy as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
                this.vwPolicy = null;
            }
        }
    }

    public static class MultiPolicy
    {
        public static MultiPolicy<TContext, TValue> Create<TContext, TValue>(
            IContextMapper<TContext, TValue> vwPolicy,
            IContextMapper<TContext, TValue> initialPolicy = null)
        {
            return new MultiPolicy<TContext, TValue>(vwPolicy, initialPolicy);
        }
    }
}
