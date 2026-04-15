using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.Enums;
using ClinicQueue.Domain.ValueObjects;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClinicQueue.Infrastructure.Persistence;

public class ClinicDbContext : IdentityDbContext<ClinicIdentityUser>
{
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<MedicalReport> MedicalReports => Set<MedicalReport>();
    public DbSet<BotSession> BotSessions => Set<BotSession>();
    public DbSet<NotificationReminder> NotificationReminders => Set<NotificationReminder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var personNameConverter = new ValueConverter<PersonName, string>(
            value => value.Value,
            value => new PersonName(value));

        var phoneNumberConverter = new ValueConverter<PhoneNumber, string>(
            value => value.Value,
            value => new PhoneNumber(value));

        modelBuilder.Entity<Patient>(builder =>
        {
            builder.ToTable("Patients");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasConversion(personNameConverter).HasMaxLength(120);
            builder.Property(x => x.PhoneNumber).HasConversion(phoneNumberConverter).HasMaxLength(20);
            builder.Property(x => x.MedicalHistoryReference).HasMaxLength(200);
        });

        modelBuilder.Entity<Doctor>(builder =>
        {
            builder.ToTable("Doctors");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasConversion(personNameConverter).HasMaxLength(120);
            builder.Property(x => x.Specialty).HasMaxLength(120);
            builder.Ignore(x => x.Availability);
        });

        modelBuilder.Entity<Appointment>(builder =>
        {
            builder.ToTable("Appointments");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>();
            builder.HasIndex(x => new { x.DoctorId, x.SlotTime });
            builder.HasIndex(x => x.PatientId);
        });

        modelBuilder.Entity<QueueEntry>(builder =>
        {
            builder.ToTable("QueueEntries");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>();
            builder.HasIndex(x => x.AppointmentId).IsUnique();
            builder.HasIndex(x => x.Position);
        });

        modelBuilder.Entity<MedicalReport>(builder =>
        {
            builder.ToTable("MedicalReports");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasConversion<string>();
            builder.Property(x => x.FileName).HasMaxLength(255);
            builder.Property(x => x.DocumentReference).HasMaxLength(255);
        });

        modelBuilder.Entity<BotSession>(builder =>
        {
            builder.ToTable("BotSessions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ChannelUserId).HasMaxLength(40);
            builder.Property(x => x.State).HasMaxLength(60);
            builder.Property(x => x.LanguageCode).HasMaxLength(20);
            builder.Property(x => x.LastIntent).HasMaxLength(40);
        });

        modelBuilder.Entity<NotificationReminder>(builder =>
        {
            builder.ToTable("NotificationReminders");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasConversion<string>();
            builder.HasIndex(x => new { x.AppointmentId, x.ScheduledFor });
        });
    }
}
