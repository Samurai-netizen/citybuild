using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the builder queue of CR-0013 (<c>06</c>): <c>city_buildings.queued_order</c> (null for stored buildings),
    /// <c>cities.next_queued_order</c> (1 for stored cities, which have no queue), their checks, and the pause check widened to the
    /// <c>Queued</c> phase. Down drops the queue; a city with a queued order is not valid after it.
    /// </summary>
    public partial class AddBuilderQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings");

            migrationBuilder.AddColumn<int>(
                name: "queued_order",
                table: "city_buildings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "next_queued_order",
                table: "cities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings",
                sql: "NOT paused OR phase NOT IN ('Constructing', 'Queued')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_buildings_queued_order",
                table: "city_buildings",
                sql: "queued_order IS NULL OR queued_order >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_next_queued_order",
                table: "cities",
                sql: "next_queued_order >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_city_buildings_queued_order",
                table: "city_buildings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_next_queued_order",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "queued_order",
                table: "city_buildings");

            migrationBuilder.DropColumn(
                name: "next_queued_order",
                table: "cities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings",
                sql: "NOT paused OR phase <> 'Constructing'");
        }
    }
}
