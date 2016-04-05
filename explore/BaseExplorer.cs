using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TContext, TValue, TExplorerState, TMapperValue>
        : IExplorer<TContext, TValue, TExplorerState, TMapperValue>, IDisposable
    {
        protected IContextMapper<TContext, TMapperValue> contextMapper;
        protected bool explore;
        protected readonly uint numActionsFixed;

        protected BaseExplorer(IContextMapper<TContext, TMapperValue> defaultPolicy, uint numActions = uint.MaxValue)
        {
            if (defaultPolicy == null)
                throw new ArgumentNullException("defaultPolicy");

            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.contextMapper = defaultPolicy;
            this.explore = true;
            this.numActionsFixed = numActions;
        }

        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public Decision<TValue, TExplorerState, TMapperValue> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = (numActionsFixed == uint.MaxValue) ? numActionsVariable : numActionsFixed;

            // Actual number of actions at decision time must be a valid positive finite number.
            // TODO: doesn't work with ADF...
            //if (numActions == uint.MaxValue || numActions < 1)
            //{
            //    throw new ArgumentException("Number of actions must be at least 1.");
            //}

            return this.MapContextInternal(saltedSeed, context, numActions);
        }

        protected abstract Decision<TValue, TExplorerState, TMapperValue> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                var disposable = this.contextMapper as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                    this.contextMapper = null;
                }
            }
        }
    }
}
