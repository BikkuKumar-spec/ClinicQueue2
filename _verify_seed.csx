#r "nuget: Microsoft.Data.Sqlite, 8.0.0"
using Microsoft.Data.Sqlite;

var db = @"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\src\ClinicQueue.Api\clinic_queue.db";
using var conn = new SqliteConnection($"Data Source={db}");
conn.Open();

var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM specialties";
var sc = Convert.ToInt32(cmd.ExecuteScalar());
cmd.CommandText = "SELECT COUNT(*) FROM doctors";
var dc = Convert.ToInt32(cmd.ExecuteScalar());

Console.WriteLine($"DB={db}");
Console.WriteLine($"SPEC_COUNT={sc}");
Console.WriteLine($"DOC_COUNT={dc}");

cmd.CommandText = "SELECT name FROM specialties ORDER BY name";
using (var r = cmd.ExecuteReader())
{
    while (r.Read()) Console.WriteLine($"SPECIALTY={r.GetString(0)}");
}

cmd.CommandText = "SELECT name FROM doctors ORDER BY name";
using (var r = cmd.ExecuteReader())
{
    while (r.Read()) Console.WriteLine($"DOCTOR={r.GetString(0)}");
}
