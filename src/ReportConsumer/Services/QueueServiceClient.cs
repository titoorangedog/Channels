using System.Net.Http.Json;
using ReportConsumer.Models;

namespace ReportConsumer.Services;

public sealed class QueueServiceClient(HttpClient httpClient)
{
    public async Task<DequeueResponse?> TryDequeueAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync("api/reports/dequeue", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DequeueResponse>(cancellationToken: cancellationToken);
    }

    public async Task SendFailAsync(FailMessageRequest failRequest, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/reports/fail", failRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
