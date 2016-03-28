using System;
using System.Globalization;
using System.Text;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    /// <summary>
	/// A sample recorder class that converts the exploration tuple into string format.
	/// </summary>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public class StringRecorder<TContext, TAction, TExplorerState, TPolicyState> : IRecorder<TContext, TAction, TExplorerState, TPolicyState>
        where TContext : IStringContext
	{
        private StringBuilder recordingBuilder;

        public StringRecorder()
		{
            recordingBuilder = new StringBuilder();
		}

        /// <summary>
        /// Records the exploration data associated with a given decision.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <param name="action">Chosen by an exploration algorithm given context.</param>
        /// <param name="probability">The probability of the chosen action given context.</param>
        /// <param name="uniqueKey">A user-defined identifer for the decision.</param>
        public void Record(TContext context, Decision<TAction, TExplorerState, TPolicyState> decision, UniqueEventID uniqueKey)
        {
            recordingBuilder.Append(Convert.ToString(decision.Action, CultureInfo.InvariantCulture));
            recordingBuilder.Append(' ');
            recordingBuilder.Append(uniqueKey.Key);
            recordingBuilder.Append(' ');

            recordingBuilder.Append(Convert.ToString(decision.ExplorerState, CultureInfo.InvariantCulture));
            recordingBuilder.Append(' ');
            recordingBuilder.Append(Convert.ToString(decision.PolicyDecision.PolicyState, CultureInfo.InvariantCulture));

            recordingBuilder.Append(" | ");
            recordingBuilder.Append(((IStringContext)context).ToString());
            recordingBuilder.Append("\n");
        }

		/// <summary>
		/// Gets the content of the recording so far as a string and optionally clears internal content.
		/// </summary>
		/// <param name="flush">A boolean value indicating whether to clear the internal content.</param>
		/// <returns>
		/// A string with recording content.
		/// </returns>
        public string GetRecording(bool flush = true)
		{
            string recording = this.recordingBuilder.ToString();

            if (flush)
            {
                this.recordingBuilder.Clear();
            }

            return recording;
		}
    };
}
