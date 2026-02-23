using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReportConsumer.Models;

namespace ReportConsumer.Services
{
    public sealed class QueueServiceClient
    {
        private readonly HttpClient _client;
        private readonly ILogger<QueueServiceClient> _logger;

        public QueueServiceClient(HttpClient client, ILogger<QueueServiceClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<ReportExecutionModel?> TryDequeueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Requesting next message from queue API.");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reports/dequeue");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                // No message available (server timed out / returned no-content).
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Queue API returned non-success status code {StatusCode}", response.StatusCode);
                return null;
            }

            var model = await response.Content.ReadFromJsonAsync<ReportExecutionModel>(cancellationToken: cancellationToken);
            return model;
        }
    }
}
