using System.Security.Cryptography;
using System.Text;

namespace Agentor.Api;

public static class StartAgentRunFingerprint
{
    private const char Sep = (char)31;

    public static string Compute(
        string? agentName,
        string objective,
        bool traceIdSpecifiedInBody,
        string? traceIdFromBody)
    {
        var normAgent = string.IsNullOrWhiteSpace(agentName) ? "PR1 Agent" : agentName;
        var traceToken = traceIdSpecifiedInBody && !string.IsNullOrWhiteSpace(traceIdFromBody)
            ? traceIdFromBody.Trim()
            : "";
        var canonical = string.Concat(normAgent, Sep, objective, Sep, traceToken);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash);
    }
}