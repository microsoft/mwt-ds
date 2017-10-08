using Microsoft.Research.MultiWorldTesting.Contract;
//using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using VW;
using VW.Labels;
using VW.Serializer;
using System;
using System.IO;
using System.Threading;


namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceLocal<TContext> : DecisionServiceClient<TContext>
    {
        private VowpalWabbit<TContext> vw = null;
        // String (json) contexts need special handling due to limitations in VW C# interface
        private VowpalWabbit vwJson = null;
        object vwLock = new object();
        private bool vwDisposed = false;
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
                lock (this.vwLock)
                {
                    // Exit gracefully if the object has been disposed
                    if (vwDisposed) return null;
                    using (MemoryStream currModel = new MemoryStream())
                    {
                        VowpalWabbit vwNative = (typeof(TContext) == typeof(string)) ? vwJson.Native : vw.Native;
                        vwNative.SaveModel(currModel);
                        return currModel.ToArray();
                    }
                }
            }
        }

        public DecisionServiceLocal(
            string vwArgs, 
            int modelUpdateInterval, 
            TimeSpan expUnit)
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
            // String (json) contexts require a different type of internal policy
            ((typeof(TContext) == typeof(string)) ? 
                  new VWJsonExplorer(null, false) as IContextMapper<TContext, ActionProbability[]> : 
                  new VWExplorer<TContext>(null, JsonTypeInspector.Default, false)))
        {
            this.log = new InMemoryLogger<TContext, int[]>(expUnit);
            this.Recorder = log;
            // String (json) contexts are handled via a non-generic VW instance, whereas all other
            // context types use a generic VW instance
            if (typeof(TContext) == typeof(string))
            {
                this.vwJson = new VowpalWabbit(
                    new VowpalWabbitSettings(vwArgs)
                    {
                        TypeInspector = JsonTypeInspector.Default,
                        EnableStringExampleGeneration = this.config.DevelopmentMode,
                        EnableStringFloatCompact = true
                    }
                    );
            }
            else
            {
                this.vw = new VowpalWabbit<TContext>(
                    new VowpalWabbitSettings(vwArgs)
                    {
                        TypeInspector = JsonTypeInspector.Default,
                        EnableStringExampleGeneration = this.config.DevelopmentMode,
                        EnableStringFloatCompact = true
                    }
                    );
            }
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
            lock (this.vwLock)
            {
                if (vw != null)
                {
                    vw.Dispose();
                    vw = null;
                }
                if (vwJson != null)
                {
                    vwJson.Dispose();
                    vwJson = null;
                }
                vwDisposed = true;
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
            Interlocked.Increment(ref sinceLastUpdate);
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
            Interlocked.Increment(ref sinceLastUpdate);
            updateModelMaybe();
        }

        private void updateModelMaybe()
        {
            if (sinceLastUpdate >= ModelUpdateInterval)
            {
                // Locking at this level ensures a batch of events is processed completely before
                // the next batch (finer locking would allow interleaving, violating timeorder
                lock (this.vwLock)
                {
                    // Exit gracefully if the object has been disposed
                    if (vwDisposed) return;
                    foreach (var dp in log.FlushCompleteEvents())
                    {
                        uint action = (uint)((int[])dp.InteractData.Value)[0];
                        var label = new ContextualBanditLabel(action, -dp.Reward, ((GenericTopSlotExplorerState)dp.InteractData.ExplorerState).Probabilities[0]);
                        // String (json) contexts need to be handled specially, since the C# interface
                        // does not currently handle the CB label properly
                        if (typeof(TContext) == typeof(string))
                        {
                            // Manually insert the CB label fields into the context
                            string labelStr = string.Format("\"_label_Action\":{0},\"_label_Cost\":{1},\"_label_Probability\":{2},\"_labelIndex\":{3},",
                                label.Action, label.Cost, label.Probability, label.Action - 1);
                            string context = ((string)dp.InteractData.Context).Insert(1, labelStr);
                            using (var vwSerializer = new VowpalWabbitJsonSerializer(vwJson.Native))
                            using (VowpalWabbitExampleCollection vwExample = vwSerializer.ParseAndCreate(context))
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
                        VowpalWabbit vwNative = (typeof(TContext) == typeof(string)) ? vwJson.Native : vw.Native;
                        vwNative.SaveModel(currModel);
                        currModel.Position = 0;
                        this.UpdateModel(currModel);
                        sinceLastUpdate = 0;
                    }
                }
            }
        }
    }
}
