using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Contrib.Cache.Models;
using Orchard;

namespace Contrib.Cache.Services {
    public class CacheService : ICacheService {
        private readonly IWorkContextAccessor _workContextAccessor;

        public CacheService(
            IWorkContextAccessor workContextAccessor) {
            _workContextAccessor = workContextAccessor;
        }

        public IEnumerable<CacheItem> GetCacheItems() {
            var workContext = _workContextAccessor.GetContext();

            foreach (DictionaryEntry cacheEntry in workContext.HttpContext.Cache) {
                var cacheItem = cacheEntry.Value as CacheItem;
                if (cacheItem != null) {
                    yield return cacheItem;
                }
            }
        }

        public void Evict(string cacheKey, HttpContextBase httpContext) {
            httpContext.Cache.Remove(cacheKey);
        }
    }
}