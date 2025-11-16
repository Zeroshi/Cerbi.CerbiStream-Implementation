using System.Text.Json;
using CerbiStream.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CerbiStream_Implementation.Tests
{
    public class GovernanceTests
    {
        private (ILogger logger,string dir,string primary) CreateLoggerWithIsolatedFiles()
        {
            var dir = Path.Combine("TestLogs", "Gov-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var primary = Path.Combine(dir, "primary.log");
            var fallback = Path.Combine(dir, "fallback.log");

            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddCerbiGovernanceRuntime(LoggerFactory.Create(b=>{}), "default", "cerbi_governance.json");
                b.AddCerbiStream(o =>
                {
                    o.WithFileFallback(fallback, primary)
                     .WithTelemetryEnrichment(true)
                     .WithQueueBuffering(false)
                     .WithGovernanceProfile("default", "cerbi_governance.json");
                });
            });
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("GovTest");
            return (logger,dir,primary);
        }

        [Fact]
        public void Redacts_Forbidden_Fields_And_Tag_Violations()
        {
            var (logger,dir,primary) = CreateLoggerWithIsolatedFiles();
            // Use structured template so keys are captured by MEL
            logger.LogInformation("signup {email} {password} {ssn}", "a@b.com", "x", "111-11-1111");

            var lines = File.ReadAllLines(primary);
            lines.Should().NotBeEmpty();
            var json = JsonDocument.Parse(lines.Last()).RootElement;
            json.GetProperty("password").GetString().Should().Be("***REDACTED***");
            var v = json.GetProperty("GovernanceViolations");
            v.ValueKind.Should().Be(JsonValueKind.Array);
            v.GetArrayLength().Should().BeGreaterThan(0);
        }
    }
}
