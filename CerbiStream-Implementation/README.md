# CerbiStream Demo – A Plain?Language, End?to?End Guide (Public Ready)

> Purpose: This public demo shows how CerbiStream governance protects structured logging (redaction, tagging, relaxed diagnostics) while remaining easy to adopt. It is written for both developers and non?technical reviewers.

## Table of Contents
1. Quick Start (3 minutes)
2. What & Why (Problem ? Solution)
3. Demo Contents Overview
4. File Map
5. Governance Policy JSON Explained
6. Build?time Analyzer (Shift?Left Safety)
7. Runtime Governance Flow
8. Topics / App / Env Grouping for Dashboards
9. Background Jobs & File Rotation
10. Test Suite Proof
11. Code Walkthrough Sequence
12. Integration Copy?Paste Plan
13. CerbiShield (Central Governance Dashboard)
14. Additional / Advanced Features
15. Security & Operations Checklist
16. FAQ
17. Run the Demo
18. Summary & Next Steps
19. NuGet Packages (Used & Related)
20. License & Attribution (placeholder)

---

## 1. Quick Start (3 minutes)
```
git clone <repo>
cd CerbiStream-Implementation
dotnet build
dotnet run --project CerbiStream-Implementation/CerbiStream-Implementation.csproj
curl https://localhost:<PORT>/demo/violation
curl https://localhost:<PORT>/demo/relaxed
dotnet test CerbiStreamDemo.sln
```
Outcome: You will see governed log output (redacted sensitive fields) and relaxed diagnostic output (tagged, not redacted) in console + log files.

---

## 2. What & Why (Problem ? Solution)
Apps log rich structured data. Without guardrails, sensitive information (PII/PHI/NPI, passwords, credit cards, private keys) can leak into logs, dashboards, SIEMs, data lakes. CerbiStream adds a governance layer that:
- Redacts forbidden fields automatically
- Tags events with violations and profile version
- Provides a relaxed mode (explicit, auditable) for rare diagnostic exceptions
- Warns developers at build time via analyzer before unsafe logging ships

Plain?language: CerbiStream is the seat belt & airbag system for your logging – invisible until needed.

---

## 3. Demo Contents Overview
Features illustrated:
- Analyzer (NuGet) flags forbidden fields at compile time
- Runtime redaction & violation tagging
- Relaxed events with `GovernanceRelaxed=true`
- Topics (`topic`), application name (`app`), environment (`env`) enrichment for dashboards
- File fallback + simple rotation
- Queue buffering simulation
- Layered architecture (Controller ? Service ? Repository ? Worker)
- Unit tests proving each behavior

---

## 4. File Map
| Area | File | Purpose |
|------|------|---------|
| Bootstrap | `Program.cs` | Configure logging, governance, endpoints, worker |
| API | `Controllers/NpiController.cs` | Shows normal & relaxed NPI flows |
| Service | `Services/PatientService.cs` | Business logic logging |
| Repository | `Repositories/PatientRepository.cs` | Data lookup logging |
| Config | `appsettings.json` | Paths & toggles |
| Governance | `cerbi_governance.json` | Policy rules (forbidden/disallowed/required) |
| Tests | `*.Tests` project | Automated verification |

---

## 5. Governance Policy JSON Explained
`cerbi_governance.json` defines:
- `DisallowedFields` & `FieldSeverities`: which fields get redacted (e.g., password, ssn, creditCard, npi, pem)
- `RequiredFields`: ensure baseline telemetry keys (e.g., message, timestamp)
- Optional profiles (e.g., `diagnostics`) supporting relaxation (`AllowRelaxed`) when authorized
Benefits:
- Single source of truth for data safety
- Auditable versioning (profile version surfaces in each event)

---

## 6. Build?time Analyzer (Shift?Left Safety)
Package: `CerbiStream.GovernanceAnalyzer`
- Flags forbidden field names in structured logging calls
- Prevents risky code from merging unnoticed
Recommended: escalate analyzer severity via `.editorconfig` ? treat specific diagnostics as errors in CI.

---

## 7. Runtime Governance Flow
Normal event:
1. User code calls `logger.LogInformation("signup {email} {password}", ...)`
2. Governance wrapper inspects fields
3. Forbidden fields replaced with `***REDACTED***`
4. `GovernanceViolations` array populated with metadata
5. Enrichment adds `topic`, `app`, `env`, `GovernanceProfileVersion`
6. Logs written to primary + fallback (fallback encoded here as Base64; real deployments use AES)
Relaxed event:
1. Event includes `GovernanceRelaxed=true`
2. Redaction skipped intentionally, field values preserved
3. Event tagged for audit – dashboards can alert on frequency

---

## 8. Topics / App / Env Grouping for Dashboards
Every event contains:
- `topic` (feature area) – e.g., NPI, Payments, Auth
- `app` – CerbiStreamDemo
- `env` – Production/Development
Usage in Azure App Insights (conceptual KQL):
```
traces
| extend d=todynamic(customDimensions)
| summarize count() by tostring(d.topic), tostring(d.env)
```
Find violations:
```
traces
| extend d=todynamic(customDimensions)
| where array_length(todynamic(d.GovernanceViolations)) > 0
```

---

## 9. Background Jobs & File Rotation
Background worker simulates batch logging tasks and rotates files by size/age. Real systems: integrate with OTEL exporters, cloud sinks, and robust encryption.

---

## 10. Test Suite Proof
Run `dotnet test` – tests cover:
- Logging write & queue behavior
- Redaction & violation tagging
- Relaxed mode preserves raw sensitive values
- Configuration binding + rotation
- Analyzer package presence
All green ? functionality demonstrated.

---

## 11. Code Walkthrough Sequence (Open `Program.cs`)
1. Create builder
2. Bind config (CerbiConfig)
3. Register repository & service
4. Configure logging + governance profile
5. Wrap governance runtime
6. Hosted worker registration
7. Map violation + relaxed endpoints
8. Runtime logger: inspect ? redact/violate ? enrich ? write files ? output console

---

## 12. Integration Copy?Paste Plan
```
// Analyzer
 dotnet add package CerbiStream.GovernanceAnalyzer

// Runtime (replace stub with official package when available)
builder.Logging.AddCerbiStream(o =>
    o.WithFileFallback("logs/fallback.json","logs/primary.json")
     .WithAesEncryption()
     .WithEncryptionKey(keyBytes, ivBytes)
     .WithGovernanceProfile("default","./cerbi_governance.json")
     .WithTelemetryEnrichment(true));
```
Add `topic`, `app`, `env` to your events (or rely on automatic enrichment). Version governance JSON in source control or CerbiShield.

---

## 13. CerbiShield (Central Governance Dashboard)
CerbiShield removes per?repo JSON editing:
- Central policy authoring & version history
- Controlled rollout & validation
- Visual dashboards: number of violations, relaxed events, top offending fields
- Policy distribution & hot?reload agents
Result: Consistency, reduced friction, faster remediation.

---

## 14. Additional / Advanced Features (Future or Out?of?Scope Here)
- True AES encryption & key rotation
- Hot profile reload (watcher) without restarts
- Multi?tenant or multi?profile dynamic selection
- Export governed events via OTLP/Kafka/Azure Queue
- Serilog/NLog/ELK integration (govern first, forward after)
- Schema enforcement & required context sets
- Rate limits & sampling for high?volume diagnostics

---

## 15. Security & Operations Checklist
- Secrets: Key Vault / KMS only (no hardcoded keys)
- Analyzer severity: escalate to errors in CI
- Monitoring: alert on violation spikes or excessive relaxed events
- Dashboards: group by topic/app/env for clarity
- Audit: track `GovernanceProfileVersion` for incident forensics
- Access control: limit ability to emit relaxed events

---

## 16. FAQ
Q: Does this replace Serilog/NLog/App Insights?  
A: No – it governs before those sinks.
Q: Performance impact?  
A: Minimal overhead for redaction; measure & tune. 
Q: Can we disable redaction?  
A: Possible but strongly discouraged; use relaxed mode sparingly. 
Q: Multi?environment?  
A: Maintain separate profiles or dynamic selection; CerbiShield streamlines this.

---

## 17. Run the Demo
```
dotnet build
(dotnet run --project CerbiStream-Implementation/CerbiStream-Implementation.csproj)
# Then hit endpoints listed in Quick Start
```
View governed log files under `logs/` and test logs under `TestLogs/` (created dynamically during test runs).

---

## 18. Summary & Next Steps
CerbiStream adds safety, structure, and observability clarity with minimal friction:
- Shift?left detection (Analyzer)
- Runtime enforcement (Redaction & tagging)
- Controlled exceptions (Relaxed mode)
- Rich grouping (topic/app/env)
Next: Replace stub with official runtime, connect exporters, onboard CerbiShield, and promote analyzer diagnostics to CI errors.

---

## 19. NuGet Packages (Used & Related)
### Used in this Demo
- CerbiStream.GovernanceAnalyzer – Build?time analyzer flagging forbidden field usage.  
  Link: https://www.nuget.org/packages/CerbiStream.GovernanceAnalyzer

### Suggested / Related (if available in your environment)
- CerbiStream.Runtime – Core runtime governance & redaction (replace the stub code here). (Placeholder if not yet public)
- CerbiStream.OpenTelemetry – Export governed events directly via OTLP to observability backends. (Placeholder)
- CerbiStream.Encryption – Strong AES/GCM encryption helpers for fallback log streams. (Placeholder)
- CerbiStream.Serilog / CerbiStream.NLog – Adapters to insert governance before those logging pipelines. (Placeholder)
- CerbiStream.Transport.Kafka / Queue – Reliable asynchronous dispatch of governed events. (Placeholder)
- CerbiShield.CLI / CerbiShield.Agent – Synchronize and hot?reload governance profiles from CerbiShield dashboard. (Placeholder)

If a package is marked “Placeholder,” treat it as a conceptual module; substitute actual package names once published.

---

## 20. License & Attribution
This project is under the MIT License (see `LICENSE`).
Add badges (CI, NuGet, Docs) here when publishing:
- Build Status: (badge placeholder)
- NuGet Version (Analyzer): (badge placeholder)
- License: MIT

---

Your logs become safer, searchable, and more trustworthy. Adoption is quick. Governance stops being a burden and becomes an asset.
