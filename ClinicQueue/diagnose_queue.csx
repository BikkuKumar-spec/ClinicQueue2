using System;
using Microsoft.Data.Sqlite;
using Dapper;
using System.Linq;

var connectionString = "Data Source=clinic_queue.db";
using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("--- Recent Appointments ---");
var appointments = connection.Query(@"
    SELECT id, patient_name, status, slot_time 
    FROM appointments 
    ORDER BY created_at DESC 
    LIMIT 5");

foreach (var a in appointments) {
    Console.WriteLine($"ID: {a.id}, Name: {a.patient_name}, Status: {a.status}, Time: {a.slot_time}");
}

Console.WriteLine("\n--- Live Queue Table ---");
var queueEntries = connection.Query(@"
    SELECT q.appointment_id, q.status, q.position, a.patient_name
    FROM queue q
    JOIN appointments a ON q.appointment_id = a.id");

foreach (var q in queueEntries) {
    Console.WriteLine($"ApptID: {q.appointment_id}, Name: {q.patient_name}, Status: {q.status}, Pos: {q.position}");
}
