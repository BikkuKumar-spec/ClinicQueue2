using Microsoft.AspNetCore.Mvc;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/doctors")]
    public class DoctorsController : ControllerBase
    {
        private readonly IDoctorService _doctorService;

        public DoctorsController(IDoctorService doctorService)
        {
            _doctorService = doctorService;
        }

        [HttpGet("{id}/status")]
        public async Task<IActionResult> GetStatus(string id)
        {
            var status = await _doctorService.GetDoctorStatusAsync(id);
            if (status == null) return NotFound("Doctor not found");
            return Ok(status);
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] int status)
        {
            await _doctorService.SetDoctorStatusAsync(id, status);
            return Ok(new { success = true, status });
        }
    }
}
