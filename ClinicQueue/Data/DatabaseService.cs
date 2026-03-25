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
            builder.Database = null;

            using var connection = new MySqlConnection(builder.ToString());
            connection.Open();
            connection.Execute($"CREATE DATABASE IF NOT EXISTS `{databaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
        }

        private void InitializeDatabase()
        {
            using var connection = GetConnection();

            connection.Execute(@"CREATE TABLE IF NOT EXISTS patients (
                id VARCHAR(50) PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                phone_number VARCHAR(20) UNIQUE NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                last_visit DATETIME)");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS specialties (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(100) UNIQUE NOT NULL,
                description TEXT,
                is_active BOOLEAN DEFAULT TRUE)");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS doctors (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(100) UNIQUE NOT NULL,
                specialty_id INT,
                description TEXT,
                is_active BOOLEAN DEFAULT TRUE,
                status INT DEFAULT 0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (specialty_id) REFERENCES specialties(id))");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS schedules (
                id INT AUTO_INCREMENT PRIMARY KEY,
                doctor_id INT NOT NULL,
                day_of_week INT NOT NULL,
                start_time TIME NOT NULL,
                end_time TIME NOT NULL,
                slot_duration_minutes INT DEFAULT 30,
                max_patients_per_slot INT DEFAULT 5,
                FOREIGN KEY (doctor_id) REFERENCES doctors(id),
                UNIQUE KEY unique_doc_day (doctor_id, day_of_week))");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS appointments (
                id VARCHAR(50) PRIMARY KEY,
                patient_id VARCHAR(50) NOT NULL,
                slot_time DATETIME NOT NULL,
                status ENUM('BOOKED','ARRIVED','IN_QUEUE','IN_CONSULTATION','COMPLETED','CANCELLED','NO_SHOW') DEFAULT 'BOOKED',
                patient_name VARCHAR(255),
                doctor_name VARCHAR(100),
                specialty VARCHAR(100),
                queue_position INT,
                reminder_sent BOOLEAN DEFAULT FALSE,
                expected_to_arrive BOOLEAN DEFAULT FALSE,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (patient_id) REFERENCES patients(id))");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS queue (
                id VARCHAR(50) PRIMARY KEY,
                appointment_id VARCHAR(50) UNIQUE NOT NULL,
                position INT NOT NULL,
                priority_score INT NOT NULL,
                status VARCHAR(50) DEFAULT 'IN_QUEUE',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (appointment_id) REFERENCES appointments(id))");

            connection.Execute(@"CREATE TABLE IF NOT EXISTS notification_log (
                id VARCHAR(50) PRIMARY KEY,
                appointment_id VARCHAR(50),
                phone VARCHAR(20) NOT NULL,
                message TEXT NOT NULL,
                template VARCHAR(100),
                status VARCHAR(50) DEFAULT 'pending',
                sent_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (appointment_id) REFERENCES appointments(id))");

            RunMigrations(connection);
        }

        private void RunMigrations(MySqlConnection connection)
        {
            connection.Execute(@"INSERT IGNORE INTO specialties (name, description) VALUES
                ('General Physician','General medicine'),
                ('Dermatologist','Skin specialist'),
                ('Pediatrician','Child specialist'),
                ('Orthopedist','Bone specialist'),
                ('ENT Specialist','ENT specialist'),
                ('Ophthalmologist','Eye specialist'),
                ('Cardiologist','Heart specialist'),
                ('Pulmonologist','Lung specialist')");

            SeedSpecialtyDoctors(connection, "Dermatologist",
                "Dr. Mehta", "Senior Dermatologist",
                "Dr. Ajay", "Skin & Hair Specialist");

            SeedSpecialtyDoctors(connection, "Pediatrician",
                "Dr. Gupta", "Child Health Specialist",
                "Dr. Kavya", "Senior Pediatrician");

            SeedSpecialtyDoctors(connection, "Orthopedist",
                "Dr. Rao", "Bone & Joint Surgeon",
                "Dr. Kumar", "Sports Medicine");

            SeedSpecialtyDoctors(connection, "ENT Specialist",
                "Dr. Singh", "ENT Surgeon",
                "Dr. Anil", "Ear, Nose & Throat");

            SeedSpecialtyDoctors(connection, "Ophthalmologist",
                "Dr. Kapoor", "Eye Surgeon",
                "Dr. Joshi", "Vision Specialist");

            SeedSpecialtyDoctors(connection, "Cardiologist",
                "Dr. Desai", "Heart Specialist",
                "Dr. Patil", "Cardiovascular Surgeon");

            SeedSpecialtyDoctors(connection, "Pulmonologist",
                "Dr. Iyer", "Lung Specialist",
                "Dr. Nair", "Respiratory Specialist");
        }

        private void SeedSpecialtyDoctors(
            MySqlConnection connection,
            string specialtyName,
            string name1,
            string desc1,
            string name2,
            string desc2)
        {
            var count = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM doctors d
                JOIN specialties s ON d.specialty_id = s.id
                WHERE s.name = @name",
                new { name = specialtyName });

            if (count <= 1)
            {
                connection.Execute(@"
                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name1, s.id, @desc1, 1 FROM specialties s WHERE s.name = @spec;

                    INSERT IGNORE INTO doctors (name, specialty_id, description, is_active)
                    SELECT @name2, s.id, @desc2, 1 FROM specialties s WHERE s.name = @spec;",
                    new
                    {
                        name1,
                        desc1,
                        name2,
                        desc2,
                        spec = specialtyName
                    });
            }
        }
    }
}