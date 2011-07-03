using System;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using Contrib.Cache.Filters;
using Contrib.Cache.Models;
using Orchard;
using Orchard.ContentManagement;
using Orchard.Core.Common.Models;
using Orchard.Core.Routable.Models;
using Orchard.Data;
using Orchard.ContentManagement.Handlers;

namespace Contrib.Cache.Handlers {
    public class CacheSettingsPartHandler : ContentHandler {
        private readonly IWorkContextAccessor _workContextAccessor;

        public CacheSettingsPartHandler(
            IRepository<CacheSettingsPartRecord> repository,
            IWorkContextAccessor workContextAccessor) {
            _workContextAccessor = workContextAccessor;
            Filters.Add(new ActivatingFilter<CacheSettingsPart>("Site"));
            Filters.Add(StorageFilter.For(repository));

            // initializing default cache settings values
            OnInitializing<CacheSettingsPart>((context, part) => { part.DefaultCacheDuration = 60; });

            // evict modified routable content when updated
            OnPublished<IContent>(
                (context, part) => {
                    // list of cache keys to evict
                    var evict = new List<object>();
                    var workContext = _workContextAccessor.GetContext();

                    Action<RoutePart> findAndEvict = (p) => {
                        // search for CacheItem object in the cache
                        foreach (DictionaryEntry cacheEntry in workContext.HttpContext.Cache) {
                            var cacheItem = cacheEntry.Value as CacheItem;
                            if (cacheItem == null) {
                                continue;
                            }

                            if (cacheItem.Url == VirtualPathUtility.ToAbsolute("~/" + p.Path)) {
                                evict.Add(cacheEntry.Key);
                            }
                        }
                    };

                    var routable = part.As<RoutePart>();
                    if (routable != null) {
                        findAndEvict(routable);
                    }

                    // search the cache for containers too
                    var commonPart = part.As<CommonPart>();
                    if (commonPart != null) {
                        if (commonPart.Container != null) {
                            var routableCommon = commonPart.Container.As<RoutePart>();
                            if (routableCommon != null) {
                                findAndEvict(routableCommon);
                            }
                        }
                    }

                    // remove all content to evict
                    foreach(var key in evict) {
                        workContext.HttpContext.Cache.Remove(key.ToString());
                    }

            });
        }
    }
}