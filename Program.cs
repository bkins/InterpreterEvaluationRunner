
using InterpreterEvaluationRunner;
using InterpreterEvaluationRunner.Interpreter.Pipeline;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddFilter("System.Net.Http.HttpClient"
                        , LogLevel.Warning);

builder.Services.AddHttpClient<IModelClient, OllamaClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");

    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<NormalizationLayer>();
builder.Services.AddSingleton<ResultScorer>();
builder.Services.AddSingleton<ResultExporter>();
builder.Services.AddSingleton<IEvaluationRunner, EvaluationRunner>();

builder.Services.AddSingleton<IResponseRepairEngine, ResponseRepairEngine>();
builder.Services.AddSingleton<INormalizationLayer, NormalizationLayer>();
builder.Services.AddSingleton<IContractValidator, InterpreterResponseValidator>();
builder.Services.AddSingleton<IInterpreterPipeline, InterpreterPipeline>();

//Rules
builder.Services.AddSingleton<IRepairRule, MarkdownFenceRemovalRule>();
builder.Services.AddSingleton<IRepairRule, ToolCallNoiseRemovalRule>();
builder.Services.AddSingleton<IRepairRule, LeadingZeroDecimalRepairRule>();
builder.Services.AddSingleton<IRepairRule, ExtractFirstJsonObjectRule>();

var host   = builder.Build();
var runner = host.Services.GetRequiredService<IEvaluationRunner>();

#if DEBUG
    await runner.RunAsync(1);
#else
    await runner.RunAsync();
#endif

