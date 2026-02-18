using ChannelsApi.Models;
using ChannelsApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChannelsApi.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportQueueService queueService) : ControllerBase
{
    [HttpPost("enqueue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Enqueue([FromBody] ReportExecutionModel model, CancellationToken cancellationToken)
    {
        await queueService.EnqueueAsync(model, cancellationToken);

        return Accepted(new
        {
            Message = "Messaggio accodato correttamente.",
            Queue = "BackOfficeEU.Reports",
            MessageId = model.Id
        });
    }

    [HttpGet("errors")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ErrorQueueMessage>), StatusCodes.Status200OK)]
    public IActionResult GetErrors()
    {
        return Ok(queueService.SnapshotErrors());
    }

    [HttpPost("errors/{messageId}/requeue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequeueError(string messageId, CancellationToken cancellationToken)
    {
        var found = await queueService.RequeueErrorAsync(messageId, cancellationToken);
        if (!found)
        {
            return NotFound(new { Message = $"Messaggio {messageId} non presente nella error queue." });
        }

        return Accepted(new
        {
            Message = "Messaggio reinserito nella queue principale.",
            Queue = "BackOfficeEU.Reports",
            MessageId = messageId
        });
    }
}
