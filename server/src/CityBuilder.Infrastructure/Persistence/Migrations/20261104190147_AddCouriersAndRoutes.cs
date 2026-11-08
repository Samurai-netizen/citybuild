using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCouriersAndRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "city_couriers",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<short>(type: "smallint", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    to_stop = table.Column<short>(type: "smallint", nullable: false),
                    departed_at = table.Column<long>(type: "bigint", nullable: true),
                    arrives_at = table.Column<long>(type: "bigint", nullable: true),
                    stamina_seconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_couriers", x => new { x.city_id, x.station_id, x.slot });
                    table.CheckConstraint("ck_city_couriers_slot", "slot >= 1");
                    table.CheckConstraint("ck_city_couriers_stamina_seconds", "stamina_seconds >= 0");
                    table.CheckConstraint("ck_city_couriers_to_stop", "to_stop >= 0");
                    table.CheckConstraint("ck_city_couriers_walk", "(departed_at is null and arrives_at is null) or (departed_at is not null and arrives_at is not null and departed_at >= 0 and arrives_at > departed_at)");
                    table.ForeignKey(
                        name: "fk_city_couriers_city_buildings_station_id",
                        columns: x => new { x.city_id, x.station_id },
                        principalTable: "city_buildings",
                        principalColumns: new[] { "city_id", "building_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_route_actions",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<short>(type: "smallint", nullable: false),
                    stop_index = table.Column<short>(type: "smallint", nullable: false),
                    action_index = table.Column<short>(type: "smallint", nullable: false),
                    building_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_route_actions", x => new { x.city_id, x.station_id, x.slot, x.stop_index, x.action_index });
                    table.CheckConstraint("ck_city_route_actions_action_index", "action_index >= 0");
                    table.CheckConstraint("ck_city_route_actions_kind", "kind in ('take', 'put')");
                    table.CheckConstraint("ck_city_route_actions_slot", "slot >= 1");
                    table.CheckConstraint("ck_city_route_actions_stop_index", "stop_index >= 0");
                    table.ForeignKey(
                        name: "fk_city_route_actions_city_buildings_station_id",
                        columns: x => new { x.city_id, x.station_id },
                        principalTable: "city_buildings",
                        principalColumns: new[] { "city_id", "building_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_courier_loads",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<short>(type: "smallint", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    from_building_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_courier_loads", x => new { x.city_id, x.station_id, x.slot, x.item_id, x.from_building_id });
                    table.CheckConstraint("ck_city_courier_loads_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_city_courier_loads_city_couriers_slot",
                        columns: x => new { x.city_id, x.station_id, x.slot },
                        principalTable: "city_couriers",
                        principalColumns: new[] { "city_id", "station_id", "slot" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "city_courier_loads");

            migrationBuilder.DropTable(
                name: "city_route_actions");

            migrationBuilder.DropTable(
                name: "city_couriers");
        }
    }
}
