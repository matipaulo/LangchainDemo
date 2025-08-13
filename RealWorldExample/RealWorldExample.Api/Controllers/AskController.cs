namespace RealWorldExample.Api.Controllers;

using LangChain.Databases.Sqlite;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers.OpenAI;
using Microsoft.AspNetCore.Mvc;
using RealWorldExample.Api.Tools;
using LangChain.Memory;
using static LangChain.Chains.Chain;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private const string CollectionName = "troubleshootingDocuments";
    private readonly List<IAiTool> _tools = [new CreateWorkOrderTool()];
    
    private readonly SqLiteVectorDatabase _vectorDatabase;
    private readonly OpenAiEmbeddingModel _embeddingModel;
    private readonly OpenAiChatModel _openAiChatModel;
    private readonly ConversationMemory _memory;

    public AskController(SqLiteVectorDatabase vectorDatabase, OpenAiEmbeddingModel embeddingModel, OpenAiChatModel openAiChatModel, ConversationMemory memory)
    {
        _vectorDatabase = vectorDatabase;
        _embeddingModel = embeddingModel;
        _openAiChatModel = openAiChatModel;
        _memory = memory;
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestAsync([FromBody] string documentUrl)
    {
        using var http = new HttpClient();
        var pdfBytes = await http.GetByteArrayAsync(documentUrl);
        using var pdfStream = new MemoryStream(pdfBytes);

        await _vectorDatabase.AddDocumentsFromAsync<PdfPigPdfLoader>(
            _embeddingModel, // Used to convert text to embeddings
            dimensions: 1536, // Should be 1536 for TextEmbeddingV3SmallModel
            dataSource: new DataSource
            {
                Type = DataSourceType.Stream,
                Stream = pdfStream,
            },
            collectionName: CollectionName,
            textSplitter: null,
            behavior: AddDocumentsToDatabaseBehavior.OverwriteExistingCollection);

        return Ok();
    }

    [HttpPost("/{conversationId}/ask")]
    public async Task<ActionResult<string>> AskAsync([FromRoute] Guid conversationId, [FromBody] string question,
        CancellationToken cancellationToken)
    {
        _memory.AddUser(conversationId, question);
        
        var context = await RetrieveRelevantContextAsync(question, cancellationToken);
        var toolCatalog = BuildToolCatalog();
        var systemPrompt = Prompts.SystemPrompt.Replace("{tools_catalog}", toolCatalog);
        var finalPrompt = BuildRagPrompt(conversationId, context, question);

        bool toolExecuted = false;

        for (var step = 0; step < 4; step++)
        {
            var agentReply = await GenerateAgentReplyAsync(systemPrompt, finalPrompt, cancellationToken);

            var normalizedReply = NormalizeAgentReply(agentReply);

            if (normalizedReply.StartsWith("CALL_TOOL:", StringComparison.OrdinalIgnoreCase))
            {
                if (toolExecuted)
                {
                    normalizedReply = "A tool was already executed. Provide the final answer without calling tools.";
                }
                else
                {
                    if (!TryParseToolCall(normalizedReply, out var toolName, out var argsDict, out var parseError))
                    {
                        var errorMsg = $"Bad tool call format: {parseError}";
                        _memory.AddAssistant(conversationId, errorMsg);
                        return errorMsg;
                    }

                    var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                    if (tool is null)
                        return $"Unknown tool: {toolName}";

                    var toolResult = await tool.ExecuteAsync(argsDict, cancellationToken);
                    toolExecuted = true;

                    finalPrompt = BuildFinalAnswerPrompt(conversationId, context, toolName, toolResult, question);

                    continue;
                }
            }

            _memory.AddAssistant(conversationId, normalizedReply);
            return normalizedReply;
        }

        const string failMessage = "I couldn't complete the request.";
        _memory.AddAssistant(conversationId, failMessage);
        return failMessage;
    }

    private async Task<string> RetrieveRelevantContextAsync(string question, CancellationToken cancellationToken)
    {
        var collection = await _vectorDatabase.GetCollectionAsync(CollectionName, cancellationToken);
        var similarDocuments = await collection.GetSimilarDocuments(_embeddingModel, question, amount: 5, cancellationToken: cancellationToken);
        return similarDocuments.AsString();
    }

    private string BuildToolCatalog() =>
        string.Join("\n", _tools.Select(t => $"- {t.Name}: {t.Description}"));

    private string BuildRagPrompt(Guid conversationId, string context, string question) =>
        Prompts.RagPrompt
            .Replace("{history}", _memory.RenderHistory(conversationId))
            .Replace("{context}", context)
            .Replace("{question}", question);

    private string BuildFinalAnswerPrompt(Guid conversationId, string context, string toolName, object toolResult, string question) =>
        $"""
        HISTORY:
        {_memory.RenderHistory(conversationId)}

        CONTEXT:
        {context}

        TOOL RESULT ({toolName}):
        {System.Text.Json.JsonSerializer.Serialize(toolResult)}

        USER QUESTION:
        {question}

        Using the TOOL RESULT above, provide the FINAL ANSWER now.
        Do NOT call any tool again. Be concise and accurate.
        """;

    private async Task<string> GenerateAgentReplyAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        string? content = null;
        await foreach (var reply in _openAiChatModel.GenerateAsync($$"""
            {{systemPrompt}}

            {{userPrompt}}
            """, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(reply.LastMessageContent))
                content = reply.LastMessageContent;
        }
        return (content ?? string.Empty).Trim();
    }

    private static string NormalizeAgentReply(string reply) =>
        reply.Trim('`')
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "");

    private static bool TryParseToolCall(string text, out string name, out Dictionary<string, object> args, out string error)
    {
        name = string.Empty;
        args = new Dictionary<string, object>();
        error = string.Empty;

        try
        {
            var idx = text.IndexOf(':');
            if (idx < 0) { error = "Missing ':' after CALL_TOOL"; return false; }

            var json = text[(idx + 1)..].Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                error = "Missing 'name' string property.";
                return false;
            }

            name = nameProp.GetString()!;

            if (root.TryGetProperty("args", out var argsProp) && argsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                args = argsProp.EnumerateObject()
                    .ToDictionary(p => p.Name, p => (object?)p.Value.ToString() ?? "");
            }
            else
            {
                args = new Dictionary<string, object>();
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}