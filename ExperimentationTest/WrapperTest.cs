using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.IO.Compression;
using Experimentation;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections.Generic;
using VW;

namespace ExperimentationTest
{
    [TestClass]
    public class WrapperTest
    {
        [TestMethod]
        public void WrapperVersusCommandLine()
        {
            var vwArgs = "--cb_explore_adf --cb_type dr --epsilon 0.2";
            int numExamples = 1000;
            var inputFile = Path.GetTempFileName();
            var vwFile = inputFile + ".vw.gz";
            var wrapperPredictionFile = inputFile + ".vw.wrapper.pred";
            var commandLinePredictionFile = inputFile + ".vw.commandline.pred";

            var rand = new Random();
            var contexts = new bool[numExamples].Select((_, i) => TestContext.CreateRandom(rand, i));

            File.WriteAllLines(inputFile, contexts.Select(c => JsonConvert.SerializeObject(c)));

            using (var reader = new StreamReader(inputFile))
            using (var writer = new StreamWriter(new GZipStream(File.Create(vwFile), CompressionLevel.Optimal)))
            {
                VowpalWabbitJsonToString.Convert(reader, writer);
            }

            OfflineTrainer.Train(vwArgs, inputFile, wrapperPredictionFile);

            Process.Start(new ProcessStartInfo
            {
                FileName = "vw.exe",
                Arguments = $"{vwArgs} -p {commandLinePredictionFile} -d {vwFile}",
                CreateNoWindow = true,
                UseShellExecute = false
            }).WaitForExit();

            var wrapperActionScore = File
                .ReadAllLines(wrapperPredictionFile)
                .Select(l => JsonConvert.DeserializeObject<WrapperPredictionLine>(l))
                .ToList();

            var commandLineActionScore = File
                .ReadAllLines(commandLinePredictionFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries))
                .Select(ll => ll.Select(l => l.Split(':')).Select(l => new { Action = Convert.ToInt32(l[0]), Prob = Convert.ToSingle(l[1]) }))
                .ToList();

            Assert.AreEqual(wrapperActionScore.Count, commandLineActionScore.Count);

            for (int i = 0; i < wrapperActionScore.Count; i++)
            {
                Assert.IsTrue(commandLineActionScore[i].Select(ap => ap.Action).SequenceEqual(wrapperActionScore[i].Actions));
                Assert.IsTrue(commandLineActionScore[i].Select(ap => ap.Prob).SequenceEqual(wrapperActionScore[i].Probs, new FloatComparer()));
            }

            File.Delete(inputFile);
            File.Delete(vwFile);
            File.Delete(wrapperPredictionFile);
        }

        class FloatComparer : IEqualityComparer<float>
        {
            public bool Equals(float x, float y)
            {
                return Math.Abs(x - y) < 1e-6;
            }

            public int GetHashCode(float obj)
            {
                return obj.GetHashCode();
            }
        }

        class WrapperPredictionLine
        {
            [JsonProperty("nr")]
            public int Number { get; set; }

            [JsonProperty("as")]
            public int[] Actions { get; set; }

            [JsonProperty("p")]
            public float[] Probs { get; set; }
        }
    }
}
