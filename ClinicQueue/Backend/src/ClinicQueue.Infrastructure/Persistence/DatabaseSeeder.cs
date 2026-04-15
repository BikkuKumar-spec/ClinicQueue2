using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly ClinicDbContext _dbContext;

    private static readonly string[] LegacySpecialties =
    [
        "General Physician",
        "Dermatologist",
        "Pediatrician",
        "Orthopedist",
        "ENT Specialist"
    ];

    private static readonly LegacyDoctorSeed[] LegacyDoctors =
    [
        new("Dr. Suresh", "General Physician", "Senior General Physician", true, 0),
        new("Dr. Mukesh", "General Physician", "General Practitioner", true, 0),
        new("Dr. Akash", "Dermatologist", "Senior Dermatologist", true, 0),
        new("Dr. Ajay", "Dermatologist", "Skin & Hair Specialist", true, 0),
        new("Dr. Beena", "Pediatrician", "Child Health Specialist", true, 0),
        new("Dr. Kavya", "Pediatrician", "Senior Pediatrician", true, 0),
        new("Dr. Raj", "Orthopedist", "Bone & Joint Surgeon", true, 0),
        new("Dr. Kumar", "Orthopedist", "Sports Medicine Specialist", true, 0),
        new("Dr. Priya", "ENT Specialist", "ENT Surgeon", true, 0),
        new("Dr. Anil", "ENT Specialist", "Ear, Nose & Throat Specialist", true, 0)
    ];

    public DatabaseSeeder(ClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        var existingDoctorNames = await _dbContext.Doctors
            .AsNoTracking()
            .Select(x => x.Name.Value)
            .ToListAsync(cancellationToken);

        var existingNameSet = new HashSet<string>(existingDoctorNames, StringComparer.OrdinalIgnoreCase);

        var doctorsToInsert = LegacyDoctors
            .Where(seed => LegacySpecialties.Contains(seed.Specialty, StringComparer.OrdinalIgnoreCase))
            .Where(seed => !existingNameSet.Contains(seed.Name))
            .Select(seed =>
            {
                var doctor = Doctor.Create(new PersonName(seed.Name), seed.Specialty);

                // Legacy SQL seed used is_active = 1 and status = 0 for all doctors.
                doctor.SetAvailability(seed.IsActive);

                return doctor;
            })
            .ToList();

        if (doctorsToInsert.Count == 0)
        {
            return;
        }

        await _dbContext.Doctors.AddRangeAsync(doctorsToInsert, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record LegacyDoctorSeed(
        string Name,
        string Specialty,
        string Description,
        bool IsActive,
        int Status);
}