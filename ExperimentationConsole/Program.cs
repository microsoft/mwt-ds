using Experimentation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExperimentationConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var storageAccount = new CloudStorageAccount(new StorageCredentials("", ""), false);

            var outputDirectory = @"c:\temp\abc";
            var startTimeInclusive = new DateTime(2016, 8, 11, 0, 0, 0);
            var endTimeExclusive = new DateTime(2016, 8, 14, 0, 0, 0);

            using (var writer = new StreamWriter(Path.Combine(outputDirectory, $"{startTimeInclusive:yyyy-MM-dd_HH}-{endTimeExclusive:yyyy-MM-dd_HH}.json")))
            {
                AzureBlobDownloader.Download(storageAccount, startTimeInclusive, endTimeExclusive, writer, outputDirectory).Wait();
            }
        }
    }
}
