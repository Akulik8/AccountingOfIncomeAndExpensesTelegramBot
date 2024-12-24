using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmountMessageId",
                table: "transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescriptionMessageId",
                table: "transactions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountMessageId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "DescriptionMessageId",
                table: "transactions");
        }
    }
}
