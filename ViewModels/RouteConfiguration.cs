using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace Contrib.Cache.ViewModels {
    public class RouteConfiguration {
        public string RouteKey { get; set; }
        public string Url { get; set; }
        public int Priority { get; set; }
        public int? Duration { get; set; }
        public string FeatureName { get; set; }
    }
}