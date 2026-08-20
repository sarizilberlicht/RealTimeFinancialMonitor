using Backend.Hubs;
using Backend.Models;
using Backend.Services;
using Backend.Storage;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace Backend.Tests;

public class TransactionServiceTests
{
    private static IHubContext<TransactionHub> CreateHubContext()
    {
        var clientProxyMock = new Mock<IClientProxy>();

        clientProxyMock
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients>();

        hubClientsMock
            .Setup(clients => clients.All)
            .Returns(clientProxyMock.Object);

        var hubContextMock =
            new Mock<IHubContext<TransactionHub>>();

        hubContextMock
            .Setup(context => context.Clients)
            .Returns(hubClientsMock.Object);

        return hubContextMock.Object;
    }

    [Fact]
    public async Task Process_ValidTransaction_ShouldStoreTransaction()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 1500.50m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await service.ProcessAsync(transaction);

        var result = await store.GetAllAsync();

        // Assert
        Assert.Contains(transaction, result);
    }

    [Fact]
    public async Task Process_TransactionWithEmptyCurrency_ShouldThrowArgumentException()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 1500.50m,
            Currency = "",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(transaction)
        );
    }

    [Fact]
    public async Task Process_TransactionWithEmptyId_ShouldThrowArgumentException()
    {
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.Empty,
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(transaction)
        );
    }

    [Fact]
    public async Task Process_TransactionWithInvalidStatus_ShouldThrowArgumentException()
    {
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = (TransactionStatus)999,
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(transaction)
        );
    }

    [Fact]
    public async Task Process_TransactionWithDefaultTimestamp_ShouldThrowArgumentException()
    {
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = default
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(transaction)
        );
    }

    [Fact]
    public async Task Process_TransactionWithNonPositiveAmount_ShouldThrowArgumentException()
    {
        var store = new FakeTransactionStore();

        var service = new TransactionService(
            store,
            CreateHubContext()
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 0m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ProcessAsync(transaction)
        );
    }

    [Fact]
    public async Task Process_ValidTransaction_ShouldBroadcastTransaction()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var clientProxyMock = new Mock<IClientProxy>();

        clientProxyMock
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hubClientsMock = new Mock<IHubClients>();

        hubClientsMock
            .Setup(clients => clients.All)
            .Returns(clientProxyMock.Object);

        var hubContextMock =
            new Mock<IHubContext<TransactionHub>>();

        hubContextMock
            .Setup(context => context.Clients)
            .Returns(hubClientsMock.Object);

        var service = new TransactionService(
            store,
            hubContextMock.Object
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await service.ProcessAsync(transaction);

        // Assert
        clientProxyMock.Verify(
            client => client.SendCoreAsync(
                "TransactionReceived",
                It.Is<object?[]>(args =>
                    args.Length == 1 &&
                    ReferenceEquals(args[0], transaction)
                ),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateStatusAsync_ExistingTransaction_ShouldUpdateStatus()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var hubContext = new Mock<IHubContext<TransactionHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();

        hubContext
            .Setup(h => h.Clients)
            .Returns(clients.Object);

        clients
            .Setup(c => c.All)
            .Returns(clientProxy.Object);

        var service = new TransactionService(
            store,
            hubContext.Object
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        await store.AddAsync(transaction);

        // Act
        await service.UpdateStatusAsync(
            transaction.TransactionId,
            TransactionStatus.Completed
        );

        // Assert
        var transactions = await store.GetAllAsync();

        var updatedTransaction = transactions.Single(
            t => t.TransactionId == transaction.TransactionId
        );

        Assert.Equal(
            TransactionStatus.Completed,
            updatedTransaction.Status
        );
    }

    [Fact]
    public async Task UpdateStatusAsync_ExistingTransaction_ShouldBroadcastUpdatedTransaction()
    {
        // Arrange
        var store = new FakeTransactionStore();

        var hubContext = new Mock<IHubContext<TransactionHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();

        hubContext
            .Setup(h => h.Clients)
            .Returns(clients.Object);

        clients
            .Setup(c => c.All)
            .Returns(clientProxy.Object);

        var service = new TransactionService(
            store,
            hubContext.Object
        );

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100,
            Currency = "USD",
            Status = TransactionStatus.Pending,
            Timestamp = DateTimeOffset.UtcNow
        };

        await store.AddAsync(transaction);

        // Act
        await service.UpdateStatusAsync(
            transaction.TransactionId,
            TransactionStatus.Completed
        );

        // Assert
        clientProxy.Verify(
    client => client.SendCoreAsync(
        "TransactionReceived",
        It.Is<object[]>(arguments =>
            arguments.Length == 1 &&
            arguments[0] != null &&
            ((Transaction)arguments[0]).TransactionId ==
                transaction.TransactionId &&
            ((Transaction)arguments[0]).Status ==
                TransactionStatus.Completed
        ),
        It.IsAny<CancellationToken>()
    ),
    Times.Once
);
    }

    private class FakeTransactionStore : ITransactionStore
    {
        private readonly List<Transaction> _transactions = new();

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
}