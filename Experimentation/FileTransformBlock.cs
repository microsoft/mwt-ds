using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Experimentation
{
    public class Line
    {
        public string Content { get; set; }

        public int Number { get; set; }
    }


    public static class FileTransformBlock
    {
        public static ISourceBlock<T> Create<T>(string file, Func<Line, T> transform)
        {
            var block = new TransformBlock<Line, T>(
                transform,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    BoundedCapacity = 128
                });

            new Thread(() =>
            {
                var input = block.AsObserver();
                try
                {
                    using (var reader = new StreamReader(file))
                    {
                        string line;
                        int lineNr = 0;
                        var batch = new List<Line>();
                        while ((line = reader.ReadLine()) != null)
                            input.OnNext(new Line { Content = line, Number = lineNr++ });

                        input.OnCompleted();
                    }
                }
                catch (Exception ex)
                {
                    input.OnError(ex);
                }
            }).Start();

            return block;
        }

        public static ISourceBlock<List<T>> CreateBatch<T>(string file, Func<Line, T> transform)
        {
            var block = new TransformBlock<List<Line>, List<T>>(
                batch => batch.Select(transform).ToList(),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8,
                    BoundedCapacity = 128
                });

            new Thread(() =>
            {
                var input = block.AsObserver();
                try
                {
                    using (var reader = new StreamReader(file))
                    {
                        string line;
                        int lineNr = 0;
                        var batch = new List<Line>();
                        while ((line = reader.ReadLine()) != null)
                        {
                            batch.Add(new Line { Content = line, Number = lineNr++ });
                            if (batch.Count >= 512)
                            {
                                input.OnNext(batch);
                                batch = new List<Line>();
                            }
                        }

                        if (batch.Count > 0)
                            input.OnNext(batch);

                        input.OnCompleted();
                    }
                }
                catch (Exception ex)
                {
                    input.OnError(ex);
                }
            }).Start();

            return block;
            //    var order = new TransformBlock<List<T>, List<T>>(t => t, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });
            //    block.LinkTo(order, new DataflowLinkOptions { PropagateCompletion = true });

            //    return order;
        }

        public static ISourceBlock<IList<T>> Batch<T>(int n, ISourceBlock<T> source)
        {
            var block = new BatchBlock<T>(n);
            source.LinkTo(block, new DataflowLinkOptions { PropagateCompletion = true });
            return block;
        }

    }
}
