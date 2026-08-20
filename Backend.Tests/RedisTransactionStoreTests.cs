using Backend.Models;
using Backend.Storage;
using StackExchange.Redis;
using Xunit;

namespace Backend.Tests;

public class RedisTransactionStoreTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task AddAsync_Transaction_ShouldBeAvailableFromAnotherStoreInstance()
    {
        // Arrange
        var connection = await ConnectionMultiplexer.ConnectAsync(
            "localhost:6379"
        );

        var database = connection.GetDatabase();

        await database.KeyDeleteAsync("transactions:test");
        try
        {
            var storeA = new RedisTransactionStore(
                connection,
                "transactions:test"
            );

            var storeB = new RedisTransactionStore(
                connection,
                "transactions:test"
            );

            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                Amount = 500m,
                Currency = "USD",
                Status = TransactionStatus.Completed,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Act
            await storeA.AddAsync(transaction);

            var result = await storeB.GetAllAsync();

            // Assert
            Assert.Contains(
                result,
                item => item.TransactionId == transaction.TransactionId
            );
        }
        finally
        {
            await database.KeyDeleteAsync("transactions:test");
            await connection.CloseAsync();
        }
    }
}