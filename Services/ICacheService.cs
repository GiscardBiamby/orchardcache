using System.Collections.Generic;
using Orchard;
using Contrib.Cache.Models;

namespace Contrib.Cache.Services {
    public interface ICacheService : IDependency {
        IEnumerable<CacheItem> GetCacheItems();
    }
}