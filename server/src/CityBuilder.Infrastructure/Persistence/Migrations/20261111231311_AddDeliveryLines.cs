using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the delivery queue of CR-0008 amendment 1 (<c>06</c>): the <c>city_delivery_lines</c> table, <c>cities.next_line_id</c> (1 for stored
    /// cities), and the worker shuttle's <c>city_trips.remaining</c> (0) and <c>leg</c> (<c>ToTarget</c>): a stored trip is a one-load trip on its
    /// way to the target.
    /// </summary>
    public partial class AddDeliveryLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.AddColumn<string>(
                name: "leg",
                table: "city_trips",
                type: "text",
                nullable: false,
                defaultValue: "ToTarget");

            migrationBuilder.AddColumn<long>(
                name: "remaining",
                table: "city_trips",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "next_line_id",
                table: "cities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "city_delivery_lines",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_id = table.Column<int>(type: "integer", nullable: false),
                    station_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<short>(type: "smallint", nullable: false),
                    source_id = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_delivery_lines", x => new { x.city_id, x.station_id, x.slot, x.line_id });
                    table.CheckConstraint("ck_city_delivery_lines_buildings", "source_id <> target_id");
                    table.CheckConstraint("ck_city_delivery_lines_quantity", "quantity > 0");
                    table.CheckConstraint("ck_city_delivery_lines_slot", "slot >= 1");
                    table.ForeignKey(
                        name: "fk_city_delivery_lines_city_couriers",
                        columns: x => new { x.city_id, x.station_id, x.slot },
                        principalTable: "city_couriers",
                        principalColumns: new[] { "city_id", "station_id", "slot" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and quantity = 0 and remaining > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_remaining",
                table: "city_trips",
                sql: "remaining >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_next_line_id",
                table: "cities",
                sql: "next_line_id >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "city_delivery_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_remaining",
                table: "city_trips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_next_line_id",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "leg",
                table: "city_trips");

            migrationBuilder.DropColumn(
                name: "remaining",
                table: "city_trips");

            migrationBuilder.DropColumn(
                name: "next_line_id",
                table: "cities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "quantity > 0");
        }
    }
}
