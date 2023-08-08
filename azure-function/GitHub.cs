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

public class GitHubQA
{
    /// <summary>
    /// The max tokens to process in a single semantic function call.
    /// </summary>
    private const int MaxTokens = 1024;

    /// <summary>
    /// The max file size to send directly to memory.
    /// </summary>
    private const int MaxFileSize = 2048;

    private readonly ISKFunction _summarizeCodeFunction;
    private readonly IKernel _kernel;
    private readonly ILogger<GitHubQA> _logger;
    private static readonly char[] s_trimChars = new char[] { ' ', '/' };

    internal const string SummarizeCodeSnippetDefinition = @"BEGIN CONTENT TO SUMMARIZE:
    {{$INPUT}}
    END CONTENT TO SUMMARIZE.

    Summarize the content in 'CONTENT TO SUMMARIZE', identifying main points.
    Do not incorporate other general knowledge.
    Summary is in plain text, in complete sentences, with no markup or tags.

    BEGIN SUMMARY:
    ";


    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubQA"/> class.
    /// </summary>
    /// <param name="kernel">Kernel instance</param>
    /// <param name="logger">Optional logger</param>
    
    public GitHubQA(IKernel kernel, ILogger<GitHubQA>? logger = null)
    {
        this._kernel = kernel;
        this._logger = logger ?? NullLogger<GitHubQA>.Instance;
        this._summarizeCodeFunction = kernel.CreateSemanticFunction(
            SummarizeCodeSnippetDefinition,
            skillName: nameof(GitHubQA),
            description: "Given a snippet of code, summarize the part of the file.",
            maxTokens: MaxTokens,
            temperature: 0.1,
            topP: 0.5);
    }


    [OpenApiOperation(operationId: "SummarizeRepository", tags: new[] { "SummarizeRepository" }, Description = "Downloads a repository and summarizes the content")]
    [OpenApiParameter(name: "URL", Description = "URL of the GitHub repository to summarize", Required = true, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "repositoryBranch", Description = "Name of the repository repositoryBranch which will be downloaded and summarized", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "searchPattern", Description = "The search string to match against the names of files in the repository", Required = false, In = ParameterLocation.Query)]
    [OpenApiParameter(name: "patToken", Description = "Personal access token for private repositories", Required = false, In = ParameterLocation.Query)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Downloads a repository and summarizes the content")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(string), Description = "Returns the error of the input.")]
    [Function("SummarizeRepository")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,CancellationToken cancellationToken)
    {
        
        var URL = req.Query["URL"];
        var repositoryBranch = req.Query["repositoryBranch"];
        var searchPattern = req.Query["searchPattern"];
        var patToken = req.Query["patToken"];
        searchPattern ??= "*.md";
        repositoryBranch ??= "main";

        string tempPath = Path.GetTempPath();
        Console.WriteLine(tempPath);
        string directoryPath = Path.Combine(tempPath, $"SK-{Guid.NewGuid()}");
        string filePath = Path.Combine(tempPath, $"SK-{Guid.NewGuid()}.zip");

        try
        {
            var repositoryUri = Regex.Replace(URL.Trim(s_trimChars), "github.com", "api.github.com/repos", RegexOptions.IgnoreCase);
            var repoBundle = $"{repositoryUri}/zipball/{repositoryBranch}";

            this._logger.LogDebug("Downloading {RepoBundle}", repoBundle);
            var headers = new Dictionary<string, string>();
            headers.Add("X-GitHub-Api-Version", "2022-11-28");
            headers.Add("Accept", "application/vnd.github+json");
            headers.Add("User-Agent", "msft-semantic-kernel-sample");
            if (!string.IsNullOrEmpty(patToken))
            {
                this._logger.LogDebug("Access token detected, adding authorization headers");
                headers.Add("Authorization", $"Bearer {patToken}");
            }

            await this.DownloadToFileAsync(repoBundle, headers, filePath, cancellationToken);

            ZipFile.ExtractToDirectory(filePath, directoryPath);

            await this.SummarizeCodeDirectoryAsync(directoryPath, searchPattern, repositoryUri, repositoryBranch, cancellationToken);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type","text/plain");
            response.WriteString($"{repositoryUri}-{repositoryBranch}");
            return response;
        }
        finally
        {
            // Cleanup downloaded file and also unzipped content
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
        
    }


    #region private
    private async Task DownloadToFileAsync(string uri, IDictionary<string, string> headers, string filePath, CancellationToken cancellationToken = default)
    {
        // Download URI to file.
        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        foreach (var header in headers)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using FileStream fileStream = File.Create(filePath);
        await contentStream.CopyToAsync(fileStream, 81920, cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Summarize a code file into an embedding
    /// </summary>
    private async Task SummarizeCodeFileAsync(string filePath, string repositoryUri, string repositoryBranch, string fileUri, CancellationToken cancellationToken = default)
    {
        string code = File.ReadAllText(filePath);

        if (code != null && code.Length > 0)
        {
            if (code.Length > MaxFileSize)
            {
                var extension = new FileInfo(filePath).Extension;

                List<string> lines;
                List<string> paragraphs;

                switch (extension)
                {
                    case ".md":
                        {
                            lines = TextChunker.SplitMarkDownLines(code, MaxTokens);
                            paragraphs = TextChunker.SplitMarkdownParagraphs(lines, MaxTokens);

                            break;
                        }
                    default:
                        {
                            lines = TextChunker.SplitPlainTextLines(code, MaxTokens);
                            paragraphs = TextChunker.SplitPlainTextParagraphs(lines, MaxTokens);

                            break;
                        }
                }

                for (int i = 0; i < paragraphs.Count; i++)
                {
            
                    await this._kernel.Memory.SaveInformationAsync(
                        "generic",//$"{repositoryUri}-{repositoryBranch}",
                        text: $"{paragraphs[i]} File:{repositoryUri}/blob/{repositoryBranch}/{fileUri}",
                        id: $"{fileUri}_{i}",
                        cancellationToken: cancellationToken);
                        
                }
            }
            else
            {
            
                
                await this._kernel.Memory.SaveInformationAsync(
                    "generic",//$"{repositoryUri}-{repositoryBranch}",
                    text: $"{code} File:{repositoryUri}/blob/{repositoryBranch}/{fileUri}",
                    id: fileUri,
                    cancellationToken: cancellationToken);

            }
        }
        
    }

    /// <summary>
    /// Summarize the code found under a directory into embeddings (one per file)
    /// </summary>
    private async Task SummarizeCodeDirectoryAsync(string directoryPath, string searchPattern, string repositoryUri, string repositoryBranch, CancellationToken cancellationToken = default)
    {
        string[] filePaths = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);

        if (filePaths != null && filePaths.Length > 0)
        {
            this._logger.LogDebug("Found {0} files to summarize", filePaths.Length);
            Console.WriteLine("Found {0} files to summarize", filePaths.Length);
            foreach (string filePath in filePaths)
            {
                var fileUri = this.BuildFileUri(directoryPath, filePath, repositoryUri, repositoryBranch);
                await this.SummarizeCodeFileAsync(filePath, repositoryUri, repositoryBranch, fileUri, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Build the file uri corresponding to the file path.
    /// </summary>
    private string BuildFileUri(string directoryPath, string filePath, string repositoryUri, string repositoryBranch)
    {
        var repositoryBranchName = $"{repositoryUri.Trim('/').Substring(repositoryUri.LastIndexOf('/'))}-{repositoryBranch}";
        return filePath.Substring(directoryPath.Length + repositoryBranchName.Length + 1).Replace('\\', '/');
    }

    #endregion
}