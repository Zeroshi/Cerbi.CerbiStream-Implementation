using Microsoft.Extensions.Logging;

namespace CerbiStream_Implementation.Repositories
{
    // Repository layer: simulates data access. In a real application this would query a database.
    // Demonstrates governed logging at the persistence boundary where identifiers often appear.
    public class PatientRepository
    {
        private readonly ILogger<PatientRepository> _logger;
        public PatientRepository(ILogger<PatientRepository> logger) => _logger = logger;

        // GetByNpi
        // Logs the requested NPI (identifier). Governance redacts it downstream.
        // Returns a simple anonymous object for demonstration if the NPI matches a known test value.
        // In production you would map to a concrete model type and exclude unnecessary sensitive fields.
        public object? GetByNpi(string npi)
        {
            _logger.LogInformation("Repository get {topic} {npi}", "NPI", npi);
            // Simulated lookup (no real DB). The returned object purposely includes non-sensitive name fields.
            return npi == "1234567890" ? new { npi, firstName = "Jane", lastName = "Doe" } : null;
        }
    }
}
