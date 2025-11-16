using CerbiStream_Implementation.Services; // Service (business logic) layer
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CerbiStream_Implementation.Controllers
{
    // API Controller demonstrating governed logging at the HTTP boundary.
    // Route prefix: /api/npi
    [ApiController]
    [Route("api/npi")]
    public class NpiController : ControllerBase
    {
        private readonly ILogger<NpiController> _logger;
        private readonly PatientService _service;
        public NpiController(ILogger<NpiController> logger, PatientService service)
        {
            _logger = logger; _service = service;
        }

        // POST /api/npi/lookup
        // Purpose: Normal lookup flow. The NPI value will be redacted by governance.
        // Logging pattern:
        // 1) Controller logs a request marker with a topic (feature area)
        // 2) Service + Repository add their own structured events
        // Governance outcome: 'npi' appears but is replaced with ***REDACTED*** in final governed log.
        [HttpPost("lookup")]
        public IActionResult Lookup([FromBody] LookupRequest req)
        {
            _logger.LogInformation("HTTP request {topic} {operation}", "NPI", "lookup");
            var patient = _service.LookupByNpi(req.Npi);
            return Ok(new { found = patient != null });
        }

        // POST /api/npi/diagnostics-dump
        // Purpose: Deliberate relaxed diagnostic event. 'GovernanceRelaxed' signals redaction should be bypassed.
        // Governance outcome: 'npi' is preserved in the output, and the event is flagged with GovernanceRelaxed=true.
        // Operational intent: Rare, authorized troubleshooting; dashboards can alert on frequency of relaxed events.
        [HttpPost("diagnostics-dump")]
        public IActionResult Diagnostics([FromBody] LookupRequest req)
        {
            _logger.LogInformation("HTTP diagnostic {topic} {GovernanceRelaxed} {npi}", "NPI", true, req.Npi);
            var patient = _service.LookupByNpi(req.Npi);
            return Ok(new { found = patient != null, relaxed = true });
        }

        // Request DTO (Data Transfer Object) carrying the NPI value from the client.
        public record LookupRequest(string Npi);
    }
}
