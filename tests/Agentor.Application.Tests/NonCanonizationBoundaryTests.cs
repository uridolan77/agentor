using System.Reflection;
using Agentor.Application.Abstractions;
using Agentor.Contracts.KnowledgeState;
using Agentor.Domain;
using Agentor.Infrastructure.Athanor;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class NonCanonizationBoundaryTests
{
    [Fact]
    public void IKnowledgeStateClient_has_no_Canonize_operations()
    {
        foreach (var m in typeof(IKnowledgeStateClient).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            Assert.DoesNotContain("Canonize", m.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AgentRun_public_declared_surface_has_no_Canonize_methods()
    {
        foreach (var m in typeof(AgentRun).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            Assert.DoesNotContain("Canonize", m.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FakeKnowledgeStateClient_submit_status_is_explicitly_non_canon()
    {
        var sut = new FakeKnowledgeStateClient();
        var r = await sut.SubmitCandidateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CandidateKnowledgeSubmissionDto("x", "{}"),
            CancellationToken.None);
        Assert.Contains("non_canon", r.Status, StringComparison.OrdinalIgnoreCase);
    }
}