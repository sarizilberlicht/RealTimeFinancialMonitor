using Backend.Models;
using Backend.Services;
using Backend.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactionService;
    private readonly ITransactionStore _store;

    public TransactionsController(
        TransactionService transactionService,
        ITransactionStore store)
    {
        _transactionService = transactionService;
        _store = store;
    }

    [HttpPost]
    public async Task<IActionResult> Create(Transaction transaction)
    {
        try
        {
            await _transactionService.ProcessAsync(transaction);

            return Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var transactions = await _store.GetAllAsync();

        return Ok(transactions);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateTransactionStatusRequest request
    )
    {
        var updatedTransaction =
            await _transactionService.UpdateStatusAsync(
                id,
                request.Status
            );

        if (updatedTransaction is null)
        {
            return NotFound();
        }

        return Ok(updatedTransaction);
    }

}