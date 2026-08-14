using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    approval_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_actor_client_id",
                table: "audit_entries",
                column: "actor_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_occurred_at",
                table: "audit_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_tool_name",
                table: "audit_entries",
                column: "tool_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");
        }
    }
}
