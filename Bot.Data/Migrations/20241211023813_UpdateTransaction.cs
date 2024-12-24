using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bot.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryMessageId",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "DescriptionMessageId",
                table: "transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryMessageId",
                table: "transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescriptionMessageId",
                table: "transactions",
                type: "integer",
                nullable: true);
        }
    }
}
