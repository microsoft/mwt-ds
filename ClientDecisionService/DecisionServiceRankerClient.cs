using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceRankerClient<TContext, TExplorerState, TMapperValue> : DecisionServiceBaseClient<TContext, uint[], TExplorerState, TMapperValue>
    {
        public DecisionServiceRankerClient(DecisionServiceConfiguration config, ApplicationTransferMetadata metaData, IExplorer<TContext, uint[], TExplorerState, TMapperValue> explorer)
            : base(config, metaData, explorer)
        {
        }

        public uint[] ChooseActions(UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return this.mwtExplorer.MapContext(uniqueKey, context, numActionsVariable);
        }
    }
}
