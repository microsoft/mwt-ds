using System;
using System.Linq;
using System.Runtime.Caching;
using MA = Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction;
using SA = Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public static class DecisionServiceFactory
    {
        private static MemoryCache dsCache = new MemoryCache("DecisionServiceCache");
        private static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromHours(24);

        /// <summary>
        /// Inserts a <see cref="SA.DecisionServiceJson"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="config">The configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="SA.DecisionServiceJson"/> object.</returns>
        public static SA.DecisionServiceJson AddOrGetExisting(string token, SA.DecisionServiceJsonConfiguration config, TimeSpan? expirationTime = null)
        {
            return DecisionServiceFactory.AddOrGetExisting(token, () => config, expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="SA.DecisionServiceJson"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="configCreator">A callback to retrieve the configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="SA.DecisionServiceJson"/> object.</returns>
        public static SA.DecisionServiceJson AddOrGetExisting(string token, Func<SA.DecisionServiceJsonConfiguration> configCreator, TimeSpan? expirationTime = null)
        {
            return InternalAddOrGetExisting(token, new Lazy<SA.DecisionServiceJson>(() => new SA.DecisionServiceJson(configCreator())), expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="SA.DecisionService<TContext>"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="config">The configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="SA.DecisionService<TContext>"/> object.</returns>
        public static SA.DecisionService<TContext> AddOrGetExisting<TContext>(string token, SA.DecisionServiceConfiguration<TContext> config, TimeSpan? expirationTime = null)
        {
            return DecisionServiceFactory.AddOrGetExisting(token, () => config, expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="SA.DecisionService<TContext>"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="configCreator">A callback to retrieve the configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="SA.DecisionService<TContext>"/> object.</returns>
        public static SA.DecisionService<TContext> AddOrGetExisting<TContext>(string token, Func<SA.DecisionServiceConfiguration<TContext>> configCreator, TimeSpan? expirationTime = null)
        {
            return InternalAddOrGetExisting(token, new Lazy<SA.DecisionService<TContext>>(() => new SA.DecisionService<TContext>(configCreator())), expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="MA.DecisionServiceJson"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="config">The configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="MA.DecisionServiceJson"/> object.</returns>
        public static MA.DecisionServiceJson AddOrGetExisting(string token, MA.DecisionServiceJsonConfiguration config, TimeSpan? expirationTime = null)
        {
            return DecisionServiceFactory.AddOrGetExisting(token, () => config, expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="MA.DecisionServiceJson"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="configCreator">A callback to retrieve the configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="MA.DecisionServiceJson"/> object.</returns>
        public static MA.DecisionServiceJson AddOrGetExisting(string token, Func<MA.DecisionServiceJsonConfiguration> configCreator, TimeSpan? expirationTime = null)
        {
            return InternalAddOrGetExisting(token, new Lazy<MA.DecisionServiceJson>(() => new MA.DecisionServiceJson(configCreator())), expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="MA.DecisionService<TContext, TActionDependentFeatures>"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="config">The configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="MA.DecisionService<TContext, TActionDependentFeatures>"/> object.</returns>
        public static MA.DecisionService<TContext, TActionDependentFeatures> AddOrGetExisting<TContext, TActionDependentFeatures>(
            string token, MA.DecisionServiceConfiguration<TContext, TActionDependentFeatures> config, TimeSpan? expirationTime = null)
        {
            return DecisionServiceFactory.AddOrGetExisting(token, () => config, expirationTime);
        }

        /// <summary>
        /// Inserts a <see cref="MA.DecisionService<TContext, TActionDependentFeatures>"/> object into the cache using the specified token key, configuration and expiration time.
        /// </summary>
        /// <param name="token">The token which identifies the decision service application.</param>
        /// <param name="configCreator">A callback to retrieve the configuration for the decision service object.</param>
        /// <param name="expirationTime">
        /// The expiration time until the decision service object is removed from cache and disposed.
        /// At dispose time, any events pending will be uploaded to the join server.
        /// </param>
        /// <returns>The <see cref="MA.DecisionService<TContext, TActionDependentFeatures>"/> object.</returns>
        public static MA.DecisionService<TContext, TActionDependentFeatures> AddOrGetExisting<TContext, TActionDependentFeatures>(
            string token, Func<MA.DecisionServiceConfiguration<TContext, TActionDependentFeatures>> configCreator, TimeSpan? expirationTime = null)
        {
            return InternalAddOrGetExisting(
                token,
                new Lazy<MA.DecisionService<TContext, TActionDependentFeatures>>(() => new MA.DecisionService<TContext, TActionDependentFeatures>(configCreator())),
                expirationTime);
        }

        /// <summary>
        /// Remove and dispose all objects in the cache.
        /// </summary>
        public static void EvictAll()
        {
            var cacheKeyList = dsCache.Select(item => item.Key).ToList();
            foreach (var key in cacheKeyList)
            {
                dsCache.Remove(key);
            }
        }

        private static T InternalAddOrGetExisting<T>(string token, Lazy<T> obj, TimeSpan? expirationTime = null)
        {
            var oldObj = (Lazy<T>)dsCache.AddOrGetExisting(token, obj, new CacheItemPolicy
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
    }
}
