using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public sealed class StartStopAzureEmulator
    {
        private static bool _wasUp;
        
        [AssemblyInitialize]
        public static void StartAzureBeforeAllTestsIfNotUp(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext context)
        {
            if (!AzureStorageEmulatorManager.IsProcessStarted())
            {
                AzureStorageEmulatorManager.StartStorageEmulator();
                _wasUp = false;
            }
            else
            {
                _wasUp = true;
            }

        }

        [AssemblyCleanup]
        public static void StopAzureAfterAllTestsIfWasDown()
        {
            if (!_wasUp)
            {
                AzureStorageEmulatorManager.StopStorageEmulator();
            }
            else
            {
                // Leave as it was before testing...
            }
        }
    }
}
