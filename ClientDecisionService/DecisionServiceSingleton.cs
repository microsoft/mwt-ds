namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using System;

    public class DecisionServiceJsonSingleton : DecisionServiceJson
    {
        private static volatile DecisionServiceJsonSingleton instance;
        private static readonly object syncRoot = new object();

        private DecisionServiceJsonSingleton(DecisionServiceJsonConfiguration config)
            : base(config) { }

        public static DecisionServiceJsonSingleton Create(DecisionServiceJsonConfiguration config)
        {
            if (instance == null)
            {
                lock (syncRoot)
                {
                    if (instance == null)
                    {
                        instance = new DecisionServiceJsonSingleton(config);
                    }
                }
            }

            return instance;
        }

        public static DecisionServiceJsonSingleton Instance { get { return instance; } }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using System;

    public class DecisionServiceJsonSingleton : DecisionServiceJson
    {
        private static volatile DecisionServiceJsonSingleton instance;
        private static readonly object syncRoot = new object();

        private DecisionServiceJsonSingleton(DecisionServiceJsonConfiguration config)
            : base(config) { }

        public static DecisionServiceJsonSingleton Create(DecisionServiceJsonConfiguration config)
        {
            if (instance == null)
            {
                lock (syncRoot)
                {
                    if (instance == null)
                    {
                        instance = new DecisionServiceJsonSingleton(config);
                    }
                }
            }

            return instance;
        }

        public static DecisionServiceJsonSingleton Instance { get { return instance; } }
    }
}