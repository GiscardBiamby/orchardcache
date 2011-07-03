using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using Autofac.Features.Metadata;
using Contrib.Cache.Models;
using Contrib.Cache.Services;
using Contrib.Cache.ViewModels;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Localization;
using Orchard.Mvc.Routes;
using Orchard.Security;
using Orchard.UI.Admin;
using Orchard.UI.Notify;

namespace Contrib.Cache.Controllers {
    [Admin]
    public class AdminController : Controller {
        private readonly IEnumerable<Meta<IRouteProvider>> _routeProviders;
        private readonly ISignals _signals;
        private readonly ICacheService _cacheService;

        public AdminController(
            IOrchardServices services,
            IEnumerable<Meta<IRouteProvider>> routeProviders,
            ISignals signals,
            ICacheService cacheService) {
            _routeProviders = routeProviders;
            _signals = signals;
            _cacheService = cacheService;
            Services = services;
            }

        public IOrchardServices Services { get; set; }
        public Localizer T { get; set; }

        public ActionResult Index() {
            if (!Services.Authorizer.Authorize(StandardPermissions.SiteOwner, T("Not allowed to manage cache")))
                return new HttpUnauthorizedResult();

            var routeConfigurations = new List<RouteConfiguration>();

            foreach (var routeProvider in _routeProviders) {
                // right now, ignore generic routes
                if (routeProvider.Value is StandardExtensionRouteProvider) continue;

                var routeCollection = routeProvider.Value.GetRoutes();
                var feature = routeProvider.Metadata["Feature"] as Orchard.Environment.Extensions.Models.Feature;

                // if there is no feature, skip route
                if (feature == null) continue;

                foreach (var routeDescriptor in routeCollection) {
                    var route = (Route)routeDescriptor.Route;

                    // ignore admin routes
                    if (route.Url.StartsWith("Admin/") || route.Url == "Admin") continue;

                    routeConfigurations.Add(new RouteConfiguration {
                        RouteDescriptor = routeDescriptor,
                        CacheSettings = new OutputCacheParameters(),
                        FeatureName =
                            String.IsNullOrWhiteSpace(feature.Descriptor.Name)
                                ? feature.Descriptor.Id
                                : feature.Descriptor.Name
                    });
                }
            }

            var settings = Services.WorkContext.CurrentSite.As<CacheSettingsPart>();

            var model = new IndexViewModel {
                CacheItems = _cacheService.GetCacheItems().ToList().OrderByDescending(x => x.CachedOnUtc),
                DefaultCacheDuration = settings.DefaultCacheDuration,
                FeatureRouteConfigurations =routeConfigurations
                        .GroupBy(x => x.FeatureName)
                        .ToDictionary(x => x.Key, x => x.Select(y => y))
            };

            return View(model);
        }

        [HttpPost, ActionName("Index")]
        public ActionResult IndexPost() {
            if (!Services.Authorizer.Authorize(StandardPermissions.SiteOwner, T("Not allowed to manage cache")))
                return new HttpUnauthorizedResult();

            var model = new IndexViewModel();
            if(TryUpdateModel(model)) {
                var settings = Services.WorkContext.CurrentSite.As<CacheSettingsPart>();
                settings.DefaultCacheDuration = model.DefaultCacheDuration;
                _signals.Trigger("CacheSettingsPart");

                Services.Notifier.Information(T("Cache Settings saved successfully."));
            }
            else {
                Services.Notifier.Error(T("Could not save Cache Settings."));
            }

            return RedirectToAction("Index");
        }

        public ActionResult Evict(string cacheKey) {
            if (!Services.Authorizer.Authorize(StandardPermissions.SiteOwner, T("Not allowed to manage cache")))
                return new HttpUnauthorizedResult();

            HttpContext.Cache.Remove(cacheKey);

            return RedirectToAction("Index");
        }
    }
}