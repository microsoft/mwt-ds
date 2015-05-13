using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionService.Uploader
{
    /// <summary>
    /// Represents the method that will handle the PackageSent event of a <see cref="Microsoft.Research.DecisionService.Uploader"/> object.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="PackageEventArgs"/> object that contains the event data.</param>
    public delegate void PackageSentEventHandler(object sender, PackageEventArgs e);

    /// <summary>
    /// Represents the method that will handle the PackageSendFailed event of a <see cref="Microsoft.Research.DecisionService.Uploader"/> object.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="PackageEventArgs"/> object that contains the event data.</param>
    public delegate void PackageSendFailedEventHandler(object sender, PackageEventArgs e);

    /// <summary>
    /// Provides data for the PackageSent and PackageSendFailed events.
    /// </summary>
    public class PackageEventArgs : EventArgs
    {
        /// <summary>
        /// Records that were included in the event.
        /// </summary>
        public IEnumerable<string> Records { get; set; }

        /// <summary>
        /// The identifier of the package that was sent.
        /// </summary>
        public Guid PackageId { get; set; }

        /// <summary>
        /// The exception which caused the package send to fail.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
