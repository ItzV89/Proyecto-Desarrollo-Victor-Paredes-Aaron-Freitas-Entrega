using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthUser.Api.Infrastructure.Persistence.Migrations
{
 public partial class InitialCreate : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.CreateTable(
 name: "Profiles",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 Username = table.Column<string>(type: "text", nullable: false),
 Email = table.Column<string>(type: "text", nullable: false),
 CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_Profiles", x => x.Id);
 });
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropTable(
 name: "Profiles");
 }
 }
}
