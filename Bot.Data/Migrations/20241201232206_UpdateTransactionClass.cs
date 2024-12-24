using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactionClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryMessageId",
                table: "transactions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "CategoryMessageId",
                table: "transactions");
        }
    }
}
