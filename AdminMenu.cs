using Orchard.Localization;
using Orchard.Security;
using Orchard.UI.Navigation;

namespace Contrib.Cache {
    public class AdminMenu : INavigationProvider {
        public Localizer T { get; set; }
        public string MenuName { get { return "admin"; } }

        public void GetNavigation(NavigationBuilder builder) {
            builder
                .Add(T("Settings"), menu => menu
                    .Add(T("Cache"), "11", item => item.Action("Index", "Admin", new { area = "Contrib.Cache" }).Permission(StandardPermissions.SiteOwner))
                );
        }
    }
}
