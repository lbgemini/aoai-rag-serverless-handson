using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HandsonApi;

public class Function1
{
    // Azure OpenAI �̃��f��: text-embedding-ada-002 �� Deployment Name (Model Name �ł͂Ȃ��̂Œ���)
    private static readonly string EmbeddingsDeploymentName = "text-embedding-ada-002";
    private static readonly string VectorField = "contentVector";
    private static readonly int DataCount = 3;

    private readonly OpenAIClient _openAIClient;
    private readonly SearchClient _searchClient;

    public Function1(OpenAIClient openAIClient, SearchClient searchClient)
    {
        _openAIClient = openAIClient;
        _searchClient = searchClient;
    }

    [FunctionName("vector-search")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
        ILogger log)
    {
        // �N�G��������� `query` �̒l���擾
        var query = req.Query["query"];

        if (string.IsNullOrWhiteSpace(query))
        {
            return new BadRequestResult();
        }

        // �x�N�^�[��
        var embedding = await GetEmbeddingAsync(query);
        // �x�N�^�[�����̎��s
        var searchResults = await ExecuteVectorSearchAsync(embedding);

        if (searchResults.Any())
        {
            return new OkObjectResult(searchResults);
        }

        return new NotFoundResult();
    }

    private async Task<IReadOnlyList<float>> GetEmbeddingAsync(string text)
    {
        var response = await _openAIClient.GetEmbeddingsAsync(EmbeddingsDeploymentName, new EmbeddingsOptions(text));
        return response.Value.Data[0].Embedding;
    }

    private async Task<List<IndexDocument>> ExecuteVectorSearchAsync(IReadOnlyList<float> embedding)
    {
        var searchOptions = new SearchOptions
        {
            Vectors = { new SearchQueryVector { Value = embedding.ToArray(), KNearestNeighborsCount = 3, Fields = { VectorField } } },
            Size = DataCount,
            Select = { "title", "content", "category" }
        };

        // `Azure.Search.Documents.Models.SearchDocument` �� `SearchAsync()` �� Generics �ɒ�`���ăR�[�����Ă��邪�A
        // �Ǝ��� class �� Generics �Ƃ��Ē�`���\�B
        SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(null, searchOptions);
        var searchResults = new List<IndexDocument>(DataCount);
        await foreach (var result in response.GetResultsAsync())
        {
            searchResults.Add(new IndexDocument
            {
                Title = result.Document["title"].ToString(),
                Category = result.Document["category"].ToString(),
                Content = result.Document["content"].ToString(),
                Score = result.Score
            });
        }

        return searchResults;
    }
}

/// <summary>
/// �������ʂ̃��f��
/// </summary>
/// <remarks>�������ʂ� API �� response body �ɂ� List �ŏo��</remarks>>
public class IndexDocument
{
    public string Title { get; set; }
    public string Category { get; set; }
    public string Content { get; set; }
    public double? Score { get; set; }
}