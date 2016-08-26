using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Experimentation
{
    public static class Util
    {
        internal class ExpandState
        {
            internal IEnumerable Enumerable;

            internal IEnumerator Enumerator;
        }

        public static IEnumerable<string> Expand(params IEnumerable[] parameters)
        {
            var enumerators = parameters
                .Select(p => new ExpandState { Enumerator = p.GetEnumerator(), Enumerable = p })
                .Where(e => e.Enumerator.MoveNext())
                .ToArray();

            var run = true;
            do
            {
                yield return string.Join(" ", enumerators.Select(e => Convert.ToString(e.Enumerator.Current, CultureInfo.InvariantCulture)));

                for (int i = 0; i < enumerators.Length; i++)
                {
                    var e = enumerators[i];
                    if (e.Enumerator.MoveNext())
                    {
                        break;
                    }
                    else
                    {
                        // reset loop
                        e.Enumerator = e.Enumerable.GetEnumerator();
                        e.Enumerator.MoveNext();
                        if (i == enumerators.Length - 1)
                        {
                            run = false;
                        }
                    }
                }
            }
            while (run);
        }
    }
}
