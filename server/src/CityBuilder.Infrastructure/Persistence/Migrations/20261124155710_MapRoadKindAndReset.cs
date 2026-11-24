using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapRoadKindAndReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // M1-03 reset (owner's choice, G-Destructive): every city starts on the map, so every player and everything of theirs goes, in the
            // order the foreign keys need; player_events and the city rows go with their player and city (cascade). Down can't bring them back.
            migrationBuilder.Sql("DELETE FROM ledger_entries;");
            migrationBuilder.Sql("DELETE FROM command_receipts;");
            migrationBuilder.Sql("DELETE FROM cities;");
            migrationBuilder.Sql("DELETE FROM players;");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "city_roads",
                type: "text",
                nullable: false,
                defaultValue: "road");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_roads_kind",
                table: "city_roads",
                sql: "kind in ('road', 'bridge') and (kind = 'road' or level = 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_roads_kind",
                table: "city_roads");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "city_roads");
        }
    }
}
