using Microsoft.AspNetCore.Mvc;
using ClinicQueue.Services;
using ClinicQueue.Shared.DTOs;
using System.Threading.Tasks;
using System.Collections.Generic;
using ClinicQueue.Shared.Models;
using System;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SymptomAnalysisController : ControllerBase
    {
        private readonly IAISymptomService _aiService;
        private readonly ILogger<SymptomAnalysisController> _logger;

        public SymptomAnalysisController(IAISymptomService aiService, ILogger<SymptomAnalysisController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] AnalyzeSymptomRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.PatientId))
            {
                return BadRequest("Invalid request.");
            }

            try
            {
                // Use the last message from the history as the current input
                var latestMessage = request.History.LastOrDefault()?.Content ?? "";
                
                // Call the new stateful ChatAsync method
                var aiResponse = await _aiService.ChatAsync(request.PatientId, latestMessage);
                
                // Return the response in a format the frontend expects 
                // (You may need to map AiChatResponse back to AnalyzeSymptomResponse)
                return Ok(aiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Controller error in symptom analysis");
                return StatusCode(500, "An internal error occurred.");
            }
        }
    }
}
