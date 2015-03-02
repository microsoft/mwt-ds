using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;

namespace ClientDecisionService
{
    public static class ExtensionMethods
    {
        public static IObservable<IList<T>> Buffer<T>(this IObservable<T> source, int maxCount, int maxSize, Func<T, int> measure)
        {
            var subject = new Subject<IList<T>>();
            var state = new List<T>();
            var size = 0;

            // TODO: handle IDisposable
            source.Subscribe(Observer.Create<T>(
                onNext: v =>
                {
                    size += measure(v);
                    state.Add(v);

                    if (size >= maxSize || state.Count >= maxCount)
                    {
                        subject.OnNext(state);

                        state = new List<T>();
                        size = 0;
                    }
                },
                onError: e => subject.OnError(e),
                onCompleted: () =>
                {
                    if (state.Count > 0)
                    {
                        subject.OnNext(state);
                    }
                    subject.OnCompleted();
                }));

            return subject;
        }
    }
}
