using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    // Start/stop azure storage emulator from code:
    // http://stackoverflow.com/questions/7547567/how-to-start-azure-storage-emulator-from-within-a-program
    // Credits to David Peden http://stackoverflow.com/users/607701/david-peden for sharing this!
    public static class AzureStorageEmulatorManager
    {
        private static readonly string[] _windowsAzureStorageEmulatorPossiblePaths = new string[] 
        { 
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\WAStorageEmulator.exe",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator\AzureStorageEmulator.exe"
        };

        private const string _win7ProcessName = "WAStorageEmulator";
        private const string _win8ProcessName = "WASTOR~1";

        private static Process GetProcess()
        {
            return Process.GetProcessesByName(_win7ProcessName).FirstOrDefault() ?? Process.GetProcessesByName(_win8ProcessName).FirstOrDefault();
        }

        public static bool IsProcessStarted()
        {
            return GetProcess() != null;
        }

        public static void StartStorageEmulator()
        {
            if (!IsProcessStarted())
            {
                foreach (string storageEmulatorPath in _windowsAzureStorageEmulatorPossiblePaths)
                {
                    if (File.Exists(storageEmulatorPath))
                    {
                        using (Process process = Process.Start(new ProcessStartInfo
                        {
                            FileName = storageEmulatorPath,
                            Arguments = "start"
                        }))
                        {
                            process.WaitForExit();
                        }
                        break;
                    }
                }
                
            }
        }

        public static void StopStorageEmulator()
        {
            foreach (string storageEmulatorPath in _windowsAzureStorageEmulatorPossiblePaths)
            {
                if (File.Exists(storageEmulatorPath))
                {
                    using (Process process = Process.Start(new ProcessStartInfo
                    {
                        FileName = storageEmulatorPath,
                        Arguments = "stop",
                    }))
                    {
                        process.WaitForExit();
                    }
                    break;
                }
            }
        }
    }
}
