using System.Web.Mvc;
using Contrib.Cache.Services;
using Orchard.Themes;

namespace Contrib.Cache.Controllers {
    [Themed(false)]
    public class ItemController : Controller {
        private readonly ICacheService _cacheService;

        public ItemController(ICacheService cacheService) {
            _cacheService = cacheService;
        }

        // /Cache/Item/72
        public ActionResult Index(int id) {
            var viewContext = new ViewContext {
                HttpContext = HttpContext,
                Controller = this
            };

            return Content(_cacheService.GenerateContentItemSubsitution(id, viewContext));
        }

        // /Cache/Item/AntiForgeryToken
        public ActionResult AntiForgeryToken() {
            var viewContext = new ViewContext {
                HttpContext = HttpContext,
                Controller = this
            };

            var token = _cacheService.GenerateAntiForgeryToken(viewContext);
            return Content(token);
        }
    }

}