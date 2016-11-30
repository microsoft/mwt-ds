using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using VW;
using VW.Labels;
using VW.Serializer;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceLocal<TContext> : DecisionServiceClient<TContext>
    {
        private VowpalWabbit<TContext> vw;
        // This serves as the base class's recorder/logger as well, but we keep a reference around
        // becauses it exposes additional APIs that aren't part of those interfaces (yet)
        private InMemoryLogger<TContext, int[]> log;

        public int ModelUpdateInterval;
        private int sinceLastUpdate = 0;

        // A snapshot of the current VW model
        public byte[] Model
        {
            get
            {
                using (MemoryStream currModel = new MemoryStream())
                {
                    vw.Native.SaveModel(currModel);
                    return currModel.ToArray();
                }
            }
        }

        public DecisionServiceLocal(string vwArgs, int modelUpdateInterval, TimeSpan expUnit)
            : base(
            new DecisionServiceConfiguration("")
            {
                OfflineMode = true,
                OfflineApplicationID = Guid.NewGuid().ToString(),
                DevelopmentMode = false
            },
            new ApplicationClientMetadata
            {
                TrainArguments = vwArgs,
                InitialExplorationEpsilon = 1f
            },
            new VWExplorer<TContext>(null, JsonTypeInspector.Default, false))
        {
            this.log = new InMemoryLogger<TContext, int[]>(expUnit);
            this.Recorder = log;
            this.vw = new VowpalWabbit<TContext>(
                new VowpalWabbitSettings(vwArgs)
                {
                    TypeInspector = JsonTypeInspector.Default,
                    EnableStringExampleGeneration = true,
                    EnableStringFloatCompact = true
                }
                );
            this.ModelUpdateInterval = modelUpdateInterval;
        }
        
        new public void Dispose()
        {
            base.Dispose();
            vw.Dispose();
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        new public void ReportReward(float reward, string uniqueKey)
        {
            base.ReportReward(reward, uniqueKey);
            sinceLastUpdate++;
            updateModelMaybe();
        }

        public void ReportRewardAndComplete(float reward, string uniqueKey)
        {
            log.ReportRewardAndComplete(uniqueKey, reward);
            sinceLastUpdate++;
            updateModelMaybe();
        }

        private void updateModelMaybe()
        {
            if (sinceLastUpdate >= ModelUpdateInterval)
            {
                foreach (var dp in log.FlushCompleteEvents())
                {
                    uint action = (uint)((int[])dp.InteractData.Value)[0];
                    var label = new ContextualBanditLabel(action, -dp.Reward, ((GenericTopSlotExplorerState)dp.InteractData.ExplorerState).Probabilities[action - 1]);
                    vw.Learn((TContext)dp.InteractData.Context, label, index: (int)label.Action - 1);
                }
                using (MemoryStream currModel = new MemoryStream())
                {
                    vw.Native.SaveModel(currModel);
                    currModel.Position = 0;
                    this.UpdateModel(currModel);
                    sinceLastUpdate = 0;
                }
            }
        }
    }
}
