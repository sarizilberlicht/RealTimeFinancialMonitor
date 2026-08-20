using Backend.Controllers;
using Backend.Hubs;
using Backend.Models;
using Backend.Services;
using Backend.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Backend.Tests;

public class TransactionsControllerTests
{
    [Fact]
    public async Task UpdateStatus_ExistingTransaction_ShouldReturnUpdatedTransaction()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var hubContext = CreateHubContext();

        var service = new TransactionService(
            store,
            hubContext
        );

        var controller = new TransactionsController(
            service,
            store
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        await store.AddAsync(transaction);

        var request = new UpdateTransactionStatusRequest
        {
            Status = TransactionStatus.Completed
        };

        // Act
        var result = await controller.UpdateStatus(
            transaction.TransactionId,
            request
        );

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);

        var updatedTransaction =
            Assert.IsType<Transaction>(okResult.Value);

        Assert.Equal(
            TransactionStatus.Completed,
            updatedTransaction.Status
        );

        Assert.Equal(
            transaction.TransactionId,
            updatedTransaction.TransactionId
        );
    }

    [Fact]
    public async Task UpdateStatus_NonExistingTransaction_ShouldReturnNotFound()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var hubContext = CreateHubContext();

        var service = new TransactionService(
            store,
            hubContext
        );

        var controller = new TransactionsController(
            service,
            store
        );

        var request = new UpdateTransactionStatusRequest
        {
            Status = TransactionStatus.Completed
        };

        // Act
        var result = await controller.UpdateStatus(
            Guid.NewGuid(),
            request
        );

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    private static IHubContext<TransactionHub> CreateHubContext()
    {
        var clientProxy = new Mock<IClientProxy>();

        clientProxy
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();

        clients
            .Setup(c => c.All)
            .Returns(clientProxy.Object);

        var hubContext =
            new Mock<IHubContext<TransactionHub>>();

        hubContext
            .Setup(h => h.Clients)
            .Returns(clients.Object);

        return hubContext.Object;
    }

    private class FakeTransactionStore : ITransactionStore
    {
        private readonly List<Transaction> _transactions = [];

        public Task AddAsync(Transaction transaction)
        {
            _transactions.Add(transaction);

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
            TransactionStatus status)
        {
            var transaction = _transactions.FirstOrDefault(
                item =>
                    item.TransactionId == transactionId
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
}