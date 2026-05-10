using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Agentor.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c>.
/// Reads the connection string from the AGENTOR_CONNECTION_STRING environment variable,
/// falling back to a local development default.
/// </summary>
public sealed class AgentorDbContextFactory : IDesignTimeDbContextFactory<AgentorDbContext>
{
    public AgentorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTOR_CONNECTION_STRING")
            ?? "Host=localhost;Database=agentor;Username=agentor;Password=agentor";

        var opts = new DbContextOptionsBuilder<AgentorDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AgentorDbContext(opts);
    }
}
