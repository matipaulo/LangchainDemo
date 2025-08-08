using LangChain.Chains.LLM;
using LangChain.Chains.Sequentials;
using LangChain.Prompts;
using LangChain.Providers.OpenAI;
using LangChain.Schema;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddUserSecrets<Program>();

var config = builder.Build();
var apiKey = config["OpenAI:ApiKey"]!;
var openAiModel = new OpenAiChatModel(apiKey, "gpt-4o-mini");

var nameChain = "Generate a funny pet name for a {petType}.";
var firstPrompt = new PromptTemplate(new PromptTemplateInput(nameChain, new List<string>(1) { "petType" }));
var chainOne = new LlmChain(new LlmChainInput(openAiModel, firstPrompt)
{
    Verbose = true,
    OutputKey = "petName"
});

var catchyPhraseChain = "Create a catchy phrase for a pet named '{petName}'.";
var secondPrompt = new PromptTemplate(new PromptTemplateInput(catchyPhraseChain, new List<string>(1) { "petName" }));
var chainTwo = new LlmChain(new LlmChainInput(openAiModel, secondPrompt)
{
    Verbose = true
});

var overallChain = new SequentialChain(new SequentialChainInput(
    [
        chainOne,
        chainTwo
    ],
    ["petType"],
    ["petName", "text"]
));

var result = await overallChain.CallAsync(new ChainValues(new Dictionary<string, object>(1)
{
    { "petType", "dog" },
    {"petName", "text"}
}));