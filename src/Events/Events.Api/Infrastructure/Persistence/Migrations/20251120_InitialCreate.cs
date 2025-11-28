using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Api.Infrastructure.Persistence.Migrations
{
 public partial class InitialCreate : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.CreateTable(
 name: "Events",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 Name = table.Column<string>(type: "text", nullable: false),
 Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_Events", x => x.Id);
 });
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropTable(
 name: "Events");
 }
 }
}
