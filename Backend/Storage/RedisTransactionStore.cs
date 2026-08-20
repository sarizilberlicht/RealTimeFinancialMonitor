using System.Text.Json;
using Backend.Models;
using StackExchange.Redis;

namespace Backend.Storage;

public class RedisTransactionStore : ITransactionStore
{
    private readonly IDatabase _database;
    private readonly string _transactionsKey;

    public RedisTransactionStore(
        IConnectionMultiplexer connection,
        string transactionsKey)
    {
        _database = connection.GetDatabase();
        _transactionsKey = transactionsKey;
    }

    public async Task AddAsync(Transaction transaction)
    {
        var json = JsonSerializer.Serialize(transaction);

        await _database.HashSetAsync(
            _transactionsKey,
            transaction.TransactionId.ToString(),
            json
        );
    }

    public async Task<IReadOnlyCollection<Transaction>> GetAllAsync()
    {
        var entries = await _database.HashGetAllAsync(
            _transactionsKey
        );

        var transactions = entries
            .Select(entry =>
                JsonSerializer.Deserialize<Transaction>(
                    entry.Value.ToString()
                )
            )
            .Where(transaction => transaction is not null)
            .Cast<Transaction>()
            .ToArray();

        return transactions;
    }

    public async Task<Transaction?> UpdateStatusAsync(
    Guid transactionId,
    TransactionStatus status
)
    {
        var value = await _database.HashGetAsync(
            _transactionsKey,
            transactionId.ToString()
        );

        if (value.IsNullOrEmpty)
        {
            return null;
        }

        var transaction =
            JsonSerializer.Deserialize<Transaction>(
                value.ToString()
            );

        if (transaction is null)
        {
            return null;
        }

        transaction.Status = status;

        var updatedJson =
            JsonSerializer.Serialize(transaction);

        await _database.HashSetAsync(
            _transactionsKey,
            transactionId.ToString(),
            updatedJson
        );

        return transaction;
    }
}