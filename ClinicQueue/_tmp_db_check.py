import os
import sqlite3

DBS = [
    r"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\clinic_queue.db",
    r"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\src\ClinicQueue.Api\clinic_queue.db",
    r"C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue\Data Source=clinic_queue.db",
]

for p in DBS:
    print(f"DB={p}")
    print(f"EXISTS={os.path.exists(p)}")
    if not os.path.exists(p):
        print("---")
        continue

    con = sqlite3.connect(p)
    cur = con.cursor()

    cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
    tables = [r[0] for r in cur.fetchall()]
    print(f"TABLE_COUNT={len(tables)}")
    print("TABLES=" + "|".join(tables))

    for table in ("specialties", "doctors", "Doctors"):
        cur.execute("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", (table,))
        exists = cur.fetchone()[0] > 0
        print(f"TABLE_{table}_EXISTS={exists}")
        if exists:
            cur.execute(f"SELECT COUNT(*) FROM {table}")
            print(f"TABLE_{table}_ROWS={cur.fetchone()[0]}")
            cur.execute(f"SELECT name FROM {table} ORDER BY name LIMIT 50")
            names = [x[0] for x in cur.fetchall() if x and x[0] is not None]
            print(f"NAMES_{table}=" + "|".join(names))

    con.close()
    print("---")
