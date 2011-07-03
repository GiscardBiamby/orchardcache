using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Contrib.Cache.Models;

namespace Contrib.Cache.ViewModels {
    public class IndexViewModel {
        public IEnumerable<CacheItem> CacheItems { get; set; } 
        public Dictionary<string, IEnumerable<RouteConfiguration>> FeatureRouteConfigurations { get; set; }
        [Range(0, int.MaxValue), Required]
        public int DefaultCacheDuration { get; set; }
    }
}