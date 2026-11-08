using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds <c>cities.last_seen_unix</c>, the player's last request that settled the city (CR-0008-T1 part H, <c>06</c>), set to each
    /// stored city's last settled second.
    /// </summary>
    public partial class AddLastSeen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "last_seen_unix",
                table: "cities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // A stored city was last seen when it was last settled: every settle so far was the player's request (04 section 2).
            migrationBuilder.Sql("UPDATE cities SET last_seen_unix = last_settled_unix;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_seen_unix",
                table: "cities");
        }
    }
}
