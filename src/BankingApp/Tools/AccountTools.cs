using BankingApp;
using BankingApp.Data;
using Microsoft.Data.Sqlite;

namespace BankingApp.Tools;

/// <summary>
/// Read-only account-query tools. Moved/rewritten into the consolidated
/// BankingApp from the Session 2 scaffold's BankingMcpServer. All writes
/// (wire transfers) are simulated and gate on the participant's wired
/// threshold so Milestone 3 can observe a deterministic PAUSED state.
/// </summary>
public sealed class AccountTools
{
    private readonly BankingDbConnectionFactory _db;
    private readonly FdeOptions _fde;

    public AccountTools(BankingDbConnectionFactory db, FdeOptions fde)
    {
        _db = db;
        _fde = fde;
    }

    public string GetBalance(int accountId)
    {
        using var connection = _db.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.name, a.account_number, a.name, a.balance_cents, a.currency
            FROM accounts a JOIN customers c ON c.id = a.customer_id
            WHERE a.id = $id
            """;
        command.Parameters.AddWithValue("$id", accountId);
        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return $"Account {accountId}: not found";
        }

        var customer = reader.GetString(0);
        var number = reader.GetString(1);
        var accountName = reader.GetString(2);
        var cents = reader.GetInt64(3);
        var currency = reader.GetString(4);
        return $"{customer} {accountName} (#{number}): {FormatMoney(cents, currency)}";
    }

    public string ListAccounts()
    {
        using var connection = _db.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.name, a.id, a.account_number, a.name, a.balance_cents, a.currency
            FROM accounts a JOIN customers c ON c.id = a.customer_id
            ORDER BY a.id
            """;

        var lines = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var customer = reader.GetString(0);
            var id = reader.GetInt64(1);
            var number = reader.GetString(2);
            var accountName = reader.GetString(3);
            var cents = reader.GetInt64(4);
            var currency = reader.GetString(5);
            lines.Add($"{customer} {accountName} (#{number}, id {id}): {FormatMoney(cents, currency)}");
        }

        return lines.Count == 0 ? "no accounts found" : string.Join("\n", lines);
    }

    public string GetTransactionHistory(int accountId, int limit = 5)
    {
        using var connection = _db.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.description, t.amount_cents, a.currency, t.occurred_at
            FROM transactions t JOIN accounts a ON a.id = t.account_id
            WHERE t.account_id = $id
            ORDER BY t.occurred_at DESC, t.id DESC
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$id", accountId);
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var lines = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var description = reader.GetString(0);
            var cents = reader.GetInt64(1);
            var currency = reader.GetString(2);
            var when = reader.GetString(3);
            lines.Add($"{when} {description}: {FormatMoney(cents, currency)}");
        }

        return lines.Count == 0 ? $"account {accountId}: no transactions" : string.Join("\n", lines);
    }

    /// <summary>
    /// Simulated wire transfer. Over-threshold transfers are PAUSED and await
    /// approval (Milestone 3 checks this explicit paused state); under-threshold
    /// transfers are POSTED. The account ledger is intentionally read-only — the
    /// "bank" is a baked, ephemeral seed, so no actual balance mutation.
    /// </summary>
    public string SubmitWireTransfer(int fromAccountId, int toAccountId, decimal amount, string memo = "")
    {
        if (amount <= 0)
        {
            return $"ERROR: transfer amount must be positive (received {amount:C})";
        }

        var threshold = _fde.WireTransferThreshold;
        if (amount > threshold)
        {
            return $"PAUSED_PENDING_APPROVAL: ${amount:0.00} from account {fromAccountId} to account {toAccountId} " +
                   $"exceeds the ${threshold:0.00} wire threshold and requires explicit approval before it can post.";
        }

        return $"POSTED: ${amount:0.00} from account {fromAccountId} to account {toAccountId}" +
               (string.IsNullOrWhiteSpace(memo) ? "" : $" ({memo})");
    }

    private static string FormatMoney(long cents, string currency)
    {
        var amount = cents / 100m;
        return currency == "USD" ? $"${amount:0.00}" : $"{amount:0.00} {currency}";
    }
}