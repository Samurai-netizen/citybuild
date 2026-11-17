using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds <c>player_events</c> (CR-0009-T1, <c>06</c>): the player metrics' append-only events, with their two indexes, the city clock
    /// check and the foreign key to <c>players</c> that deletes a player's events with the player. Only adds; no stored row changes, and
    /// nothing is backfilled.
    /// </summary>
    public partial class AddPlayerEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    value = table.Column<long>(type: "bigint", nullable: true),
                    city_clock = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_events", x => x.id);
                    table.CheckConstraint("ck_player_events_city_clock", "city_clock >= 0");
                    table.ForeignKey(
                        name: "fk_player_events_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_events_event_type_detail",
                table: "player_events",
                columns: new[] { "event_type", "detail" });

            migrationBuilder.CreateIndex(
                name: "ix_player_events_player_id_created_at",
                table: "player_events",
                columns: new[] { "player_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_events");
        }
    }
}
