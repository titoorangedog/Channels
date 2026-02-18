using Microsoft.AspNetCore.Mvc;
using QueueService.Models;
using QueueService.Services;

namespace QueueService.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportQueueService queueService) : ControllerBase
{
    [HttpPost("enqueue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Enqueue([FromBody] ReportExecutionModel model, CancellationToken cancellationToken)
    {
        await queueService.EnqueueAsync(model, cancellationToken);
        return Accepted(new { Message = "Messaggio accodato", Queue = "BackOfficeEU.Reports", MessageId = model.Id });
    }

    [HttpPost("dequeue")]
    [ProducesResponseType(typeof(DequeueResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dequeue(CancellationToken cancellationToken)
    {
        var message = await queueService.TryDequeueAsync(cancellationToken);
        return Ok(new DequeueResponse(message is not null, message));
    }

    [HttpPost("fail")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Fail([FromBody] FailMessageRequest request, CancellationToken cancellationToken)
    {
        await queueService.FailAsync(request, cancellationToken);
        return Accepted(new { Message = "Messaggio spostato nella error queue", Queue = "BackOfficeEU.Reports.Error", MessageId = request.Payload.Id });
    }

    [HttpGet("errors")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ErrorQueueMessage>), StatusCodes.Status200OK)]
    public IActionResult GetErrors() => Ok(queueService.SnapshotErrors());

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

        return Accepted(new { Message = "Messaggio reinserito", Queue = "BackOfficeEU.Reports", MessageId = messageId });
    }
}
