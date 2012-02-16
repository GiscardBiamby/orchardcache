using System;
using System.Collections.Generic;
using System.Web;
using Contrib.Cache.Models;
using Contrib.Cache.Services;
using Orchard;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Aspects;
using Orchard.Core.Common.Models;
using Orchard.Data;
using Orchard.ContentManagement.Handlers;

namespace Contrib.Cache.Handlers {
    public class CacheSettingsPartHandler : ContentHandler {
        private readonly IWorkContextAccessor _workContextAccessor;
        private readonly ICacheService _cacheService;

        public CacheSettingsPartHandler(
            IRepository<CacheSettingsPartRecord> repository,
            IWorkContextAccessor workContextAccessor,
            ICacheService cacheService) {
            _workContextAccessor = workContextAccessor;
            _cacheService = cacheService;
            Filters.Add(new ActivatingFilter<CacheSettingsPart>("Site"));
            Filters.Add(StorageFilter.For(repository));

            // initializing default cache settings values
            OnInitializing<CacheSettingsPart>((context, part) => { part.DefaultCacheDuration = 300; });

            // evict modified routable content when updated
            OnPublished<IContent>(
                (context, part) => {
                    // list of cache keys to evict
                    var evict = new List<CacheItem>();
                    var workContext = _workContextAccessor.GetContext();

                    Action<IAliasAspect> findAndEvict = p => {
                        foreach (var cacheItem in _cacheService.GetCacheItems()) {
                            if (cacheItem.Url == VirtualPathUtility.ToAbsolute("~/" + p.Path)) {
                                evict.Add(cacheItem);
                            }
                        }
                    };

                    var alias = part.As<IAliasAspect>();
                    if (alias != null) {
                        findAndEvict(alias);
                    }

                    // search the cache for containers too
                    var commonPart = part.As<CommonPart>();
                    if (commonPart != null) {
                        if (commonPart.Container != null) {
                            var aliasCommon = commonPart.Container.As<IAliasAspect>();
                            if (aliasCommon != null) {
                                findAndEvict(aliasCommon);
                            }
                        }
                    }

                    // remove all content to evict
                    foreach (var cacheItem in evict) {
                        _cacheService.Evict(cacheItem.CacheKey, workContext.HttpContext);
                    }

                });
        }
    }
}