using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionService.Uploader
{
    /// <summary>
    /// An equality comparer with type checking.
    /// </summary>
    /// <typeparam name="T">The type of the object to compare.</typeparam>
    public class StrictTypedEqualityComparer<T> : IEqualityComparer<object>
        where T : class
    {
        private IEqualityComparer<T> equalityComparer;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="equalityComparer">The comparer instance.</param>
        public StrictTypedEqualityComparer(IEqualityComparer<T> equalityComparer)
        {
            this.equalityComparer = equalityComparer;
        }

        bool IEqualityComparer<object>.Equals(object x, object y)
        {
            if (x == null && y == null)
            {
                return false;
            }

            var xt = x as T;
            var yt = y as T;

            if (xt == null || yt == null)
            {
                return false;
            }

            return this.equalityComparer.Equals(xt, yt);
        }

        int IEqualityComparer<object>.GetHashCode(object obj)
        {
            var objt = obj as T;
            if (objt == null)
            {
                return -1;
            }

            return this.equalityComparer.GetHashCode(objt);
        }
    }
}
