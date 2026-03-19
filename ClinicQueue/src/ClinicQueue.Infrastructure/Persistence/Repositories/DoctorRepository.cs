using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence.Repositories;

public class DoctorRepository(ClinicDbContext dbContext)
    : Repository<Doctor>(dbContext), IDoctorRepository
{
    public async Task<string?> FindBestSpecialtyMatchAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var specialties = await GetActiveSpecialtiesAsync(cancellationToken);
        if (specialties.Count == 0)
        {
            return null;
        }

        var normalizedInput = NormalizeLookup(input);
        var exact = specialties.FirstOrDefault(x => NormalizeLookup(x) == normalizedInput);
        if (!string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var contains = specialties.FirstOrDefault(x => NormalizeLookup(x).Contains(normalizedInput, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(contains))
        {
            return contains;
        }

        var best = specialties
            .Select(x => new { Specialty = x, Distance = LevenshteinDistance(NormalizeLookup(x), normalizedInput) })
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        if (best is null)
        {
            return null;
        }

        return best.Distance <= 3 ? best.Specialty : null;
    }

    public async Task<Doctor?> GetFirstAvailableDoctorAsync(string specialty, CancellationToken cancellationToken = default)
    {
        var doctors = await GetAvailableBySpecialtyAsync(specialty, cancellationToken);
        return doctors.OrderBy(x => x.Name.Value).FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<string>> GetActiveSpecialtiesAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.IsAvailable && !string.IsNullOrWhiteSpace(x.Specialty))
            .Select(x => x.Specialty)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Doctor>> GetAvailableBySpecialtyAsync(string specialty, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            return [];
        }

        var normalizedSpecialty = specialty.Trim();
        var doctors = await DbSet
            .AsNoTracking()
            .Where(x => x.IsAvailable && x.Specialty.ToLower() == normalizedSpecialty.ToLower())
            .ToListAsync(cancellationToken);

        return doctors
            .OrderBy(x => x.Name.Value)
            .ToList();
    }

    public async Task<Doctor?> FindAvailableDoctorByNameAsync(string doctorName, string? specialty = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorName))
        {
            return null;
        }

        var normalizedName = doctorName.Trim();
        var query = DbSet.AsNoTracking().Where(x => x.IsAvailable);

        if (!string.IsNullOrWhiteSpace(specialty))
        {
            var normalizedSpecialty = specialty.Trim();
            query = query.Where(x => x.Specialty.ToLower() == normalizedSpecialty.ToLower());
        }

        var doctors = await query.ToListAsync(cancellationToken);

        var exact = doctors
            .FirstOrDefault(x => string.Equals(x.Name.Value, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        var partial = doctors
            .Where(x => x.Name.Value.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name.Value)
            .FirstOrDefault();

        if (partial is not null)
        {
            return partial;
        }

        var nameToken = ExtractDoctorNameToken(normalizedName);
        if (string.IsNullOrWhiteSpace(nameToken))
        {
            return null;
        }

        return doctors
            .Where(x => x.Name.Value.Contains(nameToken, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name.Value)
            .FirstOrDefault();
    }

    private static string ExtractDoctorNameToken(string rawDoctorName)
    {
        if (string.IsNullOrWhiteSpace(rawDoctorName))
        {
            return string.Empty;
        }

        var cleaned = rawDoctorName.Replace("Dr.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Doctor", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var pieces = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0)
        {
            return string.Empty;
        }

        return pieces[^1];
    }

    private static string NormalizeLookup(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var chars = input
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return new string(chars);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var matrix = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }
}
