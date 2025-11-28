using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reservations.Api.Infrastructure.Persistence.Migrations
{
 public partial class InitialCreate : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.CreateTable(
 name: "Reservations",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 EventId = table.Column<Guid>(type: "uuid", nullable: false),
 Status = table.Column<string>(type: "text", nullable: false),
 CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_Reservations", x => x.Id);
 });
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropTable(
 name: "Reservations");
 }
 }
}
