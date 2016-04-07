using System;
using System.Reflection;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    internal static class VariableActionHelper
    {
        internal static void ValidateInitialNumberOfActions(int numActions)
        {
            // Initial number of actions may be set to max value if variable action interface is used
            // Otherwise, it must be a valid number at least 1.
            if (numActions != int.MaxValue && numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }
        }

        internal static void ValidateNumberOfActions(int numActions)
        {
            // Actual number of actions at decision time must be a valid positive finite number.
            if (numActions == int.MaxValue || numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }
        }

        internal static int GetNumberOfActions(int numActionsFixed, int numActionsVariable)
        {
            int numActions = (numActionsFixed == int.MaxValue) ? numActionsVariable : numActionsFixed;

            VariableActionHelper.ValidateNumberOfActions(numActions);

            return numActions;
        }
    }
}
