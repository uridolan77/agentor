import pathlib
p = pathlib.Path(r"c:/dev/agentor/tests/Agentor.Domain.Tests/RunManifestTests.cs")
t = p.read_text(encoding="utf-8")
t = t.replace('Assert.Equal("1.1", manifest.ManifestVersion);', 'Assert.Equal("1.2", manifest.ManifestVersion);')
t = t.replace("null, null, null, null);", "null, null, null, null, 0);")
p.write_text(t, encoding="utf-8")
print("tests patched")
