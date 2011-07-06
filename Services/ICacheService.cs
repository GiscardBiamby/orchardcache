using System.Collections.Generic;
using System.Web;
using Orchard;
using Contrib.Cache.Models;

namespace Contrib.Cache.Services {
    public interface ICacheService : IDependency {
        /// <summary>
        /// Returns all current cached pages
        /// </summary>
        IEnumerable<CacheItem> GetCacheItems();

        /// <summary>
        /// Removes a specific cached page from the cache
        /// </summary>
        void Evict(string cacheKey, HttpContextBase httpContext);
    }
}