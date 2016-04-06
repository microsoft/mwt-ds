using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public abstract class AbstractModelListener : IDisposable
    {
        IModelSender modelSender;

        internal void Subscribe(IModelSender modelSender)
        {
            this.Unsubscribe();
            this.modelSender = modelSender;
            this.modelSender.Send += this.Receive;
        }

        internal void Unsubscribe()
        {
            if (this.modelSender != null)
            {
                this.modelSender.Send -= this.Receive;
                this.modelSender = null;
            }
        }

        internal abstract void Receive(object sender, Stream model);

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.DisposeInternal();

                if (this.modelSender != null)
                {
                    this.modelSender.Send -= this.Receive;
                }
            }
        }

        internal virtual void DisposeInternal() { }
    }
}
