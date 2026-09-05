using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NavalCommandOnline;
using Unity.Services.CloudCode.Core;

namespace NavalCommand.CloudCode.Tests;

public sealed class MemoryCloudSaveStore : INavalCloudSaveStore
{
    private sealed class StoredRecord
    {
        public StoredRecord(string json, string writeLock)
        {
            Json = json;
            WriteLock = writeLock;
        }

        public string Json { get; }
        public string WriteLock { get; }
    }

    private readonly object gate = new();
    private readonly Dictionary<(string EntityId, string Key), StoredRecord> records = new();
    private long nextWriteLock;

    public Action<string>? BeforeWrite { get; set; }
    public Action<string>? AfterWrite { get; set; }

    public Task<NavalStoredValue<T>> GetAsync<T>(IExecutionContext context, string entityId, string key)
    {
        lock (gate)
        {
            if (!records.TryGetValue((entityId, key), out StoredRecord? record))
                return Task.FromResult(new NavalStoredValue<T>());

            return Task.FromResult(new NavalStoredValue<T>
            {
                Value = JsonConvert.DeserializeObject<T>(record.Json),
                WriteLock = record.WriteLock
            });
        }
    }

    public Task<string?> PutAsync<T>(IExecutionContext context, string entityId, string key, T value, string? writeLock)
    {
        BeforeWrite?.Invoke(entityId);

        string nextLock;
        lock (gate)
        {
            if (records.TryGetValue((entityId, key), out StoredRecord? existing) &&
                (writeLock == null || !string.Equals(writeLock, existing.WriteLock, StringComparison.Ordinal)))
                throw new InvalidOperationException("WRITE_CONFLICT");

            string json = JsonConvert.SerializeObject(value);
            nextLock = (++nextWriteLock).ToString(CultureInfo.InvariantCulture);
            records[(entityId, key)] = new StoredRecord(json, nextLock);
        }

        AfterWrite?.Invoke(entityId);
        return Task.FromResult<string?>(nextLock);
    }

    public Task DeleteEntityAsync(IExecutionContext context, string entityId)
    {
        lock (gate)
        {
            List<(string EntityId, string Key)> keys = new();
            foreach ((string EntityId, string Key) recordKey in records.Keys)
                if (string.Equals(recordKey.EntityId, entityId, StringComparison.Ordinal))
                    keys.Add(recordKey);
            foreach ((string EntityId, string Key) recordKey in keys)
                records.Remove(recordKey);
        }

        return Task.CompletedTask;
    }

}
