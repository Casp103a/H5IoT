using Microsoft.Data.Sqlite;

namespace MqttBroker.Managers
{
    internal class DatabaseManager
    {
        private const string connectionString = "Data Source=/app/data/mqttbroker.db";

        public string ConnectionString { get => connectionString; }

        public DatabaseManager()
        {
            SetupDatabase();
        }

        private void SetupDatabase()
        {
            if (!CheckIfPinTableExists())
                CreatePinTable();

            if (!CheckIfCardTableExists())
                CreateCardTable();

            if (!CheckIfUserTableExists())
                CreateUserTable();
        }

        //This will create the pin table base the on the PinModel
        private void CreatePinTable()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            const string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Pin (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Pin TEXT NOT NULL,
                    UniqueID TEXT NOT NULL,
                    CardID TEXT NOT NULL
                );";

            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private bool CheckIfPinTableExists()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name='Pin';";
            using var command = new SqliteCommand(query, connection);
            using var reader = command.ExecuteReader();

            return reader.HasRows;
        }

        private void CreateCardTable()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            const string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Card (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    CardID TEXT NOT NULL,
                    CardName TEXT NOT NULL
                );";
            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private bool CheckIfCardTableExists()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name='Card';";
            using var command = new SqliteCommand(query, connection);
            using var reader = command.ExecuteReader();
            return reader.HasRows;
        }

        private void CreateUserTable()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            const string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS Users (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CardID TEXT NOT NULL,
                    Pin TEXT NOT NULL,
                    UserName TEXT NOT NULL
                );";
            using var command = new SqliteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        private bool CheckIfUserTableExists()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users';";
            using var command = new SqliteCommand(query, connection);
            using var reader = command.ExecuteReader();
            return reader.HasRows;
        }
    }
}