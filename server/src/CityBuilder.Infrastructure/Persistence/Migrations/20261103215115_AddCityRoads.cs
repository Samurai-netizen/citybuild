using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCityRoads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "city_roads",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<short>(type: "smallint", nullable: false),
                    y = table.Column<short>(type: "smallint", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_roads", x => new { x.city_id, x.x, x.y });
                    table.CheckConstraint("ck_city_roads_level", "level >= 1");
                    table.CheckConstraint("ck_city_roads_x", "x >= 0");
                    table.CheckConstraint("ck_city_roads_y", "y >= 0");
                    table.ForeignKey(
                        name: "fk_city_roads_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "city_roads");
        }
    }
}
