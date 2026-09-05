using CarrierSimulator.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace CarrierSimulator.Services;

public sealed class CarrierClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5064";
    public string ApiKey { get; set; } = "";
}
public sealed class CarrierClient(HttpClient client)
{
    public async Task<List<ShipmentSummary>> SearchAsync(string? keyword, CancellationToken token)
    {
        using var response = await client.GetAsync("/api/demo-carrier/shipments?keyword=" + Uri.EscapeDataString(keyword ?? ""), token);
        await EnsureSuccess(response, token);
        return await response.Content.ReadFromJsonAsync<List<ShipmentSummary>>(token) ?? [];
    }
    public async Task<ShipmentDetail> GetAsync(string id, CancellationToken token)
    {
        using var response = await client.GetAsync("/api/demo-carrier/shipments/" + Uri.EscapeDataString(id), token);
        await EnsureSuccess(response, token);
        return await response.Content.ReadFromJsonAsync<ShipmentDetail>(token) ?? throw new InvalidOperationException("运单响应为空");
    }
    public async Task AppendAsync(EventInput input, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync("/api/demo-carrier/shipments/" + Uri.EscapeDataString(input.DeliveryId) + "/events", input, token);
        await EnsureSuccess(response, token);
    }
    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string? message = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            if (json.RootElement.TryGetProperty("message", out var value)) message = value.GetString();
        }
        throw new CarrierRequestException(message ?? $"物流接口返回 {(int)response.StatusCode}，请检查服务和授权配置");
    }
}
public sealed class CarrierRequestException(string message) : Exception(message);
