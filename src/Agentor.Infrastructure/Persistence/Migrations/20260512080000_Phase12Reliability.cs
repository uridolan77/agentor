using Agentor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agentor.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AgentorDbContext))]
[Migration("20260512080000_Phase12Reliability")]
public sealed class Phase12Reliability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload_json = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "execution_leases",
            columns: table => new
            {
                resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                lease_holder = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_leases", x => x.resource_id);
            });

        migrationBuilder.CreateTable(
            name: "distributed_operations",
            columns: table => new
            {
                operation_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                committed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_distributed_operations", x => x.operation_key);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "distributed_operations");
        migrationBuilder.DropTable(name: "execution_leases");
        migrationBuilder.DropTable(name: "outbox_messages");
    }
}
