namespace RealWorldExample.Api;

using System.Collections.Concurrent;

public sealed class ConversationMemory
{
    private readonly ConcurrentDictionary<Guid, LinkedList<(string Role, string Text)>> _memory = new();
    // private readonly LinkedList<(string Role, string Text)> _turns = new();
    private readonly int _maxTurns;

    public ConversationMemory(int maxTurns = 8)
    {
        _maxTurns = Math.Max(2, maxTurns);
    }

    public void AddUser(Guid conversationId, string text) => _memory.AddOrUpdate(conversationId,
        _ => new LinkedList<(string Role, string Text)>([("User", text)]), (_, turns) =>
        {
            turns.AddLast(("User", text));
            return turns;
        });
    
    public void AddAssistant(Guid conversationId, string text) => _memory.AddOrUpdate(conversationId, 
        _ => new LinkedList<(string Role, string Text)>([("Assistant", text)]), (_, turns) =>
        {
            turns.AddLast(("Assistant", text));
            return turns;
        });

    public string RenderHistory(Guid conversationId)
    {
        if (!_memory.TryGetValue(conversationId, out var turns)) 
            return string.Empty;
    
        // Keep only the most recent N turns
        while (turns.Count > _maxTurns) turns.RemoveFirst();
        // Render in a simple, robust format the model can follow
        return string.Join("\n", turns.Select(t => $"{t.Role}: {t.Text}"));
    }
}