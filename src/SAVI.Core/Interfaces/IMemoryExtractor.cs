using SAVI.Core.Entities;

namespace SAVI.Core.Interfaces;

public interface IMemoryExtractor
{
    Task<IReadOnlyList<MemoryItem>> ExtractMemoriesAsync(
        string userMessage,
        string assistantResponse,
        string conversationId,
        CancellationToken cancellationToken = default);
}
