using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stores walkers that wait for a path (M1-03 decision 4, <c>06</c>): <c>city_couriers.waits_at</c>, the building a blocked courier waits
    /// at (set exactly in the state <c>Blocked</c>, <c>ck_city_couriers_waits_at</c>), and the trip legs <c>BlockedAtSource</c> (nothing
    /// carried, goods to fetch) and <c>BlockedAtTarget</c> (as a walk back) in <c>ck_city_trips_quantity</c>. Stored rows already fit. Down
    /// fails while a blocked walker is stored.
    /// </summary>
    public partial class BlockedWalkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.AddColumn<int>(
                name: "waits_at",
                table: "city_couriers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and ((quantity = 0 and remaining > 0) or (quantity > 0 and remaining = 0))) or (leg = 'Waiting' and quantity > 0 and remaining = 0) or (leg = 'BlockedAtSource' and quantity = 0 and remaining > 0) or (leg = 'BlockedAtTarget' and ((quantity = 0 and remaining > 0) or (quantity > 0 and remaining = 0)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_couriers_waits_at",
                table: "city_couriers",
                sql: "(state = 'Blocked') = (waits_at is not null)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.DropCheckConstraint(
                name: "ck_city_couriers_waits_at",
                table: "city_couriers");

            migrationBuilder.DropColumn(
                name: "waits_at",
                table: "city_couriers");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and ((quantity = 0 and remaining > 0) or (quantity > 0 and remaining = 0))) or (leg = 'Waiting' and quantity > 0 and remaining = 0)");
        }
    }
}
