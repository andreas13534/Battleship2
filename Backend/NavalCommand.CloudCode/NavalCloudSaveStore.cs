using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace NavalCommandOnline;

public sealed class NavalStoredValue<T>
{
    public T? Value { get; init; }
    public string? WriteLock { get; init; }
}

/// <summary>All authoritative records are server-only Cloud Save custom items.</summary>
public sealed class NavalCloudSaveStore
{
    private readonly IGameApiClient _api;

    public NavalCloudSaveStore(IGameApiClient api) => _api = api;

    public async Task<NavalStoredValue<T>> GetAsync<T>(IExecutionContext context, string entityId, string key)
    {
        var response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, entityId, new List<string> { key });
        var item = response.Data.Results.FirstOrDefault();
        if (item == null) return new NavalStoredValue<T>();
        string json = item.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(json)) return new NavalStoredValue<T> { WriteLock = item.WriteLock };
        return new NavalStoredValue<T>
        {
            Value = JsonConvert.DeserializeObject<T>(json),
            WriteLock = item.WriteLock
        };
    }

    public async Task<string?> PutAsync<T>(IExecutionContext context, string entityId, string key, T value, string? writeLock)
    {
        string json = JsonConvert.SerializeObject(value);
        var response = await _api.CloudSaveData.SetPrivateCustomItemAsync(
            context, context.ServiceToken, context.ProjectId, entityId, new SetItemBody(key, json, writeLock));
        return response.Data.WriteLock;
    }

    public async Task DeleteEntityAsync(IExecutionContext context, string entityId)
    {
        await _api.CloudSaveData.DeletePrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, entityId);
    }
}
