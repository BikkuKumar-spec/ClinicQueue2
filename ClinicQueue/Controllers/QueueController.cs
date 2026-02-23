using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/queue")]
    public class QueueController : ControllerBase
    {
        private readonly IQueueService _queueService;

        public QueueController(IQueueService queueService)
        {
            _queueService = queueService;
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<IActionResult> AddToQueue([FromBody] AddToQueueRequest request)
        {
            try
            {
                await _queueService.AddToQueueAsync(request.AppointmentId);
                return Ok(new { message = "Added to queue successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{appointmentId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromQueue(string appointmentId)
        {
            await _queueService.RemoveFromQueueAsync(appointmentId);
            return Ok(new { message = "Removed from queue" });
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentQueue()
        {
            var queue = await _queueService.GetCurrentQueueAsync();
            return Ok(queue);
        }

        [HttpPost("reorder")]
        [Authorize]
        public async Task<IActionResult> TriggerReorder()
        {
            await _queueService.ReorderQueueAsync();
            return Ok(new { message = "Queue reordered" });
        }

        [HttpGet("position/{appointmentId}")]
        public async Task<IActionResult> GetPosition(string appointmentId)
        {
            var position = await _queueService.GetPositionAsync(appointmentId);
            return Ok(position);
        }
    }
}
