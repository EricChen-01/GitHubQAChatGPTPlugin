// Copyright (c) Microsoft. All rights reserved.

using AIPlugins.AzureFunctions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Models;
using Microsoft.SemanticKernel.Skills.Core;
using Microsoft.SemanticKernel.Memory;

const string DefaultSemanticFunctionsFolder = "Prompts";
string semanticFunctionsFolder = Environment.GetEnvironmentVariable("SEMANTIC_SKILLS_FOLDER") ?? DefaultSemanticFunctionsFolder;

var memory = new VolatileMemoryStore();

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services
            .AddScoped<IKernel>((providers) =>
            {
                // This will be called each time a new Kernel is needed

                // Get a logger instance
                ILogger<IKernel> logger = providers
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger<IKernel>();

                // Register your AI Providers...
                var appSettings = AppSettings.LoadSettings();
                KernelBuilder kernelBuilder = new KernelBuilder()
                    .WithChatCompletionService(appSettings.Kernel)
                    .WithMemoryStorage(memory)
                    .WithLogger(logger);

                // If you're AI provider is Azure OpenAI    
                if(appSettings.Kernel.ServiceType.ToUpperInvariant() == ServiceTypes.AzureOpenAI)
                {
                    kernelBuilder = kernelBuilder    
                    .WithAzureTextEmbeddingGenerationService("text-embedding-ada-002",appSettings.Kernel.Endpoint,appSettings.Kernel.ApiKey);
                }
                // If you're AI provider is OpenAI
                else if(appSettings.Kernel.ServiceType.ToUpperInvariant() == ServiceTypes.OpenAI)
                {
                    kernelBuilder = kernelBuilder    
                    .WithOpenAITextEmbeddingGenerationService(appSettings.Kernel.DeploymentOrModelId,appSettings.Kernel.ApiKey, appSettings.Kernel.OrgId, appSettings.Kernel.ServiceId);
                }

                IKernel kernel =  kernelBuilder.Build();

                // Load your semantic functions...
                kernel.ImportPromptsFromDirectory(appSettings.AIPlugin.NameForModel, semanticFunctionsFolder);
                
                return kernel;
            })
            .AddScoped<IAIPluginRunner, AIPluginRunner>();
    })
    .Build();

host.Run();
