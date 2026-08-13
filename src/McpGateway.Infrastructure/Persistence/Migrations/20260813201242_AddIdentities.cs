using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace McpGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identities",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identity_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_scopes = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identities", x => x.client_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identities");
        }
    }
}
