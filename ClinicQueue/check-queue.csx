using Microsoft.Data.Sqlite;
using System;
using System.Linq;

var connectionString = "Data Source=clinic_queue.db";
using var connection = new SqliteConnection(connectionString);
connection.Open();

Console.WriteLine("=== QUEUE TABLE ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT * FROM queue";
    using var reader = cmd.ExecuteReader();
    
    var hasRows = false;
    while (reader.Read())
    {
        hasRows = true;
        Console.WriteLine($"ID: {reader["id"]}");
        Console.WriteLine($"  Appointment ID: {reader["appointment_id"]}");
        Console.WriteLine($"  Position: {reader["position"]}");
        Console.WriteLine($"  Priority Score: {reader["priority_score"]}");
        Console.WriteLine($"  Status: {reader["status"]}");
        Console.WriteLine($"  Created: {reader["created_at"]}");
        Console.WriteLine();
    }
    
    if (!hasRows)
    {
        Console.WriteLine("❌ QUEUE TABLE IS EMPTY");
    }
}

Console.WriteLine("\n=== APPOINTMENTS WITH STATUS ===");
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        SELECT id, patient_id, status, slot_time, queue_position 
        FROM appointments 
        WHERE status IN ('ARRIVED', 'IN_QUEUE', 'IN_CONSULTATION', 'BOOKED')
        ORDER BY slot_time";
    using var reader = cmd.ExecuteReader();
    
    var hasRows = false;
    while (reader.Read())
    {
        hasRows = true;
        var queuePos = reader["queue_position"] != DBNull.Value ? reader["queue_position"].ToString() : "NULL";
        Console.WriteLine($"Status: {reader["status"]} | ID: {reader["id"]} | Queue Pos: {queuePos}");
    }
    
    if (!hasRows)
    {
        Console.WriteLine("❌ NO APPOINTMENTS FOUND");
    }
}
