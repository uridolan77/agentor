using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgentorDbContext))]
[Migration("20260512100000_Phase33QueueStructuredToolPayload")]
public sealed class Phase33QueueStructuredToolPayload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
"""
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS tool_payload_json text;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
"""
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS tool_payload_json;
""");
    }
}
