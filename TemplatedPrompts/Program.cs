using LangChain.Prompts;
using LangChain.Providers.OpenAI;
using LangChain.Schema;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddUserSecrets<Program>();

var config = builder.Build();
var apiKey = config["OpenAI:ApiKey"]!;

var openAiModel = new OpenAiChatModel(apiKey, "gpt-4o-mini");

var dynamicChatTemplate = new ChatPromptTemplate(new ChatPromptTemplateInput
{
    InputVariables = ["domain", "question"],
    ValidateTemplate = true,
    PromptMessages = [
        new SystemMessagePromptTemplate(new PromptTemplate(new PromptTemplateInput(
            "You are a helpful assistant specialized in {domain}.",
            ["domain"]
        ))),
        new HumanMessagePromptTemplate(new PromptTemplate(new PromptTemplateInput(
            "{question}",
            ["question"]
        )))
    ]
});

var chatMessages = await dynamicChatTemplate.FormatAsync(new InputValues(new Dictionary<string, object>
{
    ["domain"] = "travel agent",
    ["question"] = "Could you recommend me 3 travel destinations with beach, warm weather and sunny?"
}));

await foreach (var result in openAiModel.GenerateAsync(chatMessages, OpenAiChatSettings.Default))
{
    Console.WriteLine(result);
}