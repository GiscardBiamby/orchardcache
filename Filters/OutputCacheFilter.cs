using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using System.Web.Routing;
using Contrib.Cache.Models;
using Contrib.Cache.Services;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Environment.Configuration;
using Orchard.Mvc.Extensions;
using Orchard.Mvc.Filters;
using Orchard.Services;
using Orchard.Themes;
using Orchard.UI.Admin;
using Orchard.Utility.Extensions;

namespace Contrib.Cache.Filters {
    public class OutputCacheFilter : FilterProvider, IActionFilter, IResultFilter {

        private readonly ICacheManager _cacheManager;
        private readonly IWorkContextAccessor _workContextAccessor;
        private readonly IThemeManager _themeManager;
        private readonly IClock _clock;
        private readonly ICacheService _cacheService;
        private readonly ISignals _signals;
        private readonly ShellSettings _shellSettings;

        private const string AntiforgeryBeacon = "<esi:include src=\"{0}\" />";
        private const string AntiforgeryTag = "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        private const string Esi = "<esi:include";

        public OutputCacheFilter(
            ICacheManager cacheManager,
            IWorkContextAccessor workContextAccessor,
            IThemeManager themeManager,
            IClock clock,
            ICacheService cacheService,
            ISignals signals,
            ShellSettings shellSettings) {
            _cacheManager = cacheManager;
            _workContextAccessor = workContextAccessor;
            _themeManager = themeManager;
            _clock = clock;
            _cacheService = cacheService;
            _signals = signals;
            _shellSettings = shellSettings;
        }

        private bool _debugMode;
        private int _cacheDuration;
        private string _ignoredUrls;
        private string _cacheKey;
        private CacheItem _cacheItem;

        private WorkContext _workContext;
        private CapturingResponseFilter _filter;

        public void OnActionExecuting(ActionExecutingContext filterContext) {
            // don't cache the admin
            if (AdminFilter.IsApplied(new RequestContext(filterContext.HttpContext, new RouteData())))
                return;

            // ignore child actions, e.g. HomeController is using RenderAction()
            if (filterContext.IsChildAction) {
                return;
            }

            // only return cached result on GET requests
            if(filterContext.HttpContext.Request.HttpMethod != "GET") {
                return;
            }

            _workContext = _workContextAccessor.GetContext();

            // don't return any cached content, or cache any content, if the user is authenticated
            if (_workContext.CurrentUser != null) {
                return;
            }

            // caches the default cache duration to prevent a query to the settings
            _cacheDuration = _cacheManager.Get("CacheSettingsPart.Duration",
                context => {
                    context.Monitor(_signals.When(CacheSettingsPart.CacheKey));
                    return _workContext.CurrentSite.As<CacheSettingsPart>().DefaultCacheDuration;
                }
            );

            // caches the ignored urls to prevent a query to the settings
            _ignoredUrls = _cacheManager.Get("CacheSettingsPart.IgnoredUrls",
                context => {
                    context.Monitor(_signals.When(CacheSettingsPart.CacheKey));
                    return _workContext.CurrentSite.As<CacheSettingsPart>().IgnoredUrls;
                }
            );

            // caches the ignored urls to prevent a query to the settings
            _debugMode = _cacheManager.Get("CacheSettingsPart.DebugMode",
                context => {
                    context.Monitor(_signals.When(CacheSettingsPart.CacheKey));
                    return _workContext.CurrentSite.As<CacheSettingsPart>().DebugMode;
                }
            );

            // retrieve the cached content
            _cacheKey = ComputeCacheKey(filterContext);

            // fetch cached data
            _cacheItem = filterContext.HttpContext.Cache[_cacheKey] as CacheItem;

            // render cached content
            if (_cacheItem != null) {

                var output = _cacheItem.Output;

                output = ProcessEsi(output, filterContext);

                // adds some caching information to the output if requested
                if (_debugMode) {
                    output += "<!-- Cached on " + _cacheItem.CachedOnUtc + " (UTC) until" + _cacheItem.ValidUntilUtc + "  (UTC) -->";
                }

                filterContext.Result = new ContentResult {
                    Content = output,
                    ContentType = _cacheItem.ContentType
                };

                return;
            }

            // wether the content needs to be cached or not, intercept the content at least to process ESI
            _filter = new CapturingResponseFilter(filterContext.HttpContext.Response.Filter);
            filterContext.HttpContext.Response.Filter = _filter;
        }

        public void OnActionExecuted(ActionExecutedContext filterContext) {
        }


        public void OnResultExecuting(ResultExecutingContext filterContext) {
        }

        public void OnResultExecuted(ResultExecutedContext filterContext) {
            // save the result only if the content can be intercepted
            if (_filter == null) return;

            // only for ViewResult right now, as we don't want to handle redirects, HttpNotFound, ...
            var accepted = (filterContext.Result as ViewResult) != null;
            accepted |= (filterContext.Result as PartialViewResult) != null;

            if (!accepted) return;

            // check if there is a specific rule not to cache the whole route
            var configurations = _cacheService.GetRouteConfigurations();
            var route = (Route) filterContext.Controller.ControllerContext.RouteData.Route;
            var key = _cacheService.GetRouteDescriptorKey(route.Url, route.DataTokens);
            var configuration = configurations.Where(c => c.RouteKey == key).FirstOrDefault();

            // do not cache ?
            if (configuration != null && configuration.Duration == 0) {
                return;
            }

            // ignored url ?
            var basePath = filterContext.HttpContext.Request.ToApplicationRootUrlString();
            if (IsIgnoredUrl(filterContext.RequestContext.HttpContext.Request.Url.AbsoluteUri, _ignoredUrls, basePath)) {
                return;
            }

            // get contents 
            var response = filterContext.HttpContext.Response;
            response.Flush();
            var output = _filter.GetContents(response.ContentEncoding);

            if (String.IsNullOrWhiteSpace(output)) {
                return;
            }

            var tokenIndex = output.IndexOf(AntiforgeryTag);

            // substitute antiforgery token by an Edge Side Include (http://en.wikipedia.org/wiki/Edge_Side_Includes)
            if (tokenIndex != -1) {
                var urlHelper = new UrlHelper(filterContext.RequestContext);

                var tokenEnd = output.IndexOf(">", tokenIndex);
                var sb = new StringBuilder();
                sb.Append(output.Substring(0, tokenIndex));
                sb.Append(String.Format(AntiforgeryBeacon, urlHelper.AbsoluteAction("AntiForgeryToken", "Item", new {area = "Contrib.Cache"})));
                sb.Append(output.Substring(tokenEnd + 1));

                output = sb.ToString();
            }

            var now = _clock.UtcNow;

            // default duration of specific one ?
            var cacheDuration = configuration != null && configuration.Duration.HasValue ? configuration.Duration.Value : _cacheDuration;

            var cacheItem = new CacheItem {
                ContentType = response.ContentType,
                CachedOnUtc = now,
                ValidUntilUtc = now.AddSeconds(cacheDuration),
                Url = filterContext.HttpContext.Request.Url.AbsolutePath,
                QueryString = filterContext.HttpContext.Request.Url.Query,
                Output = output,
                CacheKey = _cacheKey
            };

            // add data to cache
            filterContext.HttpContext.Cache.Add(
                _cacheKey,
                cacheItem,
                null,
                cacheItem.ValidUntilUtc,
                System.Web.Caching.Cache.NoSlidingExpiration,
                System.Web.Caching.CacheItemPriority.Normal,
                null);
        }


        private string ComputeCacheKey(ActionExecutingContext filterContext) {

            var keyBuilder = new StringBuilder();

            keyBuilder.Append("tenant=").Append(_shellSettings.Name).Append(";");

            keyBuilder.Append("url=").Append(filterContext.HttpContext.Request.RawUrl.ToLowerInvariant()).Append(";");

            foreach (var pair in filterContext.ActionParameters) {
                keyBuilder.AppendFormat("{0}={1};", pair.Key, pair.Value);
            }

            // include the theme in the cache key
            keyBuilder.Append("culture=").Append(_workContext.CurrentCulture).Append(";");

            // include the theme in the cache key
            keyBuilder.Append("theme=").Append(_themeManager.GetRequestTheme(filterContext.RequestContext).Id).Append(";");

            return keyBuilder.ToString();
        }

        /// <summary>
        /// Returns true if the given url should be ignored, as defined in the settings
        /// </summary>
        private static bool IsIgnoredUrl(string url, string ignoredUrls, string basePath) {
            if (String.IsNullOrEmpty(ignoredUrls)) {
                return false;
            }

            using (var urlReader = new StringReader(ignoredUrls)) {
                string relativePath;
                while (null != (relativePath = urlReader.ReadLine())) {
                    if (String.IsNullOrWhiteSpace(relativePath)) {
                        continue;
                    }

                    relativePath = relativePath.Trim();

                    // ignore comments)
                    if (relativePath.StartsWith("#")) {
                        continue;
                    }

                    if (String.Equals(basePath + relativePath, url, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Replaces any Esi by it's substituted value
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        private string ProcessEsi(string content, ActionExecutingContext filterContext) {
            if (content.Contains(Esi)) {
                var sb = new StringBuilder(content.Length);

                var index = 0;
                int nextEsi;
                while (-1 != (nextEsi = content.IndexOf(Esi, index))) {

                    int srcStart = content.IndexOf("src=\"", nextEsi) + 5;
                    int srcEnd = content.IndexOf("\"", srcStart);

                    var resource = content.Substring(srcStart, srcEnd - srcStart);

                    // append previous section
                    sb.Append(content.Substring(index, nextEsi));

                    index = content.IndexOf("/>", srcEnd) + 2;

                    var viewContext = new ViewContext {
                        HttpContext = filterContext.HttpContext,
                        Controller = filterContext.Controller
                    };

                    // esi includes are either /Cache/Item/AntiForgeryToken or /Cache/Item/{id}
                    string substitution;
                    if(resource.EndsWith("/Cache/Item/AntiForgeryToken")) {
                        substitution = _cacheService.GenerateAntiForgeryToken(viewContext);
                    }
                    else {
                        string id = resource.Substring(resource.LastIndexOf('/'));
                        substitution = _cacheService.GenerateContentItemSubsitution(Int32.Parse(id), viewContext);
                    }

                    sb.Append(substitution);
                }

                sb.Append(content.Substring(index));
                return sb.ToString();
            }

            return content;
        }
    }

    /// <summary>
    /// Captures the response stream while writing to it
    /// </summary>
    public class CapturingResponseFilter : Stream {
        private readonly Stream _sink;
        private readonly MemoryStream _mem;

        public CapturingResponseFilter(Stream sink) {
            _sink = sink;
            _mem = new MemoryStream();
        }

        // The following members of Stream must be overriden.
        public override bool CanRead {
            get { return true; }
        }

        public override bool CanSeek {
            get { return false; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override long Length {
            get { return 0; }
        }

        public override long Position { get; set; }

        public override long Seek(long offset, SeekOrigin direction) {
            return 0;
        }

        public override void SetLength(long length) {
            _sink.SetLength(length);
        }

        public override void Close() {
            _sink.Close();
            _mem.Close();
        }

        public override void Flush() {
            _sink.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return _sink.Read(buffer, offset, count);
        }

        // Override the Write method to filter Response to a file. 
        public override void Write(byte[] buffer, int offset, int count) {

            //Here we will not write to the sink b/c we want to capture
            _sink.Write(buffer, offset, count);

            //Write out the response to the file.
            _mem.Write(buffer, 0, count);
        }

        public string GetContents(Encoding enc) {
            var buffer = new byte[_mem.Length];
            _mem.Position = 0;
            _mem.Read(buffer, 0, buffer.Length);
            return enc.GetString(buffer, 0, buffer.Length);
        }

    }
}