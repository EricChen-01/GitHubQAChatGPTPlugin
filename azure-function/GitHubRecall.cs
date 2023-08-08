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
    [OpenApiParameter(name: "collection", Description = "Memories collection to search", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Recalls information from memory about a preloaded github repository.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    [Function("GitHubMemoryQuery")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous,"post")] HttpRequestData req,CancellationToken cancellationToken)
    {
        var result = "";
        



        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type","text/plain");
        response.WriteString($"{result}");
        return response;
    }


    #region private
    
    #endregion
}