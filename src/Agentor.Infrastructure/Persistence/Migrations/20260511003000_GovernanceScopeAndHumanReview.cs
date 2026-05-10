using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

public partial class GovernanceScopeAndHumanReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "human_review_decisions_json",
            table: "agent_runs",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<Guid>(
            name: "knowledge_scope_id",
            table: "agent_runs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "project_id",
            table: "agent_runs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "tenant_id",
            table: "agent_runs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "workspace_id",
            table: "agent_runs",
            type: "uuid",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "human_review_decisions_json", table: "agent_runs");
        migrationBuilder.DropColumn(name: "knowledge_scope_id", table: "agent_runs");
        migrationBuilder.DropColumn(name: "project_id", table: "agent_runs");
        migrationBuilder.DropColumn(name: "tenant_id", table: "agent_runs");
        migrationBuilder.DropColumn(name: "workspace_id", table: "agent_runs");
    }
}
