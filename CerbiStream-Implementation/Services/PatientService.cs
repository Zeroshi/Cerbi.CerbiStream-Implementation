using CerbiStream_Implementation.Repositories; // Data access layer
using Microsoft.Extensions.Logging;

namespace CerbiStream_Implementation.Services
{
    // Service layer: business logic between controllers and repository.
    // Demonstrates logging at a mid-tier where sensitive inputs may still appear.
    public class PatientService
    {
        private readonly ILogger<PatientService> _logger;
        private readonly PatientRepository _repo;
        public PatientService(ILogger<PatientService> logger, PatientRepository repo)
        {
            _logger = logger; _repo = repo;
        }

        // LookupByNpi
        // Flow:
        // 1) Log inbound NPI (will be redacted by governance)
        // 2) Call repository
        // 3) Log outcome (found flag) – non-sensitive, retained as-is
        // Value added: Shows that developers can keep familiar logging calls; governance handles scrubbing automatically.
        public object? LookupByNpi(string npi)
        {
            _logger.LogInformation("Service lookup {topic} {npi}", "NPI", npi);
            var result = _repo.GetByNpi(npi);
            _logger.LogInformation("Service result {topic} {found}", "NPI", result != null);
            return result;
        }
    }
}
