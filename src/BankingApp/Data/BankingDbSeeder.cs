namespace BankingApp.Data;

/// <summary>
/// Idempotent seed for legacy_bank.db. Runs at startup; on a fresh (or empty)
/// database it creates the legacy schema and the deterministic seed used by the
/// eval (`Maria Chen`, customer_id 1, account_id 101, checking balance $4523.10).
/// </summary>
public sealed class BankingDbSeeder
{
    private readonly BankingDbConnectionFactory _db;
    private readonly ILogger<BankingDbSeeder> _logger;

    public BankingDbSeeder(BankingDbConnectionFactory db, ILogger<BankingDbSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void SeedIfMissing()
    {
        using var connection = _db.Create();

        using (var schema = connection.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS customers (
                    id    INTEGER PRIMARY KEY,
                    name  TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS accounts (
                    id             INTEGER PRIMARY KEY,
                    customer_id    INTEGER NOT NULL,
                    account_number TEXT NOT NULL,
                    name           TEXT NOT NULL,
                    balance_cents  INTEGER NOT NULL,
                    currency       TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS transactions (
                    id           INTEGER PRIMARY KEY,
                    account_id   INTEGER NOT NULL,
                    amount_cents INTEGER NOT NULL,
                    description  TEXT NOT NULL,
                    occurred_at  TEXT NOT NULL
                );
                """;
            schema.ExecuteNonQuery();
        }

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM customers WHERE id = 1";
            if (Convert.ToInt64(check.ExecuteScalar()) > 0)
            {
                _logger.LogInformation("legacy_bank.db seed present at {DbPath}", _db.DbPath);
                return;
            }
        }

        using (var seed = connection.CreateCommand())
        {
            seed.CommandText = """
                INSERT INTO customers (id, name) VALUES (1, 'Maria Chen');

                INSERT INTO accounts (id, customer_id, account_number, name, balance_cents, currency)
                VALUES (101, 1, '101', 'Checking', 452310, 'USD'),
                       (102, 1, '102', 'Savings',   125000, 'USD');

                INSERT INTO transactions (id, account_id, amount_cents, description, occurred_at) VALUES
                    (1, 101,  250000, 'Payroll deposit',              '2026-09-05T00:00:00Z'),
                    (2, 101, -155000, 'Credit card payment',          '2026-09-01T00:00:00Z'),
                    (3, 101,  -45000, 'Grocery store purchase',       '2026-08-28T00:00:00Z'),
                    (4, 101,  120000, 'Mobile deposit — cheque',      '2026-08-20T00:00:00Z'),
                    (5, 102,  500000, 'Transfer from checking',       '2026-09-02T00:00:00Z'),
                    (6, 102,   -2500, 'Monthly account service fee',  '2026-09-01T00:00:00Z');
                """;
            seed.ExecuteNonQuery();
        }

        _logger.LogInformation("legacy_bank.db seeded at {DbPath} (Maria Chen, account 101, $4523.10)", _db.DbPath);
    }
}