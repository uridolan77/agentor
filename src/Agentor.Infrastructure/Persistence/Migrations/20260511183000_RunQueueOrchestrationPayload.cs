using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgentorDbContext))]
[Migration("20260511183000_RunQueueOrchestrationPayload")]
public sealed class RunQueueOrchestrationPayload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent for databases created only via EnsureCreated (no migration history) and for upgrades.
        migrationBuilder.Sql(
"""
CREATE TABLE IF NOT EXISTS run_queue_items (
    work_item_id uuid NOT NULL,
    agent_name character varying(500) NOT NULL,
    objective character varying(2000) NOT NULL,
    trace_id character varying(128),
    tenant_id uuid,
    workspace_id uuid,
    project_id uuid,
    knowledge_scope_id uuid,
    status character varying(64) NOT NULL,
    enqueued_at_utc timestamp with time zone NOT NULL,
    claimed_by character varying(256),
    lease_expires_at_utc timestamp with time zone,
    agent_run_id uuid,
    error character varying(2000),
    updated_at_utc timestamp with time zone NOT NULL,
    execution_mode character varying(64),
    recipe_id uuid,
    plan_id uuid,
    tool_key character varying(200),
    skill_key character varying(200),
    tool_input_json text,
    CONSTRAINT pk_run_queue_items PRIMARY KEY (work_item_id)
);
CREATE INDEX IF NOT EXISTS ix_run_queue_items_status_enqueued_at_utc ON run_queue_items (status, enqueued_at_utc);
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS execution_mode character varying(64);
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS recipe_id uuid;
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS plan_id uuid;
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS tool_key character varying(200);
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS skill_key character varying(200);
ALTER TABLE run_queue_items ADD COLUMN IF NOT EXISTS tool_input_json text;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
"""
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS tool_input_json;
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS skill_key;
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS tool_key;
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS plan_id;
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS recipe_id;
ALTER TABLE run_queue_items DROP COLUMN IF EXISTS execution_mode;
""");
    }
}
