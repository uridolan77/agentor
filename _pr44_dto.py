import pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")

dto = ROOT / "src/Agentor.Contracts/RunManifestDto.cs"
dt = dto.read_text(encoding="utf-8")
dt = dt.replace(
    """    string? PrimaryModelProfileRef,
    string ManifestVersion,
    string ContentHash);""",
    """    string? PrimaryModelProfileRef,
    int ExternalAgentInvocationCompletedCount,
    string ManifestVersion,
    string ContentHash);""",
)
dto.write_text(dt, encoding="utf-8")

map_path = ROOT / "src/Agentor.Api/Mapping/DtoMappings.cs"
mt = map_path.read_text(encoding="utf-8")
mt = mt.replace(
    """            manifest.PrimaryModelProfileRef,
            manifest.ManifestVersion,
            manifest.ContentHash);""",
    """            manifest.PrimaryModelProfileRef,
            manifest.ExternalAgentInvocationCompletedCount,
            manifest.ManifestVersion,
            manifest.ContentHash);""",
)
map_path.write_text(mt, encoding="utf-8")

api_tests = ROOT / "tests/Agentor.Api.Tests/ApiContractTests.cs"
at = api_tests.read_text(encoding="utf-8")
at = at.replace('Assert.Equal("1.1", manifest.ManifestVersion);', 'Assert.Equal("1.2", manifest.ManifestVersion);')
api_tests.write_text(at, encoding="utf-8")

print("dto/api patched")
