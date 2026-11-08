using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LogisticsStoresTripsAndReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CR-0008 D10, approved by the owner ("D10. Okay.", G-Destructive, OD-14): the logistics model has no city-wide stock to carry
            // over, so every player and everything of theirs goes, and each dev name logs in to a new starting city. The ledger and the
            // receipts restrict deletes, so they go first; a city's own rows go with it (cascade). Down can't bring them back.
            migrationBuilder.Sql("DELETE FROM ledger_entries;");
            migrationBuilder.Sql("DELETE FROM command_receipts;");
            migrationBuilder.Sql("DELETE FROM cities;");
            migrationBuilder.Sql("DELETE FROM players;");

            migrationBuilder.DropTable(
                name: "city_inventory");

            migrationBuilder.AddColumn<int>(
                name: "next_trip_id",
                table: "cities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "purse",
                table: "cities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "city_project_needs",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_project_needs", x => new { x.city_id, x.building_id, x.item_id });
                    table.CheckConstraint("ck_city_project_needs_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_city_project_needs_city_buildings_building_id",
                        columns: x => new { x.city_id, x.building_id },
                        principalTable: "city_buildings",
                        principalColumns: new[] { "city_id", "building_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_research_needs",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_research_needs", x => new { x.city_id, x.item_id });
                    table.CheckConstraint("ck_city_research_needs_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_city_research_needs_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_stores",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_stores", x => new { x.city_id, x.building_id, x.item_id });
                    table.CheckConstraint("ck_city_stores_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_city_stores_city_buildings_building_id",
                        columns: x => new { x.city_id, x.building_id },
                        principalTable: "city_buildings",
                        principalColumns: new[] { "city_id", "building_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_trips",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    departed_at = table.Column<long>(type: "bigint", nullable: false),
                    arrives_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_trips", x => new { x.city_id, x.trip_id });
                    table.CheckConstraint("ck_city_trips_arrives_at", "arrives_at > departed_at");
                    table.CheckConstraint("ck_city_trips_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_city_trips_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_next_trip_id",
                table: "cities",
                sql: "next_trip_id >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_purse",
                table: "cities",
                sql: "purse >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "city_project_needs");

            migrationBuilder.DropTable(
                name: "city_research_needs");

            migrationBuilder.DropTable(
                name: "city_stores");

            migrationBuilder.DropTable(
                name: "city_trips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_next_trip_id",
                table: "cities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_purse",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "next_trip_id",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "purse",
                table: "cities");

            migrationBuilder.CreateTable(
                name: "city_inventory",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_inventory", x => new { x.city_id, x.item_id });
                    table.CheckConstraint("ck_city_inventory_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "fk_city_inventory_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
