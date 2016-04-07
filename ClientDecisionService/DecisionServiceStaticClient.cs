using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Linq;
using System.Runtime.Caching;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public static class DecisionServiceStaticClient
    {
        private static MemoryCache dsCache = new MemoryCache("DecisionServiceCache");
        private static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromHours(24);

        public static T AddOrGetExisting<T>(
            string token,
            Func<string, T> clientCreator, TimeSpan? expirationTime = null)
        {
            var obj = new Lazy<T>(() => clientCreator(token));

            var oldObj = (Lazy<T>)dsCache.AddOrGetExisting(
                token,
                obj,
                new CacheItemPolicy
                {
                    SlidingExpiration = expirationTime ?? DefaultExpirationTime,
                    RemovedCallback = (cacheEntryRemovedArguments) =>
                    {
                        var dsObject = cacheEntryRemovedArguments.CacheItem.Value as IDisposable;
                        if (dsObject != null)
                        {
                            dsObject.Dispose();
                        }
                    }
                });

            return (oldObj ?? obj).Value;
        }

        /// <summary>
        /// Remove and dispose all objects in the cache.
        /// </summary>
        public static void EvictAll()
        {
            var cacheKeyList = dsCache.Select(item => item.Key).ToList();
            foreach (var key in cacheKeyList)
                dsCache.Remove(key);
        }
    }
}
