using Orchard.Data.Migration;

namespace Contrib.Cache {
    public class Migrations : DataMigrationImpl {
        public int Create() {

            SchemaBuilder.CreateTable("CacheSettingsPartRecord",
                table => table
                    .ContentPartRecord()
                    .Column<int>("DefaultCacheDuration")
                    .Column<string>("IgnoredUrls", c => c.Unlimited())
                    .Column<bool>("DebugMode", c => c.WithDefault(false))
                    .Column<bool>("ApplyCulture", c => c.WithDefault(false))
                );

            SchemaBuilder.CreateTable("CacheParameterRecord",
                    table => table
                        .Column<int>("Id", c => c.PrimaryKey().Identity())
                        .Column<int>("Duration")
                        .Column<string>("RouteKey", c => c.WithLength(255))
                    );

            return 3;
        }

        public int UpdateFrom1() {
            SchemaBuilder.CreateTable("CacheParameterRecord",
                table => table
                    .Column<int>("Id", c => c.PrimaryKey().Identity())
                    .Column<int>("Duration")
                    .Column<string>("RouteKey", c => c.WithLength(255))
                );

            SchemaBuilder.AlterTable("CacheSettingsPartRecord",
                table => table
                    .AddColumn<string>("IgnoredUrls", c => c.Unlimited())
                );

            SchemaBuilder.AlterTable("CacheSettingsPartRecord",
                table => table
                    .AddColumn<bool>("DebugMode", c => c.WithDefault(false))
                );

            return 2;
        }

        public int UpdateFrom2() {

            SchemaBuilder.AlterTable("CacheSettingsPartRecord",
                table => table
                    .AddColumn<bool>("ApplyCulture", c => c.WithDefault(false))
                );

            return 3;
        }
    }
}