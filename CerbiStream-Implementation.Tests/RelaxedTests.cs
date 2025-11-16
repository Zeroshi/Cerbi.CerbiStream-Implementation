using System.Text.Json;
using CerbiStream.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CerbiStream_Implementation.Tests
{
    public class RelaxedTests
    {
        [Fact]
        public void GovernanceRelaxed_Skips_Redaction_But_Tags_Event()
        {
            var dir = Path.Combine("TestLogs", "Relax-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var primary = Path.Combine(dir, "primary.log");
            var fallback = Path.Combine(dir, "fallback.log");
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddCerbiStream(o =>
                {
                    o.WithFileFallback(fallback, primary)
                     .WithGovernanceProfile("default", "cerbi_governance.json")
                     .WithTelemetryEnrichment(true);
                });
            });
            var sp = services.BuildServiceProvider();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RelaxTest");

            logger.LogInformation("Diagnostic dump {GovernanceRelaxed} {pem}", true, "-----BEGIN PRIVATE KEY----- ...");

            var last = File.ReadAllLines(primary).Last();
            var json = JsonDocument.Parse(last).RootElement;
            json.GetProperty("GovernanceRelaxed").GetBoolean().Should().BeTrue();
            json.GetProperty("pem").GetString().Should().Contain("BEGIN PRIVATE KEY");
        }
    }
}
