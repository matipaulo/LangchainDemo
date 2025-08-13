namespace RealWorldExample.Api.Tools;

public sealed class CreateWorkOrderTool : IAiTool
{
    public string Name => "create_work_order";
    
    public string Description => "Create a maintenance work order. Args: equipmentId (string), summary (string), priority (Low|Medium|High).";
    
    public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken cancellationToken = default)
    {
        // Simulate work order creation
        await Task.Delay(100, cancellationToken);
        
        return new
        {
            WorkOrderId = Guid.NewGuid().ToString()
        };
    }
}