using FluentScheduler;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal sealed class AzureBlobUpdater : Registry
    {
        private Dictionary<string, AzureBlobUpdateMetadata> taskToMetadata;
        private readonly static AzureBlobUpdater instance = new AzureBlobUpdater();

        internal static AzureBlobUpdater Instance { get { return instance; } }

        internal static void RegisterTask(
            string taskId, string blobAddress,
            string blobConnectionString, string blobOutputDir, TimeSpan pollDelay,
            Action<string> notifyBlobUpdate, Action<Exception> notifyPollFailure)
        {
            AzureBlobUpdater.Instance.Add(taskId,
                new AzureBlobUpdateMetadata(
                    taskId, blobAddress, 
                    blobConnectionString, blobOutputDir, pollDelay,
                    notifyBlobUpdate, notifyPollFailure));
        }

        internal static void CancelTask(string taskId)
        {
            AzureBlobUpdater.Instance.Cancel(taskId);
        }

        internal static void Start()
        {
            TaskManager.Initialize(instance);
        }

        internal static void Stop()
        {
            instance.CancelAll();
        }

        private AzureBlobUpdater()
        {
            this.taskToMetadata = new Dictionary<string, AzureBlobUpdateMetadata>();
        }

        private void Add(string taskId, AzureBlobUpdateMetadata taskMetadata)
        {
            if (this.taskToMetadata.ContainsKey(taskId))
            {
                // If task already exists, updates it to the new schedule
                this.Cancel(taskId);
            }
            taskMetadata.Scheduler = Schedule(() => 
            {
                var updateTask = new AzureBlobUpdateTask(taskMetadata);
                updateTask.Execute();
                updateTask.Stop(immediate: true);
            });
            taskMetadata.Scheduler.WithName(taskId).ToRunNow().AndEvery((int)taskMetadata.BlobPollDelay.TotalSeconds).Seconds();
            this.taskToMetadata.Add(taskId, taskMetadata);
        }

        private void Cancel(string taskId)
        {
            if (this.taskToMetadata.ContainsKey(taskId))
            {
                this.taskToMetadata[taskId].Scheduler.Disable();
                this.taskToMetadata.Remove(taskId);
            }
            TaskManager.RemoveTask(taskId);
        }

        private void CancelAll()
        {
            foreach (string taskId in this.taskToMetadata.Keys.ToList())
            {
                this.Cancel(taskId);
            }
            this.taskToMetadata.Clear();
        }
    }
}
