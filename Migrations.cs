using Orchard.Data.Migration;

namespace Contrib.Cache {
    public class Migrations : DataMigrationImpl {
        public int Create() {

            SchemaBuilder.CreateTable("CacheSettingsPartRecord", 
                table => table
                    .ContentPartRecord()
                    .Column<int>("DefaultCacheDuration")
                );

            return 1;
        }
    }
}