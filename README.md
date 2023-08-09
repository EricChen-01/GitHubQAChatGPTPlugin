# Semantic Kernel ChatGPT GitHubQA Plugin

This project is a ChatGPT plugin for interacting with GitHub. The plugin can summarize a github repository and then answer particular questions about it.
Worked in collaboration with Donovan Morgan.
## Prerequisites

- [.NET 6](https://dotnet.microsoft.com/download/dotnet/6.0) is required to run this starter.
- [Azure Functions Core Tools](https://www.npmjs.com/package/azure-functions-core-tools) is required to run this starter.
- Install the recommended extensions
  - [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp)
  - [Semantic Kernel Tools](https://marketplace.visualstudio.com/items?itemName=ms-semantic-kernel.semantic-kernel)

## Configuring the plugin

To configure the plugin, you need to provide the following information:

- Define the properties of the plugin in the [appsettings.json](./azure-function/appsettings.json.example) file. 
- Enter the API key for your AI endpoint in the [local.settings.json](./azure-function/local.settings.json) file.


### Using appsettings.json

Configure an OpenAI endpoint

1. Create a new file named `./appsettings.json` in `./azure-function/`
1. Copy [settings.json.openai-example](./config/appsettings.json.openai-example) to `./appsettings.json`
1. Edit the `kernel` object to add your OpenAI endpoint configuration
1. Edit the `aiPlugin` object to define the properties that get exposed in the ai-plugin.json file

Configure an Azure OpenAI endpoint

1. Create a new file named `./appsettings.json` in `./azure-function/`
1. Copy [settings.json.azure-example](./config/appsettings.json.azure-example) to `./appsettings.json.example`
1. Edit the `kernel` object to add your Azure OpenAI endpoint configuration
1. Edit the `aiPlugin` object to define the properties that get exposed in the ai-plugin.json file


### Using local.settings.json

1. Create a new file named `local.settings.json` in `./azure-function/`
1. Copy [local.settings.json.example](./azure-function/local.settings.json.example) to `local.settings.json`
1. Edit the `Values` object to add your OpenAI endpoint configuration in the `apiKey` property

## Running the starter

To run the Azure Functions application just hit `F5`. 

To build and run the Azure Functions application from a terminal use the following commands:

```powershell
cd azure-function
dotnet build
cd bin/Debug/net6.0
func host start  
```

To test this plugin in Microsoft's [Chat-Copilot](https://github.com/microsoft/chat-copilot) simply add `http://localhost:7071/.well-known/ai-plugin.json` to the custom plugin adder.
