using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IQueryUnderstandingService
{
    QueryAnalysisResult Analyze(string prompt, ContextPackage? context = null);
}
