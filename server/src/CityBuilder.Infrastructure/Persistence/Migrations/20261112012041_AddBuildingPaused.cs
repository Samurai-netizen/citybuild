using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the pause of CR-0008 amendment 2 (<c>06</c>, D23): <c>city_buildings.paused</c>, false for stored buildings, and the check that a
    /// building under construction is never paused.
    /// </summary>
    public partial class AddBuildingPaused : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "paused",
                table: "city_buildings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings",
                sql: "NOT paused OR phase <> 'Constructing'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_city_buildings_paused",
                table: "city_buildings");

            migrationBuilder.DropColumn(
                name: "paused",
                table: "city_buildings");
        }
    }
}
