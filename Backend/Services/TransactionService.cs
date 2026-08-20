using Backend.Hubs;
using Backend.Models;
using Backend.Storage;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Services;

public class TransactionService
{
    private readonly ITransactionStore _store;
    private readonly IHubContext<TransactionHub> _hubContext;

    public TransactionService(
        ITransactionStore store,
        IHubContext<TransactionHub> hubContext)
    {
        _store = store;
        _hubContext = hubContext;
    }

    public async Task ProcessAsync(Transaction transaction)
    {
        Validate(transaction);

        await _store.AddAsync(transaction);

        await _hubContext.Clients.All.SendAsync(
            "TransactionReceived",
            transaction
        );
    }

    private static void Validate(Transaction transaction)
    {
        if (transaction.TransactionId == Guid.Empty)
        {
            throw new ArgumentException("TransactionId is required.");
        }

        if (transaction.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(transaction.Currency))
        {
            throw new ArgumentException("Currency is required.");
        }

        if (!Enum.IsDefined(transaction.Status))
        {
            throw new ArgumentException("Status is invalid.");
        }

        if (transaction.Timestamp == default)
        {
            throw new ArgumentException("Timestamp is required.");
        }
    }

    public async Task<Transaction?> UpdateStatusAsync(
    Guid transactionId,
    TransactionStatus status
)
    {
        var updatedTransaction =
            await _store.UpdateStatusAsync(
                transactionId,
                status
            );

        if (updatedTransaction is null)
        {
            return null;
        }

        await _hubContext.Clients.All.SendAsync(
            "TransactionReceived",
            updatedTransaction
        );

        return updatedTransaction;
    }
}