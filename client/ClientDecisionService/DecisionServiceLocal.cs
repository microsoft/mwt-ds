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
                    EnableStringExampleGeneration = this.config.DevelopmentMode,
                    EnableStringFloatCompact = true
                }
                );
            this.ModelUpdateInterval = modelUpdateInterval;
        }
        
        public override void Dispose(bool disposing)
        {
            // Always free unmanaged objects, but conditionally free managed objets if this is being
            // called from Dispose() (as opposed a finalizer, currently not implemented)
            if (disposing)
            {
                if (log != null)
                {
                    log.Dispose();
                    log = null;
                }
            }
            if (vw != null)
            {
                vw.Dispose();
                vw = null;
            }
            base.Dispose();
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public override void ReportReward(float reward, string uniqueKey)
        {
            base.ReportReward(reward, uniqueKey);
            sinceLastUpdate++;
            updateModelMaybe();
        }

        /// <summary>
        /// Identical to ReportReward() but immediately completes the event; call this when using 
        /// manual event completion or if you want to force completion when using an experimental 
        /// unit duration. 
        /// This functionality is not available in the base class and hence is only implemented here.
        /// </summary>
        /// <param name="reward"></param>
        /// <param name="uniqueKey"></param>
        public void ReportRewardAndComplete(float reward, string uniqueKey)
        {
            // Call our logger directly as this method is not part of the ILogger interface (yet)
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
                    var label = new ContextualBanditLabel(action, -dp.Reward, ((GenericTopSlotExplorerState)dp.InteractData.ExplorerState).Probabilities[0]);
                    // A string (json) context needs to be handled specially, since the C# interface
                    // doesn't currently handle the CB label properly
                    if (typeof(TContext) == typeof(string))
                    {
                        // Manually insert the CB label fields into the context
                        string labelStr = string.Format("\"_label_Action\":{0},\"_label_Cost\":{1},\"_label_Probability\":{2},\"_labelIndex\":{3},", 
                            label.Action, label.Cost, label.Probability, label.Action - 1);
                        string context = ((string)dp.InteractData.Context).Insert(1, labelStr);
                        using (var vwJson = new VowpalWabbitJsonSerializer(vw.Native))
                        using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
                        {
                            vwExample.Learn();
                        }
                    }
                    else
                    {
                        vw.Learn((TContext)dp.InteractData.Context, label, index: (int)label.Action - 1);
                    }
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
