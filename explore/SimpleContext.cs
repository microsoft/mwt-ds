using System.Linq;
using System.Globalization;
using System.Text;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    /// <summary>
	/// A sample context class that stores a vector of Features.
	/// </summary>
	public class SimpleContext
	{
        public SimpleContext(float[] features)
		{
            this.Features = features;
		}

        public override string ToString()
		{
            return string.Join(" ", this.Features);
		}

        public float[] Features { get; set; }
	};
}
