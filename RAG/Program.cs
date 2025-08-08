using LangChain.Databases.Sqlite;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers;
using LangChain.Providers.Ollama;

var provider = new OllamaProvider();
var embeddingModel = new OllamaEmbeddingModel(provider, id: "all-minilm");
var llm = new OllamaChatModel(provider, id: "llama3");

var vectorDatabase = new SqLiteVectorDatabase(dataSource: "vectors.db");
const string collectionName = "dotnet";

var url = "https://raw.githubusercontent.com/dotnet-architecture/eBooks/main/current/microservices/NET-Microservices-Architecture-for-Containerized-NET-Applications.pdf";
using var http = new HttpClient();
var pdfBytes = await http.GetByteArrayAsync(url);
using var pdfStream = new MemoryStream(pdfBytes);

var vectorCollection = await vectorDatabase.AddDocumentsFromAsync<PdfPigPdfLoader>(
    embeddingModel, // Used to convert text to embeddings
    dimensions: 1536, // Should be 1536 for TextEmbeddingV3SmallModel
                      //dimensions: 384, //for all-MiniLM- 384 dimensions
    dataSource: new DataSource
    {
        Type = DataSourceType.Stream,
        Stream = pdfStream,
    },
    collectionName: collectionName,
    textSplitter: null,
    behavior: AddDocumentsToDatabaseBehavior.OverwriteExistingCollection);

const string question = "What products can work on top of RabbitMQ?";

var similarDocuments = await vectorCollection.GetSimilarDocuments(embeddingModel, question, amount: 5);

Console.WriteLine("Similar Documents:");
foreach (var document in similarDocuments)
{
    Console.WriteLine(document);
}

var answer = await llm.GenerateAsync(
    $"""
     Use the following pieces of context to answer the question at the end.
     If the answer is not in context then just say that you don't know, don't try to make up an answer.
     Keep the answer as short as possible.

     {similarDocuments.AsString()}

     Question: {question}
     Helpful Answer:
     """);

Console.WriteLine($"LLM answer: {answer}");