using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket_manadger.Migrations
{
    public partial class AddRequestWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_requests_users_clientId",
                table: "requests");

            migrationBuilder.AddForeignKey(
                name: "FK_requests_users_clientId",
                table: "requests",
                column: "clientId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<int>(
                name: "priority", table: "requests", type: "integer",
                nullable: false, defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "executorId", table: "requests", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_requests_executorId_statusId",
                table: "requests",
                columns: new[] { "executorId", "statusId" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_statusId_CraatedAt",
                table: "requests",
                columns: new[] { "statusId", "CraatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_requests_users_executorId",
                table: "requests",
                column: "executorId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticketId = table.Column<int>(type: "integer", nullable: false),
                    userId = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.ForeignKey("FK_comments_requests_ticketId", x => x.ticketId, "requests", "id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_comments_users_userId", x => x.userId, "users", "id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requestHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticketId = table.Column<int>(type: "integer", nullable: false),
                    userId = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    details = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requestHistory", x => x.id);
                    table.ForeignKey("FK_requestHistory_requests_ticketId", x => x.ticketId, "requests", "id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_requestHistory_users_userId", x => x.userId, "users", "id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comments_ticketId_createdAt", table: "comments",
                columns: new[] { "ticketId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_userId", table: "comments", column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_requestHistory_ticketId_createdAt", table: "requestHistory",
                columns: new[] { "ticketId", "createdAt" });

            migrationBuilder.CreateIndex(
                name: "IX_requestHistory_userId", table: "requestHistory", column: "userId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "comments");
            migrationBuilder.DropTable(name: "requestHistory");
            migrationBuilder.DropForeignKey(name: "FK_requests_users_executorId", table: "requests");
            migrationBuilder.DropIndex(name: "IX_requests_ExecutorId_StatusId", table: "requests");
            migrationBuilder.DropIndex(name: "IX_requests_StatusId_CraatedAt", table: "requests");
            migrationBuilder.DropColumn(name: "executorId", table: "requests");
            migrationBuilder.DropColumn(name: "priority", table: "requests");

            migrationBuilder.DropForeignKey(
                name: "FK_requests_users_clientId",
                table: "requests");

            migrationBuilder.AddForeignKey(
                name: "FK_requests_users_clientId",
                table: "requests",
                column: "clientId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
