using System.Collections.Concurrent;
using Backend.Models;

namespace Backend.Storage;

public class InMemoryTransactionStore : ITransactionStore
{
    private readonly ConcurrentQueue<Transaction> _transactions = new();

    public Task AddAsync(Transaction transaction)
    {
        _transactions.Enqueue(transaction);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Transaction>> GetAllAsync()
    {
        IReadOnlyCollection<Transaction> result =
            _transactions.ToArray();

        return Task.FromResult(result);
    }

    public Task<Transaction?> UpdateStatusAsync(
        Guid transactionId,
        TransactionStatus status
    )
    {
        var transaction = _transactions.FirstOrDefault(
            item => item.TransactionId == transactionId
        );

        if (transaction is null)
        {
            return Task.FromResult<Transaction?>(null);
        }

        transaction.Status = status;

        return Task.FromResult<Transaction?>(
            transaction
        );
    }
}