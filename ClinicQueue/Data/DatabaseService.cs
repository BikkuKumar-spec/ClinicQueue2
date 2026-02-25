using MySqlConnector;
using Dapper;

namespace ClinicQueue.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
            CreateDatabaseIfNotExists();
            InitializeDatabase();
        }

        public MySqlConnection GetConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private void CreateDatabaseIfNotExists()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.Database;
            builder.Database = null; // connect without database to create it

            using var connection = new MySqlConnection(builder.ToString());
            connection.Open();
            connection.Execute($"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();
            
            // Create Patients table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS patients (
                    id VARCHAR(50) PRIMARY KEY,
                    name VARCHAR(255) NOT NULL,
                    phone_number VARCHAR(20) UNIQUE NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    last_visit DATETIME
                )
            ");

            // Create Specialties table first since Doctors depends on it
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS specialties (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) UNIQUE NOT NULL,
                    description TEXT,
                    is_active BOOLEAN DEFAULT TRUE
                )
            ");

            // Create Doctors table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS doctors (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) UNIQUE NOT NULL,
                    specialty_id INT,
                    description TEXT,
                    is_active BOOLEAN DEFAULT TRUE,
                    status INT DEFAULT 0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    FOREIGN KEY (specialty_id) REFERENCES specialties(id)
                )
            ");

            // Create Schedules table for Doctors
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS schedules (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    doctor_id INT NOT NULL,
                    day_of_week INT NOT NULL,
                    start_time TIME NOT NULL,
                    end_time TIME NOT NULL,
                    slot_duration_minutes INT DEFAULT 30,
                    max_patients_per_slot INT DEFAULT 5,
                    FOREIGN KEY (doctor_id) REFERENCES doctors(id),
                    UNIQUE KEY unique_doc_day (doctor_id, day_of_week)
                )
            ");

            // Create Bookings/Appointments table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS appointments (
                    id VARCHAR(50) PRIMARY KEY,
                    patient_id VARCHAR(50) NOT NULL,
                    slot_time DATETIME NOT NULL,
                    status ENUM('BOOKED', 'ARRIVED', 'IN_QUEUE', 'IN_CONSULTATION', 'COMPLETED', 'CANCELLED', 'NO_SHOW') DEFAULT 'BOOKED',
                    patient_name VARCHAR(255),
                    doctor_name VARCHAR(100),
                    specialty VARCHAR(100),
                    queue_position INT,
                    reminder_sent BOOLEAN DEFAULT FALSE,
                    expected_to_arrive BOOLEAN DEFAULT FALSE,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    FOREIGN KEY (patient_id) REFERENCES patients(id),
                    INDEX idx_appointments_slot (slot_time),
                    INDEX idx_appointments_status (status)
                )
            ");



            // Create Queue table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS queue (
                    id VARCHAR(50) PRIMARY KEY,
                    appointment_id VARCHAR(50) UNIQUE NOT NULL,
                    position INT NOT NULL,
                    priority_score INT NOT NULL,
                    status VARCHAR(50) DEFAULT 'IN_QUEUE',
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id),
                    INDEX idx_queue_priority (priority_score)
                )
            ");



            // Create Notification Log table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS notification_log (
                    id VARCHAR(50) PRIMARY KEY,
                    appointment_id VARCHAR(50),
                    phone VARCHAR(20) NOT NULL,
                    message TEXT NOT NULL,
                    template VARCHAR(100),
                    status VARCHAR(50) DEFAULT 'pending',
                    sent_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");

            // Create Queue Position History table (for notification tracking)
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS queue_position_history (
                    appointment_id VARCHAR(50) PRIMARY KEY,
                    last_notified_position INT NOT NULL,
                    last_notification_time DATETIME NOT NULL,
                    next_in_queue_notified BOOLEAN DEFAULT FALSE,
                    arrival_reminder_sent BOOLEAN DEFAULT FALSE,
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");

            // Create Symptom Analyses table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS symptom_analyses (
                    id VARCHAR(50) PRIMARY KEY,
                    patient_id VARCHAR(50) NOT NULL,
                    appointment_id VARCHAR(50),
                    original_symptoms TEXT NOT NULL,
                    translated_symptoms TEXT,
                    recommended_specialty VARCHAR(100) NOT NULL,
                    severity VARCHAR(50) NOT NULL,
                    ai_reasoning TEXT,
                    detected_language VARCHAR(50) NOT NULL,
                    intent VARCHAR(50) DEFAULT 'Other',
                    normalized_english TEXT,
                    confidence DOUBLE DEFAULT 0.0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (patient_id) REFERENCES patients(id),
                    FOREIGN KEY (appointment_id) REFERENCES appointments(id)
                )
            ");
            
            // Run migrations
            RunMigrations(connection);
        }

        private void RunMigrations(MySqlConnection connection)
        {
            // Seed default specialties (IGNORE to be idempotent)
            connection.Execute(@"
                INSERT IGNORE INTO specialties (name, description) VALUES
                    ('General Physician',  'General medicine and common illnesses'),
                    ('Dermatologist',      'Skin, hair and nail conditions'),
                    ('Pediatrician',       'Child health specialist'),
                    ('Orthopedist',        'Bone and joint specialist'),
                    ('ENT Specialist',     'Ear, Nose and Throat specialist'),
                    ('Ophthalmologist',    'Eye specialist'),
                    ('Cardiologist',       'Heart specialist'),
                    ('Pulmonologist',      'Lung specialist')
            ");

            var gpDoctorCount = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM doctors d
                JOIN specialties s ON d.specialty_id = s.id
                WHERE s.name = 'General Physician' AND d.is_active = 1
            ");

            if (gpDoctorCount == 0)
            {
                Console.WriteLine("[DB] Seeding General Physician doctors: Dr. Sharma, Dr. Verma");
                connection.Execute(@"
                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active) 
                    SELECT 'Dr. Sharma', s.id, 'Senior General Physician', 1
                    FROM specialties s WHERE s.name = 'General Physician';

                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT 'Dr. Verma', s.id, 'General Practitioner', 1
                    FROM specialties s WHERE s.name = 'General Physician';
                ");
            }

            SeedSpecialtyDoctors(connection, "Dermatologist",
                ("Dr. Mehta", "Senior Dermatologist"),
                ("Dr. Ajay",  "Skin & Hair Specialist"));

            SeedSpecialtyDoctors(connection, "Pediatrician",
                ("Dr. Gupta", "Child Health Specialist"),
                ("Dr. Kavya", "Senior Pediatrician"));

            SeedSpecialtyDoctors(connection, "Orthopedist",
                ("Dr. Rao",   "Bone & Joint Surgeon"),
                ("Dr. Kumar", "Sports Medicine"));

            SeedSpecialtyDoctors(connection, "ENT Specialist",
                ("Dr. Singh", "ENT Surgeon"),
                ("Dr. Anil",  "Ear, Nose & Throat"));
                
            SeedSpecialtyDoctors(connection, "Ophthalmologist",
                ("Dr. Kapoor", "Eye Surgeon"),
                ("Dr. Joshi", "Vision Specialist"));
                
            SeedSpecialtyDoctors(connection, "Cardiologist",
                ("Dr. Desai", "Heart Specialist"),
                ("Dr. Patil", "Cardiovascular Surgeon"));

            SeedSpecialtyDoctors(connection, "Pulmonologist",
                ("Dr. Iyer", "Lung Specialist"),
                ("Dr. Nair", "Respiratory Specialist"));
                
            SeedDoctorSchedules(connection);
            SeedDummyBookings(connection);
        }

        private void SeedSpecialtyDoctors(
            MySqlConnection connection,
            string specialtyName,
            (string Name, string Description) doc1,
            (string Name, string Description) doc2)
        {
            var count = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM doctors d
                JOIN specialties s ON d.specialty_id = s.id
                WHERE s.name = @name AND d.is_active = 1", new { name = specialtyName });

            if (count == 0 || count == 1)
            {
                Console.WriteLine($"[DB] Seeding {specialtyName} doctors: {doc1.Name}, {doc2.Name}");
                connection.Execute(@"
                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name1, s.id, @desc1, 1 FROM specialties s WHERE s.name = @spec;
                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name2, s.id, @desc2, 1 FROM specialties s WHERE s.name = @spec;",
                    new { name1 = doc1.Name, desc1 = doc1.Description,
                          name2 = doc2.Name, desc2 = doc2.Description,
                          spec  = specialtyName });
            }
        }
        
        private void SeedDoctorSchedules(MySqlConnection connection)
        {
            var scheduledCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM schedules");
            if (scheduledCount == 0)
            {
                Console.WriteLine("[DB] Seeding schedules for ALL doctors (Mon-Sat, 9AM-6PM)");
                // Create a schedule for each existing doctor for Mon-Sat (1 to 6)
                connection.Execute(@"
                    INSERT IGNORE INTO schedules (doctor_id, day_of_week, start_time, end_time, slot_duration_minutes, max_patients_per_slot)
                    SELECT id, d.day_of_week, '09:00:00', '18:00:00', 30, 5
                    FROM doctors
                    CROSS JOIN (
                        SELECT 1 as day_of_week UNION ALL
                        SELECT 2 UNION ALL
                        SELECT 3 UNION ALL
                        SELECT 4 UNION ALL
                        SELECT 5 UNION ALL
                        SELECT 6
                    ) d
                ");
            }
        }

        private void SeedDummyBookings(MySqlConnection connection)
        {
            var patientCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM patients");
            if (patientCount == 0)
            {
                Console.WriteLine("[DB] Seeding dummy patients and appointments for testing...");
                
                // 1. Insert dummy patients
                var p1Id = Guid.NewGuid().ToString();
                var p2Id = Guid.NewGuid().ToString();
                
                connection.Execute(@"
                    INSERT IGNORE INTO patients (id, name, phone_number) VALUES 
                    (@Id1, 'Rahul Kumar', '919876543210'),
                    (@Id2, 'Priya Singh', '919876543211')",
                    new { Id1 = p1Id, Id2 = p2Id });

                // 2. Find Dr. Sharma
                var sharmaId = connection.ExecuteScalar<int?>("SELECT id FROM doctors WHERE name = 'Dr. Sharma' LIMIT 1");
                var vermaId = connection.ExecuteScalar<int?>("SELECT id FROM doctors WHERE name = 'Dr. Verma' LIMIT 1");

                if (sharmaId.HasValue)
                {
                    // Create appointments for today, 10 AM and 11 AM
                    var today = DateTime.Today;
                    var t1 = today.AddHours(10);
                    var t2 = today.AddHours(11);
                    
                    var apt1 = Guid.NewGuid().ToString();
                    var apt2 = Guid.NewGuid().ToString();

                    connection.Execute(@"
                        INSERT IGNORE INTO appointments (id, patient_id, slot_time, status, patient_name, doctor_name, specialty)
                        VALUES 
                        (@A1, @P1, @T1, 'BOOKED', 'Rahul Kumar', 'Dr. Sharma', 'General Physician'),
                        (@A2, @P2, @T2, 'ARRIVED', 'Priya Singh', 'Dr. Sharma', 'General Physician')",
                        new { A1 = apt1, P1 = p1Id, T1 = t1, A2 = apt2, P2 = p2Id, T2 = t2 });

                    // Add to queue for Priya (status ARRIVED -> IN_QUEUE usually)
                    connection.Execute(@"
                        UPDATE appointments SET status = 'IN_QUEUE' WHERE id = @A2;
                        INSERT IGNORE INTO queue (id, appointment_id, position, priority_score, status)
                        VALUES (@Qid, @A2, 1, 100, 'IN_QUEUE')",
                        new { A2 = apt2, Qid = Guid.NewGuid().ToString() });
                }
            }
        }
    }
}
