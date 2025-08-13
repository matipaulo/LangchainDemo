namespace RealWorldExample.Api.Tools;

public interface IAiTool
{
    string Name { get; }
    string Description { get; }       // short, for prompt catalog
    Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken cancellationToken = default);
}