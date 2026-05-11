using System.Text.Json.Serialization;
using Agentor.Api;
using Agentor.Api.Endpoints;
using Agentor.Api.Middleware;
using Agentor.Api.Security;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgentorApplication();
builder.Services.AddAgentorInfrastructure(builder.Configuration);

// Switch to EF Core + PostgreSQL when configured.
var persistenceOpts = builder.Configuration
    .GetSection(AgentorPersistenceOptions.SectionName)
    .Get<AgentorPersistenceOptions>() ?? new AgentorPersistenceOptions();

if (persistenceOpts.Mode == AgentorPersistenceOptions.ModePostgres
    && !string.IsNullOrWhiteSpace(persistenceOpts.ConnectionString))
{
    builder.Services.AddAgentorEfCoreRepository(db =>
        db.UseNpgsql(persistenceOpts.ConnectionString));
}

builder.Services.AddOpenApi();

builder.Services.AddAgentorWebAuthentication(builder.Configuration);
builder.Services.AddAgentorWebAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActorAccessor, HeaderOrFakeActorAccessor>();
builder.Services.AddScoped<IAuthorizationDecisionService, RoleBasedAuthorizationDecisionService>();
builder.Services.AddSingleton<IValidateOptions<AgentorAuthOptions>, AgentorAuthOptionsValidator>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddOptions<AgentorRuntimeOptions>()
    .BindConfiguration(AgentorRuntimeOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AgentorPersistenceOptions>()
    .BindConfiguration(AgentorPersistenceOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<AgentorAuthOptions>()
    .BindConfiguration(AgentorAuthOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<AgentorPublicRunOptions>(
    builder.Configuration.GetSection(AgentorPublicRunOptions.SectionName));

builder.Services.Configure<RuntimePolicyOptions>(
    builder.Configuration.GetSection(RuntimePolicyOptions.SectionName));

builder.Services.Configure<AuditExportOptions>(
    builder.Configuration.GetSection(AuditExportOptions.SectionName));

builder.Services.Configure<ToolExecutionOptions>(
    builder.Configuration.GetSection(ToolExecutionOptions.SectionName));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestTracingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

app.MapSystemEndpoints();

var v1 = app.MapGroup("/api/v1")
    .RequireAuthorization(AgentorAuthorizationPolicies.Authenticated);
v1.MapAgentRunEndpoints();
v1.MapRunQueueEndpoints();
v1.MapOpsEndpoints();
v1.MapAthanorEndpoints();
v1.MapGovernanceEndpoints();
v1.MapPolicyBundleEndpoints();
Phase13ProductEndpoints.MapProductSurface(v1);

app.Run();

public partial class Program
{
}
