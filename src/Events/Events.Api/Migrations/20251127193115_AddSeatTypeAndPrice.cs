using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatTypeAndPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Seats",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Seats",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Seats");
        }
    }
}
