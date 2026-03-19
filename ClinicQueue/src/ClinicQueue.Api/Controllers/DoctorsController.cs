using System.Collections.Concurrent;
using ClinicQueue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/doctors")]
public class DoctorsController(ClinicDbContext dbContext) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, int> StatusOverrides =
        new(StringComparer.OrdinalIgnoreCase);

    [HttpGet("{doctorName}/status")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetStatus(string doctorName, CancellationToken cancellationToken)
    {
        if (StatusOverrides.TryGetValue(doctorName, out var overriddenStatus))
        {
            return Ok(overriddenStatus);
        }

        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var doctor = doctors.FirstOrDefault(x =>
            string.Equals(x.Name.Value, doctorName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Id, doctorName, StringComparison.OrdinalIgnoreCase));

        if (doctor is null)
        {
            return Ok(0);
        }

        return Ok(doctor.IsAvailable ? 0 : 1);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(CancellationToken cancellationToken)
    {
        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Where(x => x.IsAvailable)
            .OrderBy(x => x.Specialty)
            .ToListAsync(cancellationToken);

        var response = doctors
            .OrderBy(x => x.Specialty)
            .ThenBy(x => x.Name.Value)
            .Select(x => new
            {
                id = x.Id,
                name = x.Name.Value,
                specialty = x.Specialty,
                isAvailable = x.IsAvailable
            })
            .ToList();

        return Ok(response);
    }

    [HttpGet("specialties")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpecialties(CancellationToken cancellationToken)
    {
        var specialties = await dbContext.Doctors
            .AsNoTracking()
            .Where(x => x.IsAvailable && !string.IsNullOrWhiteSpace(x.Specialty))
            .Select(x => x.Specialty)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        return Ok(specialties);
    }

    [HttpPut("{doctorName}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(string doctorName, [FromBody] int status, CancellationToken cancellationToken)
    {
        var normalizedStatus = status switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            _ => 2
        };

        StatusOverrides[doctorName] = normalizedStatus;

        var doctors = await dbContext.Doctors.ToListAsync(cancellationToken);

        var doctor = doctors.FirstOrDefault(x =>
            string.Equals(x.Name.Value, doctorName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Id, doctorName, StringComparison.OrdinalIgnoreCase));

        if (doctor is not null)
        {
            doctor.SetAvailability(normalizedStatus == 0);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { success = true, status = normalizedStatus });
    }
}
