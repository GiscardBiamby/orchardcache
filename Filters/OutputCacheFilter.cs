using System;
using System.IO;
using System.Text;
using System.Web.Mvc;
using System.Web.Routing;
using Contrib.Cache.Models;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Mvc.Filters;
using Orchard.Services;
using Orchard.Themes;
using Orchard.UI.Admin;

namespace Contrib.Cache.Filters {
    public class OutputCacheFilter : FilterProvider, IActionFilter, IResultFilter {

        private readonly ICacheManager _cacheManager;
        private readonly IWorkContextAccessor _workContextAccessor;
        private readonly IThemeManager _themeManager;
        private readonly IClock _clock;
        private readonly ISignals _signals;

        private const string AntiforgeryBeacon = "[[OutputCacheFilterAntiForgeryToken]]";
        private const string AntiforgeryTag = "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";

        public OutputCacheFilter(
            ICacheManager cacheManager,
            IWorkContextAccessor workContextAccessor,
            IThemeManager themeManager,
            IClock clock,
            ISignals signals) {
            _cacheManager = cacheManager;
            _workContextAccessor = workContextAccessor;
            _themeManager = themeManager;
            _clock = clock;
            _signals = signals;
            }

        private int _cacheDuration;
        private string _cacheKey;
        
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

            _workContext = _workContextAccessor.GetContext();

            // don't return any cached content, or cache any content, if the user is authenticated
            if (_workContext.CurrentUser != null) {
                return;
            }

            // caches the default cache duration to prevent a query to the settings
            _cacheDuration = _cacheManager.Get("CacheSettingsPart",
                context => {
                        context.Monitor(_signals.When("CacheSettingsPart"));
                        return _workContext.CurrentSite.As<CacheSettingsPart>()
                            .DefaultCacheDuration;
                }
            );

            // retrieve the cached content
            _cacheKey = ComputeCacheKey(filterContext);

            // fetch cached data
            var cacheItem = filterContext.HttpContext.Cache[_cacheKey] as CacheItem;

            // render cached content
            if (cacheItem != null) {

                // replace any anti forgery token with a fresh value
                var output = cacheItem.Output;
                if (output.Contains(AntiforgeryBeacon)) {
                    var viewContext = new ViewContext {
                        HttpContext = filterContext.HttpContext, 
                        Controller = filterContext.Controller
                    };

                    var htmlHelper = new HtmlHelper(viewContext, new ViewDataContainer());
                    var siteSalt = _workContext.CurrentSite.SiteSalt;
                    var token = htmlHelper.AntiForgeryToken(siteSalt);
                    output = output.Replace(AntiforgeryBeacon, token.ToString());
                }
                
                filterContext.Result = new ContentResult {
                    Content = output,
                    ContentType = cacheItem.ContentType
                };

                return;
            }

            // no cache content available, intercept the execution results for caching
            var response = filterContext.HttpContext.Response;
            response.Filter = _filter = new CapturingResponseFilter(response.Filter);
        }
        
        public void OnResultExecuted(ResultExecutedContext filterContext) {
            // save the result only if the content can be intercepted
            if (_filter == null) return;

            // only for ViewResult right now, as we don't want to handle redirects, HttpNotFound, ...
            var accepted = (filterContext.Result as ViewResult) != null;
            accepted |= (filterContext.Result as PartialViewResult) != null;

            if (!accepted) return;

            // get contents 
            var response = filterContext.HttpContext.Response;
            response.Flush();
            var output = _filter.GetContents(response.ContentEncoding);

            if (String.IsNullOrWhiteSpace(output)) {
                return;
            }

            var tokenIndex = output.IndexOf(AntiforgeryTag);

            // substitute antiforgery token by a beacon
            if(tokenIndex != -1) {
                var tokenEnd = output.IndexOf(">", tokenIndex);
                var sb = new StringBuilder();
                sb.Append(output.Substring(0, tokenIndex));
                sb.Append(AntiforgeryBeacon);
                sb.Append(output.Substring(tokenEnd + 1));

                output = sb.ToString();
            }

            var now = _clock.UtcNow;

            var cacheItem = new CacheItem {
                ContentType = response.ContentType,
                CachedOnUtc = now,
                ValidUntilUtc = now.AddSeconds(_cacheDuration),
                Url = filterContext.HttpContext.Request.Url.AbsolutePath,
                QueryString = filterContext.HttpContext.Request.Url.Query,
                Output = output
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

        public void OnActionExecuted(ActionExecutedContext filterContext) {
        }

        public void OnResultExecuting(ResultExecutingContext filterContext) {
        }

        private string ComputeCacheKey(ActionExecutingContext filterContext) {
            
            var keyBuilder = new StringBuilder();

            // todo: add tenant if not using the url
            keyBuilder.Append("url=").Append(filterContext.HttpContext.Request.Url.PathAndQuery).Append(";");

            foreach (var pair in filterContext.ActionParameters) {
                keyBuilder.AppendFormat("{0}={1};", pair.Key, pair.Value);
            }

            // include the theme in the cache key
            keyBuilder.Append("culture=").Append(_workContext.CurrentCulture).Append(";");

            // include the theme in the cache key
            keyBuilder.Append("theme=").Append(_themeManager.GetRequestTheme(filterContext.RequestContext).Id).Append(";");

            return keyBuilder.ToString();
        }

        [Serializable]
        public class CacheItem {
            public DateTime ValidUntilUtc { get; set; }
            public DateTime CachedOnUtc { get; set; }
            public string Output { get; set; }
            public string ContentType { get; set; }
            public string QueryString { get; set; }
            public string Url { get; set; }
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

    public class ViewDataContainer : IViewDataContainer {
        public ViewDataDictionary ViewData { get; set; }
    }

}