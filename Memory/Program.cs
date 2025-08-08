using LangChain.Memory;
using LangChain.Providers.OpenAI;
using Microsoft.Extensions.Configuration;
using static LangChain.Chains.Chain;

var builder = new ConfigurationBuilder()
    .AddUserSecrets<Program>();

var config = builder.Build();
var apiKey = config["OpenAI:ApiKey"]!;
var openAiModel = new OpenAiChatModel(apiKey, "gpt-4o-mini");

var template = @"The following is a conversation between a helpful assistant and a user.

{history}
Human: {input}
Assistant: ";

var memory = new ConversationBufferMemory();

var chain =
    LoadMemory(memory, outputKey: "history")
    | Template(template)
    | LLM(openAiModel)
    | UpdateMemory(memory, requestKey: "input", responseKey: "text");

Console.WriteLine("Start a conversation with an assistant!");

while (true)
{
    Console.WriteLine();

    Console.Write("Human: ");
    var input = Console.ReadLine() ?? string.Empty;
    if (input == "exit")
    {
        break;
    }

    // Build a new chain by prepending the user's input to the original chain
    var currentChain = Set(input, "input")
                       | chain;

    // Get a response from the Assistant
    var response = await currentChain.RunAsync("text");

    Console.Write("Assistant: ");
    Console.WriteLine(response);

    Console.WriteLine("Memory contents: \n" + string.Join('\n', memory.ChatHistory.Messages.Select(x => $"{x.Role}: {x.Content}")));
}