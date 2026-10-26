using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dev_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    era = table.Column<short>(type: "smallint", nullable: false),
                    clock_seconds = table.Column<long>(type: "bigint", nullable: false),
                    last_settled_unix = table.Column<long>(type: "bigint", nullable: false),
                    next_building_id = table.Column<int>(type: "integer", nullable: false),
                    research_id = table.Column<string>(type: "text", nullable: true),
                    research_started_at = table.Column<long>(type: "bigint", nullable: true),
                    research_ends_at = table.Column<long>(type: "bigint", nullable: true),
                    state_version = table.Column<long>(type: "bigint", nullable: false),
                    content_version = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                    table.CheckConstraint("ck_cities_era", "era >= 1");
                    table.ForeignKey(
                        name: "fk_cities_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "command_receipts",
                columns: table => new
                {
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_type = table.Column<string>(type: "text", nullable: false),
                    payload_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_command_receipts", x => new { x.player_id, x.request_id });
                    table.ForeignKey(
                        name: "fk_command_receipts_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "city_buildings",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<int>(type: "integer", nullable: false),
                    type_id = table.Column<string>(type: "text", nullable: false),
                    x = table.Column<short>(type: "smallint", nullable: false),
                    y = table.Column<short>(type: "smallint", nullable: false),
                    level = table.Column<short>(type: "smallint", nullable: false),
                    assigned_workers = table.Column<short>(type: "smallint", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    phase_started_at = table.Column<long>(type: "bigint", nullable: true),
                    phase_ends_at = table.Column<long>(type: "bigint", nullable: true),
                    target_level = table.Column<short>(type: "smallint", nullable: true),
                    cycle_labor = table.Column<long>(type: "bigint", nullable: true),
                    labor_remaining = table.Column<long>(type: "bigint", nullable: true),
                    segment_started_at = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_buildings", x => new { x.city_id, x.building_id });
                    table.CheckConstraint("ck_city_buildings_assigned_workers", "assigned_workers >= 0");
                    table.CheckConstraint("ck_city_buildings_labor_remaining", "labor_remaining >= 0");
                    table.CheckConstraint("ck_city_buildings_level", "level >= 1");
                    table.CheckConstraint("ck_city_buildings_x", "x >= 0");
                    table.CheckConstraint("ck_city_buildings_y", "y >= 0");
                    table.ForeignKey(
                        name: "fk_city_buildings_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "city_research",
                columns: table => new
                {
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    research_id = table.Column<string>(type: "text", nullable: false),
                    completed_at = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_city_research", x => new { x.city_id, x.research_id });
                    table.ForeignKey(
                        name: "fk_city_research_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    delta = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    ref_type = table.Column<string>(type: "text", nullable: true),
                    ref_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_entries_cities_city_id",
                        column: x => x.city_id,
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_cities_player_id",
                table: "cities",
                column: "player_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_city_buildings_city_id_x_y",
                table: "city_buildings",
                columns: new[] { "city_id", "x", "y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_city_id_created_at",
                table: "ledger_entries",
                columns: new[] { "city_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_players_dev_name",
                table: "players",
                column: "dev_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "city_buildings");

            migrationBuilder.DropTable(
                name: "city_inventory");

            migrationBuilder.DropTable(
                name: "city_research");

            migrationBuilder.DropTable(
                name: "command_receipts");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "cities");

            migrationBuilder.DropTable(
                name: "players");
        }
    }
}
