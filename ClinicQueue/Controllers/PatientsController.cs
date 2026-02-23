using Microsoft.AspNetCore.Mvc;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/patients")]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientService _patientService;

        public PatientsController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatient(string id)
        {
            var patient = await _patientService.GetByIdAsync(id);
            if (patient == null)
                return NotFound();
            return Ok(patient);
        }

        [HttpGet("phone/{phoneNumber}")]
        public async Task<IActionResult> GetByPhone(string phoneNumber)
        {
            var patient = await _patientService.GetByPhoneAsync(phoneNumber);
            if (patient == null)
                return NotFound();
            return Ok(patient);
        }

        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(string id)
        {
            var history = await _patientService.GetHistoryAsync(id);
            return Ok(history);
        }
    }
}
