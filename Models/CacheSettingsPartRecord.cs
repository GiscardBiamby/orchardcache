using Orchard.ContentManagement.Records;

namespace Contrib.Cache.Models {
    public class CacheSettingsPartRecord : ContentPartRecord {
        public virtual int DefaultCacheDuration { get; set; }
    }
}