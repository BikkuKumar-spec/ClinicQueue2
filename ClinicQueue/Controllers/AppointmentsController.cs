using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentsController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request)
        {
            try
            {
                var appointment = await _appointmentService.CreateAsync(request);
                return Ok(appointment);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointment(string id)
        {
            var appointment = await _appointmentService.GetByIdAsync(id);
            if (appointment == null)
                return NotFound();
            return Ok(appointment);
        }

        [HttpGet("slots/available")]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] DateTime? date, [FromQuery] string doctorName)
        {
            var targetDate = date ?? DateTime.Today;
            var slots = await _appointmentService.GetAvailableSlotsAsync(targetDate, doctorName);
            return Ok(slots);
        }

        [HttpPatch("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                await _appointmentService.UpdateStatusAsync(id, request.Status);
                return Ok(new { message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        [HttpGet("debug/db")]
        public async Task<IActionResult> DebugDb()
        {
            var slots = await _appointmentService.GetDashboardSlotsAsync(DateTime.Today);
            return Ok(new { 
                Message = "Debug Data", 
                Timestamp = DateTime.UtcNow,
                SlotsTotal = slots.Count,
                First5Booked = slots.Where(s => s.IsBooked).Take(5).ToList()
            });
        }
    }
}
