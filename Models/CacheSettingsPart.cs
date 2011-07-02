using Orchard.ContentManagement;

namespace Contrib.Cache.Models {
    public class CacheSettingsPart : ContentPart<CacheSettingsPartRecord> {
        public int DefaultCacheDuration {
            get { return Record.DefaultCacheDuration; }
            set { Record.DefaultCacheDuration = value; }
        }
    }
}