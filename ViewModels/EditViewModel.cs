using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contrib.Cache.ViewModels {
    public class EditViewModel {
        public Dictionary<string, IEnumerable<RouteConfiguration>> FeatureRouteConfigurations { get; set; }
        [Range(0, int.MaxValue), Required]
        public int DefaultCacheDuration { get; set; }
    }
}