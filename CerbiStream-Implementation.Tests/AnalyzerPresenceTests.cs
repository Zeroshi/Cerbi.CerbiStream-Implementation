using FluentAssertions;
using System.Xml.Linq;
using Xunit;
using System.IO;

namespace CerbiStream_Implementation.Tests
{
    public class AnalyzerPresenceTests
    {
        [Fact]
        public void Csproj_Contains_GovernanceAnalyzer_Package()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var csprojPath = Path.Combine(root, "CerbiStream-Implementation", "CerbiStream-Implementation.csproj");
            File.Exists(csprojPath).Should().BeTrue($"Expected csproj at {csprojPath}");

            var csproj = XDocument.Load(csprojPath);
            var has = csproj
                .Descendants("PackageReference")
                .Any(x => (string?)x.Attribute("Include") == "CerbiStream.GovernanceAnalyzer");
            has.Should().BeTrue("official governance analyzer must be referenced so violations surface at build time");
        }
    }
}
