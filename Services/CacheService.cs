using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Contrib.Cache.Models;
using Contrib.Cache.ViewModels;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Data;
using Orchard.DisplayManagement;

namespace Contrib.Cache.Services {
    public class CacheService : ICacheService {
        private readonly IWorkContextAccessor _workContextAccessor;
        private readonly IRepository<CacheParameterRecord> _repository;
        private readonly ICacheManager _cacheManager;
        private readonly ISignals _signals;
        private readonly Lazy<IContentManager> _contentManager;
        private readonly Lazy<IDisplayHelperFactory> _displayHelperFactory;

        public CacheService(
            IWorkContextAccessor workContextAccessor,
            IRepository<CacheParameterRecord> repository,
            ICacheManager cacheManager,
            ISignals signals,
            Lazy<IContentManager> contentManager,
            Lazy<IDisplayHelperFactory> displayHelperFactory) {
            _workContextAccessor = workContextAccessor;
            _repository = repository;
            _cacheManager = cacheManager;
            _signals = signals;
            _contentManager = contentManager;
            _displayHelperFactory = displayHelperFactory;
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

        public string GetRouteDescriptorKey(string url, RouteValueDictionary routeValueDictionary) {
            var keyBuilder = new StringBuilder();
            keyBuilder.AppendFormat("url={0};", url);

            // the data tokens are used in case the same url is used by several features, like *{path} (Rewrite Rules and Home Page Provider)
            if (routeValueDictionary != null) {
                foreach (var key in routeValueDictionary.Keys) {
                    keyBuilder.AppendFormat("{0}={1};", key, routeValueDictionary[key]);
                }
            }

            return keyBuilder.ToString().ToLowerInvariant();
        }

        public CacheParameterRecord GetCacheParameterByKey(string key) {
            return _repository.Get(c => c.RouteKey == key);
        }

        public IEnumerable<RouteConfiguration> GetRouteConfigurations() {
            return _cacheManager.Get("GetRouteConfigurations",
                ctx => {
                    ctx.Monitor(_signals.When("GetRouteConfigurations"));
                    return _repository.Fetch(c => true).Select(c => new RouteConfiguration { RouteKey = c.RouteKey, Duration = c.Duration });
                });
        }

        public string GenerateAntiForgeryToken(ViewContext viewContext) {
            
            var workContext = _workContextAccessor.GetContext();

            var htmlHelper = new HtmlHelper(viewContext, new ViewDataContainer());
            var siteSalt = workContext.CurrentSite.SiteSalt;
            return htmlHelper.AntiForgeryToken(siteSalt).ToString();
       }

        public string GenerateContentItemSubsitution(int id, ViewContext viewContext) {
            var contentItem = _contentManager.Value.Get(id, VersionOptions.Published);

            if (contentItem == null)
                return String.Empty;

            dynamic model = _contentManager.Value.BuildDisplay(contentItem);
            var display = _displayHelperFactory.Value.CreateHelper(viewContext, new ViewDataContainer());
            IHtmlString result = display(model);

            return result.ToHtmlString();
        }

        public void SaveCacheConfigurations(IEnumerable<RouteConfiguration> routeConfigurations) {
            // remove all current configurations
            var configurations = _repository.Fetch(c => true);

            foreach (var configuration in configurations) {
                _repository.Delete(configuration);
            }

            // save the new configurations
            foreach (var configuration in routeConfigurations) {
                if (!configuration.Duration.HasValue) {
                    continue;
                }

                _repository.Create(new CacheParameterRecord {
                    Duration = configuration.Duration.Value,
                    RouteKey = configuration.RouteKey
                });
            }

            // invalidate the cache
            _signals.Trigger("GetRouteConfigurations");
        }
    }

    public class ViewDataContainer : IViewDataContainer {
        public ViewDataDictionary ViewData { get; set; }
    }

}