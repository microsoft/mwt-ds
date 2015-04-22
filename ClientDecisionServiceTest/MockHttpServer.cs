using Microsoft.Research.DecisionService.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    public class MockHttpServer : IDisposable
    {
        protected MockHttpServer(string uri)
        {
            this.listener = new HttpListener();
            this.listener.Prefixes.Add(uri);
            this.listener.Start();

            this.cancelTokenSource = new CancellationTokenSource();
            this.pollFinishedEvent = new ManualResetEventSlim();
        }

        public void Run()
        {
            this.backendTask = new TaskFactory().StartNew(DoWork, this.cancelTokenSource.Token);
        }

        public void Stop()
        {
            this.cancelTokenSource.Cancel();
            this.pollFinishedEvent.Wait(1000);
            this.listener.Stop();
            this.backendTask.Wait();
            this.backendTask.Dispose();
        }

        public void Dispose()
        {
            this.listener.Close();
        }

        private void DoWork()
        {
            try
            {
                this.Listen();
            }
            catch (Exception ex)
            {
                if (!(ex is HttpListenerException))
                {
                    throw;
                }
            }
            finally 
            {
                this.pollFinishedEvent.Set();
            }
        }

        public virtual void Reset() { }

        protected virtual void Listen() { }

        protected CancellationTokenSource cancelTokenSource;
        protected HttpListener listener;

        private Task backendTask;
        private readonly ManualResetEventSlim pollFinishedEvent;
    }
}
