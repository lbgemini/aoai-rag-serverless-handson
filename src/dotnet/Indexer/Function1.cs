using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using System.Threading.Tasks;

namespace Indexer;

public class Function1
{
    // Azure OpenAI �̃��f��: text-embedding-ada-002 �� Deployment Name (Model Name �ł͂Ȃ��̂Œ���)
    private const string EmbeddingsDeploymentName = "text-embedding-ada-002";

    private readonly OpenAIClient _openAIClient;
    private readonly SearchClient _searchClient;

    public Function1(OpenAIClient openAIClient, SearchClient searchClient)
    {
        _openAIClient = openAIClient;
        _searchClient = searchClient;
    }

    [FunctionName("Function1")]
    public async Task Run([CosmosDBTrigger(
            databaseName: "handson",
            containerName: "azure",
            Connection = "CosmosConnection",
            CreateLeaseContainerIfNotExists = true,  // false ����
            LeaseContainerName = "leases")]IReadOnlyList<AzureInfo> input,
        ILogger log)
    {
        var documentsToUpload = new List<IndexDocument>();

        foreach (var azureInfo in input)
        {
            // content �̒l���x�N�^�[��
            var contentVector = await GetEmbeddingAsync(azureInfo.Content);

            // Cognitive Search �̃C���f�b�N�X�̃X�L�[�}�֕ϊ�
            documentsToUpload.Add(new IndexDocument
            {
                Id = azureInfo.Id,
                Category = azureInfo.Category,
                Title = azureInfo.Title,
                Content = azureInfo.Content,
                ContentVector = contentVector
            });
        }

        // Cognitive Search �̃C���f�b�N�X�X�V
        await _searchClient.MergeOrUploadDocumentsAsync(documentsToUpload);
        log.LogInformation($"{documentsToUpload.Count} document(s) uploaded.");
    }

    private async Task<IReadOnlyList<float>> GetEmbeddingAsync(string text)
    {
        var response = await _openAIClient.GetEmbeddingsAsync(EmbeddingsDeploymentName, new EmbeddingsOptions(text));
        return response.Value.Data[0].Embedding;
    }
}

/// <summary>
/// Cosmos DB: azure �R���e�i�[�̃X�L�[�}
/// </summary>
public record AzureInfo(string Id, string Category, string Title, string Content);

/// <summary>
/// Cognitive Search �� Index �X�V�p�X�L�[�}
/// </summary>
public class IndexDocument
{
    public string Id { get; set; }
    public string Category { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public IReadOnlyList<float> ContentVector { get; set; }
}