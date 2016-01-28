using System;

namespace MultiWorldTesting
{
    /// <summary>
    /// Represents a unique identifier for an event.
    /// </summary>
    public class UniqueEventID
    {
        /// <summary>
        /// The key for the event.
        /// </summary>
        /// <remarks>
        /// This key is used as a seed to the randomization, which
        /// ensures consistent experience within same event, but
        /// random across events.
        /// </remarks>
        public string Key { get; set; }

        public int Id { get; set; }

        /// <summary>
        /// The time stamp of the event which, together with the key,
        /// uniquely identify an event. Events with same key
        /// must have the same time stamp.
        /// </summary>
        public DateTime TimeStamp { get; set; }
    }
}
