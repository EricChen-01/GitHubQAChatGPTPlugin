using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Globalization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.SemanticKernel.Skills.Core;

namespace GitHubQAPlugin;

public class GitHubRecall
{
    

    private readonly IKernel _kernel;
    private readonly ILogger<GitHubRecall> _logger;




    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubRecall"/> class.
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    /// <param name="logger">Optional logger</param>
    
    public GitHubRecall(IKernel kernel, ILogger<GitHubRecall>? logger = null)
    {
        this._kernel = kernel;
        this._logger = logger ?? NullLogger<GitHubRecall>.Instance;
    }


    [OpenApiOperation(operationId: "GitHubMemoryQuery", tags: new[] { "GitHubMemoryQuery" }, Description = "Recalls information from memory about a preloaded github repository.")]
    [OpenApiParameter(name: "input", Description = "The input text to find related memories for", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "collection", Description = "Memories collection to search. Collection is in the form https://<githublink>-<branch>.", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Recalls information from memory about a preloaded github repository.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    [Function("GitHubMemoryQuery")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous,"post")] HttpRequestData req,CancellationToken cancellationToken)
    {
        var input = req.Query["input"];
        var collection = req.Query["collection"];
        collection ??= "generic";

        var memory = new TextMemorySkill(_kernel.Memory);
        
        var relevantMemory = await memory.RecallAsync(input,collection,0,1,_logger,cancellationToken);

        var result = await GenerateRecallResponse(relevantMemory, input);

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type","text/plain");
        response.WriteString($"{result}");
        return response;
    }


    #region private

    /// <summary>
    /// Answers a following question given recalled info.
    /// </summary>
    /// <param name="recalledInfo"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    private async Task<string> GenerateRecallResponse(string recalledInfo, string input)
    {
        string RecallCodeSnippetDefinition = $"{recalledInfo}\n" + @"
        ---
        Considering only the information above, which has been loaded from a GitHub repository, answer the following.
        Question: {{$input}}

        Answer:
    ";
    
        var recall = _kernel.CreateSemanticFunction(RecallCodeSnippetDefinition);


        var response = await recall.InvokeAsync(input);

        return $"{response}";
    }
    #endregion
}