using System;
using System.Reflection;

namespace MultiWorldTesting
{
    internal static class VariableActionHelper
    {
        internal static void ValidateInitialNumberOfActions(uint numActions)
        {
            // Initial number of actions may be set to max value if variable action interface is used
            // Otherwise, it must be a valid number at least 1.
            if (numActions != uint.MaxValue && numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }
        }

        internal static void ValidateNumberOfActions(uint numActions)
        {
            // Actual number of actions at decision time must be a valid positive finite number.
            if (numActions == uint.MaxValue || numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }
        }

        internal static uint GetNumberOfActions(uint numActionsFixed, uint numActionsVariable)
        {
            uint numActions = (numActionsFixed == uint.MaxValue) ? numActionsVariable : numActionsFixed;

            VariableActionHelper.ValidateNumberOfActions(numActions);

            return numActions;
        }
    }
}
