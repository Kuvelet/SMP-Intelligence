// Each call owns a fresh in-memory database. No database file or connection is shared.
export function createCatalog(SQL, fixture) {
  return {
    execute(sql) {
      const db = new SQL.Database();
      let statement;
      try {
        db.run(`CREATE TABLE Interchange (
          [Part Number] TEXT PRIMARY KEY, [Interchange Name] TEXT NOT NULL, Price REAL NOT NULL, Note TEXT);
          CREATE TABLE ApplicationViO (
          Part TEXT NOT NULL, Make TEXT NOT NULL, Model TEXT NOT NULL, Year INTEGER NOT NULL, VIO INTEGER NOT NULL,
          PRIMARY KEY (Part, Make, Model, Year));`);
        db.run('BEGIN');
        for (const row of fixture.interchange) db.run('INSERT INTO Interchange VALUES (?, ?, ?, ?)',
          [row.partNumber, row.reference, row.price, row.note]);
        for (const row of fixture.applications) db.run('INSERT INTO ApplicationViO VALUES (?, ?, ?, ?, ?)',
          [row.part, row.make, row.model, row.year, row.vio]);
        db.run('COMMIT; PRAGMA query_only = ON;');
        statement = db.prepare(sql);
        const columns = statement.getColumnNames();
        const rows = [];
        let truncated = false;
        while (statement.step()) {
          if (rows.length === 100) { truncated = true; break; }
          rows.push(statement.get());
        }
        return JSON.stringify({columns, rows, truncated});
      } finally {
        statement?.free();
        db.close();
      }
    }
  };
}
