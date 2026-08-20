using Backend.Models;
using Backend.Storage;
using Xunit;

namespace Backend.Tests;

public class InMemoryTransactionStoreTests
{
    [Fact]
    public async Task Add_Transaction_ShouldStoreIt()
    {
        // Arrange
        var store = new InMemoryTransactionStore();

        var transaction = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Completed,
            Timestamp = DateTimeOffset.UtcNow
        };

        // Act
        await store.AddAsync(transaction);

        var result = await store.GetAllAsync();

        // Assert
        Assert.Contains(transaction, result);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAllStoredTransactions()
    {
        // Arrange
        var store = new InMemoryTransactionStore();

        var first = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 100m,
            Currency = "USD",
            Status = TransactionStatus.Completed,
            Timestamp = DateTimeOffset.UtcNow
        };

        var second = new Transaction
        {
            TransactionId = Guid.NewGuid(),
            Amount = 200m,
            Currency = "EUR",
            Status = TransactionStatus.Failed,
            Timestamp = DateTimeOffset.UtcNow
        };

        await store.AddAsync(first);
        await store.AddAsync(second);

        // Act
        var result = await store.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
    }

    [Fact]
    public async Task Add_MultipleTransactionsConcurrently_ShouldStoreAll()
    {
        // Arrange
        var store = new InMemoryTransactionStore();

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(async () =>
            {
                var transaction = new Transaction
                {
                    TransactionId = Guid.NewGuid(),
                    Amount = i + 1,
                    Currency = "USD",
                    Status = TransactionStatus.Pending,
                    Timestamp = DateTimeOffset.UtcNow
                };

                await store.AddAsync(transaction);
            }));

        // Act
        await Task.WhenAll(tasks);

        var result = await store.GetAllAsync();

        // Assert
        Assert.Equal(100, result.Count);
    }
}