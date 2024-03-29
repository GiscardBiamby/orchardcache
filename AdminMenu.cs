﻿using Orchard.Localization;
using Orchard.Security;
using Orchard.UI.Navigation;

namespace Contrib.Cache {
    public class AdminMenu : INavigationProvider {
        public Localizer T { get; set; }
        public string MenuName { get { return "admin"; } }

        public void GetNavigation(NavigationBuilder builder) {
            builder
                .Add(T("Settings"), menu => menu
                    .Add(T("Cache"), "10.0", subMenu => subMenu.Action("Index", "Admin", new { area = "Contrib.Cache" }).Permission(StandardPermissions.SiteOwner)
                        .Add(T("Settings"), "10.0", item => item.Action("Index", "Admin", new { area = "Contrib.Cache" }).Permission(StandardPermissions.SiteOwner).LocalNav())
                        .Add(T("Statistics"), "10.0", item => item.Action("Index", "Statistics", new { area = "Contrib.Cache" }).Permission(StandardPermissions.SiteOwner).LocalNav())
                    ));
        }
    }
}
