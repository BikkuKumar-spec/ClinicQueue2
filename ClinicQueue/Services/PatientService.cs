using ClinicQueue.Data;
using ClinicQueue.Shared.Models;
using Dapper;
using ClinicQueue.Shared.Utilities;

namespace ClinicQueue.Services
{
    public interface IPatientService
    {
        Task<Patient?> GetByIdAsync(string id);
        Task<Patient?> GetByPhoneAsync(string phoneNumber);
        Task<Patient> GetOrCreateByPhoneAsync(string name, string phoneNumber);
        Task<List<Appointment>> GetHistoryAsync(string patientId);
    }

    public class PatientService : IPatientService
    {
        private readonly DatabaseService _db;

        public PatientService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<Patient?> GetByIdAsync(string id)
        {
            using var connection = _db.GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Patient>(
                "SELECT * FROM patients WHERE id = @Id",
                new { Id = id }
            );
        }

        public async Task<Patient?> GetByPhoneAsync(string phoneNumber)
        {
            using var connection = _db.GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Patient>(
                "SELECT * FROM patients WHERE phone_number = @Phone",
                new { Phone = phoneNumber }
            );
        }

        public async Task<Patient> GetOrCreateByPhoneAsync(string name, string phoneNumber)
        {
            var existing = await GetByPhoneAsync(phoneNumber);
            if (existing != null) return existing;

            var patient = new Patient
            {
                Name = name.ToProperCase(),
                PhoneNumber = phoneNumber
            };

            using var connection = _db.GetConnection();
            await connection.ExecuteAsync(@"
                INSERT INTO patients (id, name, phone_number, created_at)
                VALUES (@Id, @Name, @PhoneNumber, @CreatedAt)",
                patient
            );

            return patient;
        }

        public async Task<List<Appointment>> GetHistoryAsync(string patientId)
        {
            using var connection = _db.GetConnection();
            var appointments = await connection.QueryAsync<Appointment>(@"
                SELECT 
                    a.id, 
                    a.patient_id AS PatientId, 
                    COALESCE(NULLIF(a.patient_name, ''), p.name) AS PatientName, 
                    a.doctor_name AS DoctorName, 
                    a.slot_time AS SlotTime, 
                    a.status, 
                    a.specialty, 
                    a.queue_position AS QueuePosition,
                    a.created_at AS CreatedAt,
                    a.updated_at AS UpdatedAt
                FROM appointments a
                JOIN patients p ON a.patient_id = p.id
                WHERE a.patient_id = @PatientId 
                ORDER BY a.slot_time DESC 
                LIMIT 10",
                new { PatientId = patientId }
            );
            return appointments.ToList();
        }
    }
}
