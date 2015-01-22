using MultiWorldTesting;
using System;
using System.Collections.Generic;

namespace DecisionServiceTest
{
    class TestContext { }

    class TestOutcome { }

    class TestPolicy : IPolicy<TestContext>
    {
        public uint ChooseAction(TestContext context)
        {
            // Always returns the same action regardless of context
            return 5;
        }
    }
}
