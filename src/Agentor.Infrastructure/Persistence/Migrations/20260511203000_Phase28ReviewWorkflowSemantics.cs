using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Phase28ReviewWorkflowSemantics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "review_requested_at",
            table: "agent_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "paused_at",
            table: "agent_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "terminal_at",
            table: "agent_runs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "review_workflow_status",
            table: "agent_runs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "None");

        // Phase 28 semantic repair: completed_at was previously overloaded for review suspension and failures.
        migrationBuilder.Sql(
            """
            UPDATE agent_runs
            SET terminal_at = completed_at,
                completed_at = NULL
            WHERE status = 'Failed';
            """);

        migrationBuilder.Sql(
            """
            UPDATE agent_runs
            SET terminal_at = completed_at
            WHERE status = 'Completed';
            """);

        migrationBuilder.Sql(
            """
            UPDATE agent_runs
            SET paused_at = completed_at,
                review_requested_at = completed_at,
                completed_at = NULL,
                review_workflow_status = 'Pending'
            WHERE status = 'RequiresReview' AND completed_at IS NOT NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "review_requested_at",
            table: "agent_runs");

        migrationBuilder.DropColumn(
            name: "paused_at",
            table: "agent_runs");

        migrationBuilder.DropColumn(
            name: "terminal_at",
            table: "agent_runs");

        migrationBuilder.DropColumn(
            name: "review_workflow_status",
            table: "agent_runs");
    }
}
