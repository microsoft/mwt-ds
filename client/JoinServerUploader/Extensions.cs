using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    internal static class ExtensionMethods
    {
        internal static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, int maxCount, int maxSize, Func<T, int> measure)
        {
            return Observable.Create<IList<T>>(obs =>
            {
                var state = new List<T>();
                var size = 0;
                return source.Subscribe(
                    onNext: v =>
                    {
                        size += measure(v);
                        state.Add(v);

                        if (size >= maxSize || state.Count >= maxCount)
                        {
                            obs.OnNext(state);

                            state = new List<T>();
                            size = 0;
                        }
                    },
                    onError: e => obs.OnError(e),
                    onCompleted: () =>
                    {
                        if (state.Count > 0)
                        {
                            obs.OnNext(state);
                        }
                        obs.OnCompleted();
                    });
            });
        }
    }
}
