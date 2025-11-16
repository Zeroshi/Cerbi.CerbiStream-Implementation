using CerbiStream.Configuration; // Governance + logging extensions (stub implementation for demo)
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

// ------------------------------------------------------------
// CerbiStream Demo Entry Point
// ------------------------------------------------------------
// This file bootstraps a minimal Web API and wires CerbiStream governance.
// It shows: configuration binding, layered logging, governance redaction,
// relaxed diagnostics, enrichment (topic/app/env), file fallback & rotation.
// ------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args); // Standard ASP.NET Core host builder

// 1. Load CerbiStream configuration (paths, toggles) from appsettings.json.
var cerbiSection = builder.Configuration.GetSection("CerbiStream");
var cerbiConfig = cerbiSection.Get<CerbiStream_Implementation.CerbiConfig>() ?? new CerbiStream_Implementation.CerbiConfig();

builder.Services.AddSingleton(cerbiConfig); // Inject into other components

// 2. Register domain services to simulate a layered architecture.
builder.Services.AddScoped<CerbiStream_Implementation.Repositories.PatientRepository>();
builder.Services.AddScoped<CerbiStream_Implementation.Services.PatientService>();

// 3. Add controllers (API endpoints).
builder.Services.AddControllers();

// 4. Configure logging pipeline with CerbiStream stub options.
//    In production, replace this stub with the official runtime package.
builder.Logging.ClearProviders(); // Remove default providers for clarity in demo output
builder.Logging.AddCerbiStream(options =>
{
    options
        .WithFileFallback(cerbiConfig.FallbackFile, cerbiConfig.PrimaryFile)        // Primary + fallback logging destinations
        .WithGovernanceProfile(cerbiConfig.GovernanceProfile, cerbiConfig.GovernanceConfigPath) // Link governance JSON
        .WithTelemetryEnrichment(cerbiConfig.TelemetryEnrichment)                  // Add enrichment metadata
        .WithQueueBuffering(cerbiConfig.QueueBuffering);                           // Enable in-memory queue simulation
    if (cerbiConfig.EnableAes)
    {
        // Demo uses a static key for brevity. Real apps: use Key Vault / KMS.
        options.WithAesEncryption().WithEncryptionKey(System.Text.Encoding.UTF8.GetBytes("1234567890123456"), System.Text.Encoding.UTF8.GetBytes("1234567890123456"));
    }
});

// 5. Add governance runtime wrapper (applies redaction/violation tagging before output).
var innerFactory = LoggerFactory.Create(b => { }); // Inner factory placeholder
builder.Logging.AddCerbiGovernanceRuntime(innerFactory, cerbiConfig.GovernanceProfile, cerbiConfig.GovernanceConfigPath);

// 6. Background worker shows batch scenario + rotation logic.
builder.Services.AddHostedService<CerbiStream_Implementation.SampleBackgroundWorker>();

var app = builder.Build();
app.MapControllers(); // Map attribute-routed controllers

// ------------------------------------------------------------
// Demo Endpoint: /demo/violation
// Emits multiple forbidden fields to illustrate automatic redaction and tagging.
// ------------------------------------------------------------
app.MapGet("/demo/violation", (ILoggerFactory lf) =>
{
    var logger = lf.CreateLogger("GovernanceDemo");
    // Structured template placeholders become properties; analyzer & runtime catch forbidden fields.
    logger.LogInformation("User signup attempt with PII {email} {ssn} {password}", "user@example.com", "111-11-1111", "SuperSecret!");
    logger.LogInformation("NPI received {npi}", "1234567890");
    logger.LogInformation("Payment attempt {creditCard} {amount}", "4111-1111-1111-1111", 10.0);
    return Results.Ok(new { status = "logged" });
});

// ------------------------------------------------------------
// Demo Endpoint: /demo/relaxed
// Shows a deliberately relaxed diagnostic event. Sensitive data passes, but is flagged.
// ------------------------------------------------------------
app.MapGet("/demo/relaxed", (ILoggerFactory lf) =>
{
    var logger = lf.CreateLogger("GovernanceDemo.Relaxed");
    logger.LogInformation("Diagnostic dump {topic} {GovernanceRelaxed} {pem}", "AuthFlow", true, "-----BEGIN PRIVATE KEY----- ...");
    return Results.Ok(new { status = "relaxed-logged" });
});

app.Run(); // Start HTTP server

namespace CerbiStream_Implementation
{
    // Configuration container bound from appsettings.json.
    public class CerbiConfig
    {
        public string PrimaryFile { get; set; } = "logs/primary-governed.log";      // Main governed log file
        public string FallbackFile { get; set; } = "logs/fallback-encrypted.log";   // Secondary (simulated encrypted) file
        public bool EnableAes { get; set; } = true;                                  // Toggle encryption (demo stub)
        public string GovernanceProfile { get; set; } = "default";                  // Active governance profile name
        public string GovernanceConfigPath { get; set; } = "cerbi_governance.json"; // JSON rules file path
        public bool TelemetryEnrichment { get; set; } = true;                        // Add metadata fields
        public bool QueueBuffering { get; set; } = true;                             // Simulated queue transport
        public RotationConfig Rotation { get; set; } = new();                        // Rotation thresholds
    }
    public class RotationConfig
    {
        public int MaxFileSizeBytes { get; set; } = 1048576; // 1 MB size rotation trigger
        public int MaxFileAgeMinutes { get; set; } = 60;      // Time-based rotation trigger
    }

    // Background worker demonstrates governance also applies to non-HTTP tasks.
    public class SampleBackgroundWorker : BackgroundService
    {
        private readonly ILogger<SampleBackgroundWorker> _logger;
        private readonly CerbiConfig _cfg;
        public SampleBackgroundWorker(ILogger<SampleBackgroundWorker> logger, CerbiConfig cfg) { _logger = logger; _cfg = cfg; }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var i = 0;
            while (!stoppingToken.IsCancellationRequested && i < 5)
            {
                _logger.LogInformation("Processing batch {batchId}", i); // Normal operational log
                try { if (i == 3) throw new InvalidOperationException("Simulated failure in batch processing"); }
                catch (Exception ex) { _logger.LogError(ex, "Exception encountered while processing batch {batchId}", i); }
                await Task.Delay(500, stoppingToken); i++;
            }
            RotateIfNeeded(_cfg.PrimaryFile, _cfg.Rotation);   // After run, rotate governed file if threshold exceeded
            RotateIfNeeded(_cfg.FallbackFile, _cfg.Rotation);  // Rotate fallback file
        }

        private void RotateIfNeeded(string path, RotationConfig rotation)
        {
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            var ageMinutes = (DateTime.UtcNow - info.CreationTimeUtc).TotalMinutes;
            if (info.Length > rotation.MaxFileSizeBytes || ageMinutes > rotation.MaxFileAgeMinutes)
            {
                var archive = path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".archive";
                File.Move(path, archive);
                _logger.LogInformation("Rotated log file {file} to {archive}", path, archive); // Logged through governance layer
            }
        }
    }
}

// ------------------------------------------------------------
// Governance + Logging Implementation (Stub)
// Replace this namespace with the official CerbiStream runtime library in production.
// ------------------------------------------------------------
namespace CerbiStream.Configuration
{
    // Options object describing how governed logging should behave.
    public sealed class CerbiStreamOptions
    {
        public string? PrimaryFilePath { get; private set; }
        public string? FallbackFilePath { get; private set; }
        public bool UseAes { get; private set; }
        public byte[]? Key { get; private set; }
        public byte[]? IV { get; private set; }
        public string GovernanceProfileName { get; private set; } = "default";
        public string GovernanceConfigPath { get; private set; } = "cerbi_governance.json";
        public bool TelemetryEnrichment { get; private set; }
        public bool QueueBuffering { get; private set; }

        public CerbiStreamOptions WithFileFallback(string fallback, string primary) { FallbackFilePath = fallback; PrimaryFilePath = primary; return this; }
        public CerbiStreamOptions WithAesEncryption() { UseAes = true; return this; }
        public CerbiStreamOptions WithEncryptionKey(byte[] key, byte[] iv) { Key = key; IV = iv; return this; }
        public CerbiStreamOptions WithGovernanceProfile(string name, string path) { GovernanceProfileName = name; GovernanceConfigPath = path; return this; }
        public CerbiStreamOptions WithTelemetryEnrichment(bool enabled) { TelemetryEnrichment = enabled; return this; }
        public CerbiStreamOptions WithQueueBuffering(bool enabled) { QueueBuffering = enabled; return this; }
    }

    // Extension methods to register governed logging with the host builder.
    public static class CerbiStreamLoggingBuilderExtensions
    {
        public static ILoggingBuilder AddCerbiStream(this ILoggingBuilder builder, Action<CerbiStreamOptions> configure)
        {
            var opts = new CerbiStreamOptions();
            configure(opts);
            builder.AddProvider(new CerbiStreamLoggerProvider(opts));
            return builder;
        }
        public static ILoggingBuilder AddCerbiGovernanceRuntime(this ILoggingBuilder builder, ILoggerFactory innerFactory, string profileName, string configPath)
        {
            builder.AddProvider(new CerbiGovernanceRuntimeProvider(innerFactory, profileName, configPath));
            return builder;
        }
    }

    // Provider that creates individual governed loggers.
    internal sealed class CerbiStreamLoggerProvider : ILoggerProvider
    {
        private readonly CerbiStreamOptions _options;
        public CerbiStreamLoggerProvider(CerbiStreamOptions options) => _options = options;
        public ILogger CreateLogger(string categoryName) => new CerbiStreamLogger(categoryName, _options);
        public void Dispose() { }
    }

    // Wrapper provider layering governance redaction on top of inner loggers.
    internal sealed class CerbiGovernanceRuntimeProvider : ILoggerProvider
    {
        private readonly string _profileName; private readonly string _configPath; private GovernanceProfile? _profile; private readonly ILoggerFactory _innerFactory;
        public CerbiGovernanceRuntimeProvider(ILoggerFactory innerFactory, string profileName, string configPath)
        { _innerFactory = innerFactory; _profileName = profileName; _configPath = configPath; LoadProfile(); }
        private void LoadProfile()
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _profile = JsonSerializer.Deserialize<GovernanceProfile>(json);
            }
        }
        public ILogger CreateLogger(string categoryName) => new GovernanceWrappingLogger(_innerFactory.CreateLogger(categoryName), categoryName, _profileName, _profile);
        public void Dispose() { }
    }

    // Core logger applying governance, enrichment, and writing outputs.
    public sealed class CerbiStreamLogger : ILogger
    {
        public static Action<string>? OnEnqueue; // Test visibility hook
        private static readonly ConcurrentQueue<string> _queue = new();
        private readonly string _category; private readonly CerbiStreamOptions _opts; private readonly GovernanceProfile? _profile;
        public CerbiStreamLogger(string category, CerbiStreamOptions opts)
        {
            _category = category; _opts = opts;
            if (File.Exists(_opts.GovernanceConfigPath))
            {
                try { _profile = JsonSerializer.Deserialize<GovernanceProfile>(File.ReadAllText(_opts.GovernanceConfigPath)); } catch { }
            }
            else
            {
                // Fallback default profile (used when governance JSON not deployed). Keeps demo & tests functional.
                _profile = new GovernanceProfile
                {
                    Version = "fallback",
                    LoggingProfiles = new Dictionary<string, ProfileDefinition>
                    {
                        [_opts.GovernanceProfileName] = new ProfileDefinition
                        {
                            DisallowedFields = new List<string>{"ssn","creditCard","password","npi","pem"},
                            FieldSeverities = new Dictionary<string,string>{{"password","Forbidden"},{"secretValue","Forbidden"},{"npi","Forbidden"},{"pem","Forbidden"}},
                            RequiredFields = new List<string>{"message"}
                        }
                    }
                };
            }
        }
        public IDisposable? BeginScope<TState>(TState state) => null; // Scope unused in demo
        public bool IsEnabled(LogLevel logLevel) => true; // Accept all levels in demo
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var payload = new Dictionary<string, object?>
            {
                ["Category"] = _category,
                ["Level"] = logLevel.ToString(),
                ["Message"] = formatter(state, exception),
                ["Timestamp"] = DateTimeOffset.UtcNow
            };

            // Convert logging state (structured list or anonymous object) into a key/value map.
            Dictionary<string, object?> stateDict = new(StringComparer.OrdinalIgnoreCase);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> structured)
                foreach (var kv in structured) stateDict[kv.Key] = kv.Value;
            else if (state is not null)
                foreach (var kv in ReflectProperties(state!)) stateDict[kv.Key] = kv.Value;

            bool relaxed = stateDict.TryGetValue("GovernanceRelaxed", out var relaxObj) && relaxObj is bool b && b;
            var violations = new List<object>();
            if (!relaxed && _profile?.LoggingProfiles != null && _profile.LoggingProfiles.TryGetValue(_opts.GovernanceProfileName, out var prof))
            {
                foreach (var key in stateDict.Keys.ToList())
                {
                    if (prof.DisallowedFields?.Contains(key, StringComparer.OrdinalIgnoreCase) == true ||
                        (prof.FieldSeverities != null && prof.FieldSeverities.TryGetValue(key, out var sev) && string.Equals(sev, "Forbidden", StringComparison.OrdinalIgnoreCase)))
                    {
                        stateDict[key] = "***REDACTED***"; // Replace sensitive value
                        violations.Add(new { Code = "ForbiddenField", Field = key });
                    }
                }
            }
            payload["GovernanceProfileVersion"] = _profile?.Version ?? "1.0.0";
            payload["GovernanceViolations"] = violations;
            if (relaxed) payload["GovernanceRelaxed"] = true; // Explicit diagnostic bypass flag

            // Enrichment for dashboard grouping.
            if (!stateDict.ContainsKey("topic")) stateDict["topic"] = _category;
            if (!stateDict.ContainsKey("app")) stateDict["app"] = "CerbiStreamDemo";
            if (!stateDict.ContainsKey("env")) stateDict["env"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            foreach (var kv in stateDict)
                if (!payload.ContainsKey(kv.Key)) payload[kv.Key] = kv.Value;

            if (exception != null)
            {
                payload["Exception"] = exception.GetType().Name;
                payload["ExceptionMessage"] = exception.Message;
            }
            var json = JsonSerializer.Serialize(payload);

            // Simulated queue buffering
            if (_opts.QueueBuffering)
            {
                _queue.Enqueue(json);
                OnEnqueue?.Invoke(json); // Expose for tests
                while (_queue.Count > 50 && _queue.TryDequeue(out _)) { }
            }

            // File outputs (governed primary + encoded fallback).
            if (_opts.PrimaryFilePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_opts.PrimaryFilePath)!);
                File.AppendAllText(_opts.PrimaryFilePath, json + Environment.NewLine);
            }
            if (_opts.FallbackFilePath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_opts.FallbackFilePath)!);
                var toWrite = _opts.UseAes ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)) : json;
                File.AppendAllText(_opts.FallbackFilePath, toWrite + Environment.NewLine);
            }
            Console.WriteLine(json); // Console visibility for demo/video recording
        }

        internal static IEnumerable<KeyValuePair<string, object?>> ReflectProperties(object obj)
        {
            var type = obj.GetType();
            foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                yield return new KeyValuePair<string, object?>(p.Name, p.GetValue(obj));
        }
    }

    // Wrapper logger variant used by governance runtime provider.
    internal sealed class GovernanceWrappingLogger : ILogger
    {
        private readonly string _category; private readonly string _profileName; private readonly GovernanceProfile? _profile; private readonly ILogger _inner;
        public GovernanceWrappingLogger(ILogger inner, string category, string profileName, GovernanceProfile? profile) { _inner = inner; _category = category; _profileName = profileName; _profile = profile; }
        public IDisposable? BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var baseMessage = formatter(state, exception);
            var violationList = new List<object>();
            var redactedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object?> stateDict = new(StringComparer.OrdinalIgnoreCase);

            if (state is IReadOnlyList<KeyValuePair<string, object?>> structured)
                foreach (var kv in structured) stateDict[kv.Key] = kv.Value;
            else if (state is not null)
                foreach (var kv in CerbiStreamLogger.ReflectProperties(state!)) stateDict[kv.Key] = kv.Value;

            if (_profile?.LoggingProfiles != null && _profile.LoggingProfiles.TryGetValue(_profileName, out var prof))
            {
                foreach (var key in stateDict.Keys.ToList())
                {
                    if (prof.DisallowedFields?.Contains(key, StringComparer.OrdinalIgnoreCase) == true ||
                        (prof.FieldSeverities != null && prof.FieldSeverities.TryGetValue(key, out var sev) && string.Equals(sev, "Forbidden", StringComparison.OrdinalIgnoreCase)))
                    {
                        redactedFields.Add(key);
                        violationList.Add(new { Code = "ForbiddenField", Field = key });
                    }
                }
            }

            var governed = new List<KeyValuePair<string, object?>>
            {
                new("GovernanceProfileVersion", _profile?.Version ?? "unknown"),
                new("GovernanceViolations", violationList)
            };
            foreach (var kv in stateDict)
                governed.Add(new KeyValuePair<string, object?>(kv.Key, redactedFields.Contains(kv.Key) ? "***REDACTED***" : kv.Value));

            _inner.Log(logLevel, eventId, governed, exception, (s, e) => baseMessage);
        }
    }

    // Data model representing loaded governance profile(s) from JSON.
    public sealed class GovernanceProfile
    {
        public string? Version { get; set; }
        public Dictionary<string, ProfileDefinition>? LoggingProfiles { get; set; }
    }
    public sealed class ProfileDefinition
    {
        public List<string>? RequiredFields { get; set; }
        public List<string>? DisallowedFields { get; set; }
        public Dictionary<string,string>? FieldSeverities { get; set; }
    }
}
