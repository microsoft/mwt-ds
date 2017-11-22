using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public interface IDecisionServiceClient<TContext> : IDisposable
    {
        Task DownloadModelAndUpdate(CancellationToken cancellationToken);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context, IRanker<TContext> defaultRanker);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context, IRanker<TContext> defaultRanker, bool doNotLog);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context, int[] defaultActions);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context, int[] defaultActions, bool doNotLog);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context);

        Task<int[]> ChooseRankingAsync(string uniqueKey, TContext context, bool doNotLog);

        void ReportReward(float reward, string uniqueKey);

        void ReportOutcome(object outcome, string uniqueKey);

        void UpdateModel(Stream model);
    }
}
