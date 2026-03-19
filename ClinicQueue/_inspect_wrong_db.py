import sqlite3

DB_PATH = r"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\Data Source=clinic_queue.db"
OUT_PATH = r"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\_wrong_db_inventory.txt"

con = sqlite3.connect(f"file:{DB_PATH}?mode=ro", uri=True)
cur = con.cursor()

cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
tables = [row[0] for row in cur.fetchall()]

lines = [f"TABLES: {tables}"]
for table_name in tables:
    quoted_name = '"' + table_name.replace('"', '""') + '"'
    cur.execute(f"SELECT COUNT(*) FROM {quoted_name}")
    lines.append(f"{table_name} rows: {cur.fetchone()[0]}")

with open(OUT_PATH, "w", encoding="utf-8") as file:
    file.write("\n".join(lines))

con.close()
