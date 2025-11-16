using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using CerbiStream.Configuration;
using Xunit;

namespace CerbiStream_Implementation.Tests
{
    public class ConfigurationAndRotationTests
    {
        [Fact]
        public void Binds_Cerbi_Config_From_Appsettings()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CerbiStream-Implementation"));
            var path = Path.Combine(projectRoot, "appsettings.json");
            var builder = new ConfigurationBuilder().AddJsonFile(path, optional: false);
            var cfg = builder.Build();
            var section = cfg.GetSection("CerbiStream");
            var bound = section.Get<CerbiStream_Implementation.CerbiConfig>();
            bound.Should().NotBeNull();
            bound!.PrimaryFile.Should().Contain("logs/primary-governed.log");
        }

        [Fact]
        public void Rotates_File_When_Too_Large()
        {
            var dir = Path.Combine("TestLogs", "Rot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var primary = Path.Combine(dir, "rotate.log");
            var fallback = Path.Combine(dir, "fallback.log");

            var services = new ServiceCollection();
            services.AddSingleton(new CerbiStream_Implementation.CerbiConfig
            {
                PrimaryFile = primary,
                FallbackFile = fallback,
                Rotation = new CerbiStream_Implementation.RotationConfig { MaxFileSizeBytes = 1, MaxFileAgeMinutes = int.MaxValue }
            });
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddCerbiStream(o => o.WithFileFallback(fallback, primary));
            });
            services.AddHostedService<CerbiStream_Implementation.SampleBackgroundWorker>();
            var sp = services.BuildServiceProvider();

            File.WriteAllText(primary, new string('x', 100));
            var worker = sp.GetServices<IHostedService>().OfType<CerbiStream_Implementation.SampleBackgroundWorker>().Single();
            var mi = typeof(CerbiStream_Implementation.SampleBackgroundWorker).GetMethod("RotateIfNeeded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Should().NotBeNull();
            mi!.Invoke(worker, new object[] { primary, new CerbiStream_Implementation.RotationConfig { MaxFileSizeBytes = 1, MaxFileAgeMinutes = int.MaxValue } });

            Directory.GetFiles(dir, "rotate.log.*.archive").Should().NotBeEmpty();
        }
    }
}
