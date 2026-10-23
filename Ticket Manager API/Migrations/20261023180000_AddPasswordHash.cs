using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket_manadger.Migrations
{
    public partial class AddPasswordHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "craatedAt",
                table: "requests",
                newName: "CraatedAt");

            migrationBuilder.RenameColumn(
                name: "fullName ",
                table: "users",
                newName: "fullName");

            migrationBuilder.AddColumn<string>(
                name: "passwordHash",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "passwordHash", table: "users");
            migrationBuilder.RenameColumn(
                name: "CraatedAt", table: "requests", newName: "craatedAt");
            migrationBuilder.RenameColumn(
                name: "fullName", table: "users", newName: "fullName ");
        }
    }
}
