using Microsoft.Data.Sqlite;
using Dapper;

namespace ClinicQueue.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
            InitializeDatabase();
        }

        public SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();
            
            // Create Patients table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS patients (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    phone_number TEXT UNIQUE NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    last_visit DATETIME
                )
            ");

            // Create Appointments table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS appointments (
                    id TEXT PRIMARY KEY,
                    patient_id TEXT NOT NULL,
                    slot_time DATETIME NOT NULL,
                    status TEXT NOT NULL DEFAULT 'BOOKED',
                    patient_name TEXT,
                    doctor_name TEXT,
                    specialty TEXT,
                    queue_position INTEGER,
                    reminder_sent INTEGER DEFAULT 0,
                    expected_to_arrive INTEGER DEFAULT 0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (patient_id) REFERENCES patients(id)
                )
            ");

            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_appointments_slot ON appointments(slot_time)
            ");

            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_appointments_status ON appointments(status)
            ");

            // Create Queue table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS queue (
                    id TEXT PRIMARY KEY,
                    appointment_id TEXT UNIQUE NOT NULL,
                    position INTEGER NOT NULL,
                    priority_score INTEGER NOT NULL,
                    status TEXT DEFAULT 'IN_QUEUE',
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");

            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_queue_priority ON queue(priority_score)
            ");

            // Create Notification Log table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS notification_log (
                    id TEXT PRIMARY KEY,
                    appointment_id TEXT,
                    phone TEXT NOT NULL,
                    message TEXT NOT NULL,
                    template TEXT,
                    status TEXT DEFAULT 'pending',
                    sent_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");

            // Create Queue Position History table (for notification tracking)
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS queue_position_history (
                    appointment_id TEXT PRIMARY KEY,
                    last_notified_position INTEGER NOT NULL,
                    last_notification_time DATETIME NOT NULL,
                    next_in_queue_notified INTEGER DEFAULT 0,
                    arrival_reminder_sent INTEGER DEFAULT 0,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");

            // Create Symptom Analyses table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS symptom_analyses (
                    id TEXT PRIMARY KEY,
                    patient_id TEXT NOT NULL,
                    appointment_id TEXT,
                    original_symptoms TEXT NOT NULL,
                    translated_symptoms TEXT,
                    recommended_specialty TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    ai_reasoning TEXT,
                    detected_language TEXT NOT NULL,
                    intent TEXT DEFAULT 'Other',
                    normalized_english TEXT,
                    confidence REAL DEFAULT 0.0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (patient_id) REFERENCES patients(id),
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");
            
            // Run migrations
            RunMigrations(connection);
        }
        
        private void RunMigrations(SqliteConnection connection)
        {
            // Migration 1: Add PatientName, ReminderSent, ExpectedToArrive, DoctorName to appointments
            AddColumnIfNotExists(connection, "appointments", "patient_name", "TEXT");
            AddColumnIfNotExists(connection, "appointments", "reminder_sent", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "appointments", "expected_to_arrive", "INTEGER DEFAULT 0");
            AddColumnIfNotExists(connection, "appointments", "doctor_name", "TEXT");
            AddColumnIfNotExists(connection, "appointments", "specialty", "TEXT");
            
            // Create index for reminder queries
            // Create index for reminder queries
            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_appointments_reminder 
                ON appointments(slot_time, status, reminder_sent) 
                WHERE status = 'BOOKED' AND reminder_sent = 0
            ");
            
            // Migration 2: Add intent, normalized_english, confidence to symptom_analyses
            AddColumnIfNotExists(connection, "symptom_analyses", "intent", "TEXT DEFAULT 'Other'");
            AddColumnIfNotExists(connection, "symptom_analyses", "normalized_english", "TEXT");
            AddColumnIfNotExists(connection, "symptom_analyses", "confidence", "REAL DEFAULT 0.0");

            // Migration 3: Ensure specialties + doctors tables exist (created in earlier migration)
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS specialties (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    description TEXT,
                    is_active INTEGER DEFAULT 1
                )
            ");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS doctors (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL,
                    specialty_id INTEGER,
                    description TEXT,
                    is_active INTEGER DEFAULT 1,
                    status INTEGER DEFAULT 0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (specialty_id) REFERENCES specialties(id)
                )
            ");

            // Migration 4: Seed default specialties (INSERT OR IGNORE = idempotent)
            connection.Execute(@"
                INSERT OR IGNORE INTO specialties (name, description) VALUES
                    ('General Physician',  'General medicine and common illnesses'),
                    ('Dermatologist',      'Skin, hair and nail conditions'),
                    ('Pediatrician',       'Child health specialist'),
                    ('Orthopedist',        'Bone and joint specialist'),
                    ('ENT Specialist',     'Ear, Nose and Throat specialist')
            ");

            // Migration 5: Seed Dr. Suresh and Dr. Mukesh under General Physician
            // Only inserts if NO doctor exists for General Physician — never duplicates
            var gpDoctorCount = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM doctors d
                JOIN specialties s ON d.specialty_id = s.id
                WHERE s.name = 'General Physician' AND d.is_active = 1
            ");

            if (gpDoctorCount == 0)
            {
                Console.WriteLine("[DB] Seeding General Physician doctors: Dr. Suresh, Dr. Mukesh");
                connection.Execute(@"
                    INSERT OR IGNORE INTO doctors (name, specialty_id, description, is_active) 
                    SELECT 'Dr. Suresh', s.id, 'Senior General Physician', 1
                    FROM specialties s WHERE s.name = 'General Physician';

                    INSERT OR IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT 'Dr. Mukesh', s.id, 'General Practitioner', 1
                    FROM specialties s WHERE s.name = 'General Physician';
                ");
            }

            // Migration 6: Seed 2 doctors for each remaining specialty
            // Each block only inserts if that specialty currently has zero doctors
            SeedSpecialtyDoctors(connection, "Dermatologist",
                ("Dr. Akash", "Senior Dermatologist"),
                ("Dr. Ajay",  "Skin & Hair Specialist"));

            SeedSpecialtyDoctors(connection, "Pediatrician",
                ("Dr. Beena", "Child Health Specialist"),
                ("Dr. Kavya", "Senior Pediatrician"));

            SeedSpecialtyDoctors(connection, "Orthopedist",
                ("Dr. Raj",   "Bone & Joint Surgeon"),
                ("Dr. Kumar", "Sports Medicine Specialist"));

            SeedSpecialtyDoctors(connection, "ENT Specialist",
                ("Dr. Priya", "ENT Surgeon"),
                ("Dr. Anil",  "Ear, Nose & Throat Specialist"));
        }

        /// <summary>
        /// Idempotent helper — seeds exactly two doctors under <paramref name="specialtyName"/>
        /// only when that specialty currently has zero active doctors.
        /// </summary>
        private void SeedSpecialtyDoctors(
            SqliteConnection connection,
            string specialtyName,
            (string Name, string Description) doc1,
            (string Name, string Description) doc2)
        {
            var count = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM doctors d
                JOIN specialties s ON d.specialty_id = s.id
                WHERE s.name = @name AND d.is_active = 1", new { name = specialtyName });

            if (count == 0)
            {
                Console.WriteLine($"[DB] Seeding {specialtyName} doctors: {doc1.Name}, {doc2.Name}");
                connection.Execute(@"
                    INSERT OR IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name1, s.id, @desc1, 1 FROM specialties s WHERE s.name = @spec;
                    INSERT OR IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name2, s.id, @desc2, 1 FROM specialties s WHERE s.name = @spec;",
                    new { name1 = doc1.Name, desc1 = doc1.Description,
                          name2 = doc2.Name, desc2 = doc2.Description,
                          spec  = specialtyName });
            }
        }
        
        private void AddColumnIfNotExists(SqliteConnection connection, string table, string column, string type)
        {            
            // Reliable way to check columns in SQLite
            var columns = connection.Query<dynamic>($"PRAGMA table_info({table})");
            var columnExists = columns.Any(c => ((string)c.name).Equals(column, StringComparison.OrdinalIgnoreCase));
            
            if (!columnExists)
            {
                Console.WriteLine($"[DB] Migration: Adding column {column} to table {table}");
                connection.Execute($"ALTER TABLE {table} ADD COLUMN {column} {type}");
            }
        }
    }
}
