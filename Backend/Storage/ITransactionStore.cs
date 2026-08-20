using Backend.Models;

namespace Backend.Storage;

public interface ITransactionStore
{
    Task AddAsync(Transaction transaction);

    Task<IReadOnlyCollection<Transaction>> GetAllAsync();

    Task<Transaction?> UpdateStatusAsync(
    Guid transactionId,
    TransactionStatus status
);
}