using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialToolRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tools",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tools", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "tool_versions",
                columns: table => new
                {
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    risk_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    approval_required = table.Column<bool>(type: "boolean", nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    input_schema = table.Column<string>(type: "jsonb", nullable: false),
                    output_schema = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    required_scopes = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_versions", x => new { x.tool_name, x.version });
                    table.ForeignKey(
                        name: "FK_tool_versions_tools_tool_name",
                        column: x => x.tool_name,
                        principalTable: "tools",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tool_versions");

            migrationBuilder.DropTable(
                name: "tools");
        }
    }
}
