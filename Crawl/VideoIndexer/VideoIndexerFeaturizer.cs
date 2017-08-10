using Microsoft.DecisionService.Crawl;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Crawl.VideoIndexer
{
    public static class VideoIndexerFeaturizer
    {
        public static JObject FeaturizeVideoIndexerBreakdown(VideoBreakdownResult result)
        {
            var output = new JObject();

            output.Add(new JProperty("durationInSeconds", result.DurationInSeconds));
            if (result.SummarizedInsights != null)
            {
                if (result.SummarizedInsights.Faces != null)
                {
                    var lfaces = result.SummarizedInsights.Faces
                            .Where(f => !f.Name.StartsWith("Unknown"))
                            .GroupBy(f => f.Name)
                            .Select(f =>
                            {
                                var x = f.First();
                                return new JProperty(x.Name, x.SeenDurationRatio);
                            })
                            .ToArray();

                    if (lfaces.Length > 0)
                        output.Add(new JProperty("LFacesRatio", new JObject(lfaces)));

                    var faces = result.SummarizedInsights.Faces
                            .Where(f => !f.Name.StartsWith("Unknown"))
                            .GroupBy(f => f.Name)
                            .Select(f =>
                            {
                                var x = f.First();
                                return new[]
                                {
                                    new JProperty(x.Name, x.SeenDuration)
                                };
                            })
                            .ToArray();

                    if (faces.Length > 0)
                        output.Add(new JProperty("HFacesDuration", new JObject(faces)));
                }

                if (result.SummarizedInsights.Topics != null)
                {
                    output.Add(new JProperty("JTopics",
                        new JObject(result.SummarizedInsights.Topics
                            .Select(t => t.Name)
                            .Distinct()
                            .Select(t => new JProperty(t, 1)))));
                }

                if (result.SummarizedInsights.Sentiments != null)
                {
                    output.Add(new JProperty("KSentiments",
                        new JObject(
                        result.SummarizedInsights.Sentiments
                            .GroupBy(f => f.SentimentKey)
                            .Select(f =>
                            {
                                var x = f.First();
                                return new JProperty(x.SentimentKey, x.SeenDurationRatio);
                            }))));
                }
            }

            if (result.Breakdowns != null)
            {
                var bd = result.Breakdowns.FirstOrDefault();
                if (bd != null)
                {
                    if (bd.Insight != null)
                    {
                        if (bd.Insight.ContentModeration != null)
                            output.Add(new JProperty("BContentModeration",
                                new JObject(
                                    new JProperty("AdultClassifierValue", bd.Insight.ContentModeration.AdultClassifierValue),
                                    new JProperty("BannedWordsCount", bd.Insight.ContentModeration.BannedWordsCount),
                                    new JProperty("IsAdult", bd.Insight.ContentModeration.IsAdult),
                                    new JProperty("IsSuspectedAsAdult", bd.Insight.ContentModeration.IsSuspectedAsAdult))));

                        if (bd.Insight.TranscriptBlocks != null)
                        {
                            var lines = bd.Insight.TranscriptBlocks
                                .Where(t => t.Lines != null)
                                .SelectMany(t => t.Lines)
                                .Select(t => t.Text)
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToArray();

                            if (lines.Length > 0)
                                output.Add(new JProperty("Text",
                                    new JObject(
                                        new JProperty("_text", string.Join(" ", lines)))));
                        }
                    }
                }
            }

            return output;
        }
    }
}