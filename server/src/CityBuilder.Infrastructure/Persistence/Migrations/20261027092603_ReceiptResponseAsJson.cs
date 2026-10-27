using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityBuilder.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptResponseAsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "response_json",
                table: "command_receipts",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "response_json",
                table: "command_receipts",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json");
        }
    }
}
