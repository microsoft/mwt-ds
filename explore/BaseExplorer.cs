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

        protected BaseExplorer(IContextMapper<TContext, TMapperValue> defaultPolicy, uint numActions)
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

        public abstract Decision<TValue, TExplorerState, TMapperValue> MapContext(ulong saltedSeed, TContext context);
    }


    public abstract class BaseVariableActionExplorer<TContext, TValue, TExplorerState, TMapperValue>
       : BaseExplorer<TContext, TValue, TExplorerState, TMapperValue>, IVariableActionExplorer<TContext, TValue, TExplorerState, TMapperValue>
    {
        // TODO: change uint.max to nullable
        protected BaseVariableActionExplorer(IContextMapper<TContext, TMapperValue> defaultPolicy, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions) { }

        public override Decision<TValue, TExplorerState, TMapperValue> MapContext(ulong saltedSeed, TContext context)
        {
            return this.MapContext(saltedSeed, context, this.numActionsFixed);
        }

        public abstract Decision<TValue, TExplorerState, TMapperValue> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable);
    }
}
