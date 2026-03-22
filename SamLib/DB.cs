namespace SamLib.DB
{
    using Microsoft.Data.Sqlite;

    // DB Parent
    public abstract class SqlBase : IDisposable
    {
        protected readonly SqliteConnection Conn;
        public event Action OnChange;

        protected void RaiseChange()
        {
            OnChange?.Invoke();
        }

        protected SqlBase(string dbPath)
        {
            var folder = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            bool newDb = !File.Exists(dbPath);

            Conn = new SqliteConnection($"Data Source={dbPath}");
            Conn.Open();

            if (newDb)  OnCreate();
        }

        // Derived override this to create a new table
        protected abstract void OnCreate();

        protected SqliteCommand Command(string sql)
        {
            var cmd = Conn.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }

        public void Dispose()
        {
            Conn?.Dispose();
        }
    }

    // Example inherited use
    public class PeopleDb : SqlBase
    {
        public PeopleDb(string dbPath) : base(dbPath) { }

        protected override void OnCreate()
        {
            using var cmd = Conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS people (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL
            );";
            cmd.ExecuteNonQuery();
        }

        public List<string> GetNames()
        {
            var list = new List<string>();

            using var cmd = Command("SELECT name FROM people");
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
                list.Add(reader.GetString(0));

            return list;
        }

        public void AddName(string name)
        {
            using var cmd = Command("INSERT INTO people (name) VALUES ($name)");
            cmd.Parameters.AddWithValue("$name", name);
            cmd.ExecuteNonQuery();
            RaiseChange();
        }
    }
}
