using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicQueue.Shared.DTOs;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            // Simple hardcoded auth for MVP (replace with database check)
            if (request.Username != "assistant" || request.Password != "clinic123")
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var token = GenerateJwtToken(request.Username, "AssistantRole");
            return Ok(new { token, username = request.Username });
        }

        private string GenerateJwtToken(string username, string role)
        {
            var jwtSecret = _configuration["Jwt:Secret"] ?? "YourSuperSecretKey32CharactersLong!";
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "ClinicQueueSystem",
                audience: "ClinicDashboard",
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
