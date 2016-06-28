using System;
using System.Runtime.InteropServices;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
	/// Represents a feature in a sparse array.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct Feature
	{
        public float Value;
        public UInt32 Id;
	};
}
