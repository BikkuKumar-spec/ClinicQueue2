using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IQueueService _queueService;

        public DashboardController(
            IAppointmentService appointmentService,
            IQueueService queueService)
        {
            _appointmentService = appointmentService;
            _queueService = queueService;
        }

        [HttpGet("appointments/today")]
        public async Task<IActionResult> GetTodayAppointments([FromQuery] DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;
            var allAppointments = await _appointmentService.GetTodayAppointmentsAsync();
            var appointments = allAppointments.Where(a => a.SlotTime.Date == targetDate.Date).ToList();
            return Ok(appointments);
        }

        [HttpPatch("appointments/{id}/arrive")]
        public async Task<IActionResult> MarkArrived(string id)
        {
            try
            {
                await _appointmentService.UpdateStatusAsync(id, "ARRIVED");
                return Ok(new { success = true, message = "Patient marked as arrived and added to queue" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to mark patient as arrived: {ex.Message}" });
            }
        }

        [HttpPatch("appointments/{id}/no-show")]
        public async Task<IActionResult> MarkNoShow(string id)
        {
            try
            {
                await _appointmentService.UpdateStatusAsync(id, "NO_SHOW");
                return Ok(new { success = true, message = "Patient marked as no-show" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to mark patient as no-show: {ex.Message}" });
            }
        }

        [HttpPatch("appointments/{id}/start-consultation")]
        public async Task<IActionResult> StartConsultation(string id)
        {
            try
            {
                var appointment = await _appointmentService.GetByIdAsync(id);
                if (appointment == null) return NotFound();

                // Check if another patient is already IN_CONSULTATION FOR THIS DOCTOR
                var allAppointments = await _appointmentService.GetTodayAppointmentsAsync();
                var inConsultation = allAppointments.FirstOrDefault(a => 
                    a.Status == "IN_CONSULTATION" && 
                    a.DoctorName == appointment.DoctorName);
                
                if (inConsultation != null && inConsultation.Id != id)
                {
                    return Ok(new { success = false, message = $"Another patient ({inConsultation.PatientName}) is already in consultation with {appointment.DoctorName}. Please complete their appointment first." });
                }
                
                await _appointmentService.UpdateStatusAsync(id, "IN_CONSULTATION");
                return Ok(new { success = true, message = "Consultation started" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to start consultation: {ex.Message}" });
            }
        }

        [HttpPatch("appointments/{id}/complete")]
        public async Task<IActionResult> CompleteAppointment(string id)
        {
            try
            {
                var appointment = await _appointmentService.GetByIdAsync(id);
                if (appointment == null) return NotFound();

                await _appointmentService.UpdateStatusAsync(id, "COMPLETED");
                
                // Auto-move next patient to NOW SERVING (for this specific doctor)
                var queue = await _queueService.GetCurrentQueueAsync(appointment.DoctorName);
                if (queue.Any())
                {
                    var nextPatient = queue.OrderBy(q => q.Position).First();
                    await _appointmentService.UpdateStatusAsync(nextPatient.AppointmentId, "IN_CONSULTATION");
                }
                
                return Ok(new { success = true, message = "Appointment completed and next patient moved to NOW SERVING" });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Failed to complete appointment: {ex.Message}" });
            }
        }

        [HttpGet("slots")]
        public async Task<IActionResult> GetDashboardSlots(
            [FromQuery] DateTime? date,
            [FromQuery] string? specialty,
            [FromQuery] string? doctorName)
        {
            var slots = await _appointmentService.GetDashboardSlotsAsync(date, specialty, doctorName);
            return Ok(slots);
        }

        [HttpGet("queue/live")]
        public async Task<IActionResult> GetLiveQueue([FromQuery] string? doctorName)
        {
            var queue = await _queueService.GetCurrentQueueWithPatientsAsync(doctorName);
            return Ok(queue);
        }
    }
}
