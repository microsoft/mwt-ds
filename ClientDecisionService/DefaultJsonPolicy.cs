namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;

    public class DefaultJsonPolicy : IPolicy<string>
    {
        public PolicyDecisionTuple ChooseAction(string context, uint numActionsVariable = uint.MaxValue)
        {
            return new PolicyDecisionTuple
            {
                Action = 1
            };
        }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
    using System.Linq;

    public class DefaultJsonPolicy : IPolicy<string>
    {
        public PolicyDecisionTuple ChooseAction(string context, uint numActionsVariable = uint.MaxValue)
        {
            return new PolicyDecisionTuple
            {
                Actions = Enumerable.Range(1, (int)numActionsVariable).Select(a => (uint)a).ToArray()
            };
        }
    }
}
