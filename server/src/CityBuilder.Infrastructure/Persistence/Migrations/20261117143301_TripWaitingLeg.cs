using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lets a stored trip carry goods back and wait at its source (M0-C-3, <c>06</c>): <c>ck_city_trips_quantity</c> also allows a
    /// <c>ToSource</c> leg with a load and nothing remaining, and the <c>Waiting</c> leg with a load and nothing remaining. Stored trips
    /// already fit the new rule.
    /// </summary>
    public partial class TripWaitingLeg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and ((quantity = 0 and remaining > 0) or (quantity > 0 and remaining = 0))) or (leg = 'Waiting' and quantity > 0 and remaining = 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_trips_quantity",
                table: "city_trips",
                sql: "(leg = 'ToTarget' and quantity > 0) or (leg = 'ToSource' and quantity = 0 and remaining > 0)");
        }
    }
}
