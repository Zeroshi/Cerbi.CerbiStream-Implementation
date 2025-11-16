using System.Text.Json;
using CerbiStream.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CerbiStream_Implementation.Tests
{
    public class LoggingTests
    {
        private ServiceProvider CreateProvider(string primaryPath, string fallbackPath, out ILoggerFactory loggerFactory)
        {
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddCerbiStream(o =>
                {
                    o.WithFileFallback(fallbackPath, primaryPath)
                     .WithTelemetryEnrichment(true)
                     .WithQueueBuffering(true);
                });
            });
            var sp = services.BuildServiceProvider();
            loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return sp;
        }

        [Fact]
        public void Writes_To_Primary_File()
        {
            var dir = Path.Combine("TestLogs", "Log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var primary = Path.Combine(dir, "primary.log");
            var fallback = Path.Combine(dir, "fallback.log");

            using var provider = CreateProvider(primary, fallback, out var lf);
            var logger = lf.CreateLogger("Test");
            logger.LogInformation("hello {orderId}", 123);

            var content = File.ReadAllText(primary);
            content.Should().Contain("orderId");
            content.Should().Contain("123");
        }

        [Fact]
        public void Enqueues_To_Queue_Buffer()
        {
            var dir = Path.Combine("TestLogs", "Log-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var primary = Path.Combine(dir, "primary.log");
            var fallback = Path.Combine(dir, "fallback.log");

            string? captured = null;
            CerbiStreamLogger.OnEnqueue = s => captured = s;
            using var provider = CreateProvider(primary, fallback, out var lf);
            var logger = lf.CreateLogger("Test");
            logger.LogInformation("hello {x}", 1);
            captured.Should().NotBeNull();
            var json = JsonDocument.Parse(captured!).RootElement;
            json.GetProperty("x").GetInt32().Should().Be(1);
        }
    }
}
