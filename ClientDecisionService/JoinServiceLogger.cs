using JoinServerUploader;
using Newtonsoft.Json;
using System;

namespace ClientDecisionService
{
    internal class JoinServiceLogger<TContext> : ILogger<TContext>, IDisposable
    {
        public JoinServiceLogger(BatchingConfiguration batchConfig, 
            Func<TContext, string> contextSerializer, 
            string authorizationToken,
            string loggingServiceBaseAddress)
        {
            this.eventUploader = new EventUploader(batchConfig, loggingServiceBaseAddress);
            this.eventUploader.InitializeWithToken(authorizationToken);
            this.contextSerializer = contextSerializer ?? (x => x == null ? null : JsonConvert.SerializeObject(x));
        }

        public void Record(TContext context, uint[] actions, float probability, string uniqueKey)
        {
            this.eventUploader.Upload(new Interaction
            { 
                Key = uniqueKey,
                Actions = actions,
                Probability = probability,
                Context = this.contextSerializer(context)
            });
        }

        public void ReportReward(float reward, string uniqueKey)
        {
            this.eventUploader.Upload(new Observation
            {
                Key = uniqueKey,
                Value = JsonConvert.SerializeObject(new { Reward = reward })
            });
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            this.eventUploader.Upload(new Observation
            {
                Key = uniqueKey,
                Value = outcomeJson
            });
        }

        public void Flush()
        {
            this.eventUploader.Flush();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.eventUploader != null)
                {
                    this.eventUploader.Dispose();
                    this.eventUploader = null;
                }
            }
        }

        #region Members
        private EventUploader eventUploader;
        private readonly Func<TContext, string> contextSerializer;
        #endregion
    }
}
