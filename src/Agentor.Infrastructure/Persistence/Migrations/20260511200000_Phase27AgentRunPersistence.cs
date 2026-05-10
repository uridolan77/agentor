using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase27AgentRunPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "aggregate_version",
            table: "agent_runs",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "resume_cursor_json",
            table: "agent_runs",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "aggregate_version",
            table: "agent_runs");

        migrationBuilder.DropColumn(
            name: "resume_cursor_json",
            table: "agent_runs");
    }
}
