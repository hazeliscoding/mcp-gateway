using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requester_client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    risk_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    environment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resource = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_requester_client_id_tool_name_version_sta~",
                table: "approval_requests",
                columns: new[] { "requester_client_id", "tool_name", "version", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_status",
                table: "approval_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_requests");
        }
    }
}
