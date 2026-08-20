namespace Backend.Models;

public class Transaction
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransactionStatus Status { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed
}