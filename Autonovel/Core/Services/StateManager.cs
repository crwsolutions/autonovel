using System.Text.Json;
using Autonovel.Core.Domain;

namespace Autonovel.Core.Services;

public interface IStateManager
{
    PipelineState Load();
    void Save(PipelineState state);
    Task UpdateStateAsync(Func<PipelineState, PipelineState> updateFn, CancellationToken ct = default);
}

public class StateManager : IStateManager
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _options;

    public StateManager(string statePath)
    {
        _statePath = statePath;
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public PipelineState Load()
    {
        if (!File.Exists(_statePath))
            return PipelineStateDefaults.Default();

        var json = File.ReadAllText(_statePath);
        return JsonSerializer.Deserialize<PipelineState>(json, _options) 
            ?? PipelineStateDefaults.Default();
    }

    public void Save(PipelineState state)
    {
        var json = JsonSerializer.Serialize(state, _options);
        File.WriteAllText(_statePath, json);
    }

    public async Task UpdateStateAsync(Func<PipelineState, PipelineState> updateFn, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var current = Load();
            var updated = updateFn(current);
            Save(updated);
        }, ct);
    }
}