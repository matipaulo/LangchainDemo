using LangChain.Databases.Sqlite;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers.OpenAI;
using Microsoft.AspNetCore.Mvc;
using RealWorldExample.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var config = builder.Configuration;
var apiKey = config["OpenAI:ApiKey"]!;
var provider = new OpenAiProvider(apiKey);
builder.Services.AddSingleton(provider);

var openAiModel = new OpenAiChatModel(apiKey, "gpt-4o-mini");
builder.Services.AddSingleton(openAiModel);

var embeddingModel = new OpenAiEmbeddingModel(apiKey, "text-embedding-3-small");
builder.Services.AddSingleton(embeddingModel);

var vectorDatabase = new SqLiteVectorDatabase(dataSource: "vectors.db");
builder.Services.AddSingleton(vectorDatabase);

builder.Services.AddSingleton<ConversationMemory>();

builder.Services.AddMvc();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();