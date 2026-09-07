using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface ISearchProvider
{
    string Name { get; }
    Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}
