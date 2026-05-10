using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgentorDbContext))]
[Migration("20260510143000_AddAgentRunIdempotencyKeys")]
public sealed class AddAgentRunIdempotencyKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "agent_run_idempotency_keys",
            columns: table => new
            {
                idempotency_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                request_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                agent_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_agent_run_idempotency_keys", x => x.idempotency_key);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "agent_run_idempotency_keys");
    }
}