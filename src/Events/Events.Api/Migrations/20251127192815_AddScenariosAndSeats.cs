using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddScenariosAndSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Scenarios_EventId",
                table: "Scenarios",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Scenarios_Events_EventId",
                table: "Scenarios",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Scenarios_Events_EventId",
                table: "Scenarios");

            migrationBuilder.DropIndex(
                name: "IX_Scenarios_EventId",
                table: "Scenarios");
        }
    }
}
