using System.Web.UI;
using Orchard.Mvc.Routes;

namespace Contrib.Cache.ViewModels {
    public class RouteConfiguration {
        public RouteDescriptor RouteDescriptor { get; set; }
        public OutputCacheParameters CacheSettings { get; set; }
        public string FeatureName { get; set; }
    }
}