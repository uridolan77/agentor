using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SessionMemoryOnAgentRun : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "session_memory_json",
            table: "agent_runs",
            type: "text",
            nullable: false,
            defaultValue: "{}");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "session_memory_json",
            table: "agent_runs");
    }
}
