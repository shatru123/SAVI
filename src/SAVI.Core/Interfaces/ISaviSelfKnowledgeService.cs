using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface ISaviSelfKnowledgeService
{
    SaviSystemProfile GetSystemProfile();
    bool IsSelfKnowledgeQuery(string query, ContextPackage? context = null);
    SelfKnowledgeMatch? MatchQuery(string query, ContextPackage? context = null);
    SelfKnowledgeAnswer? GetAnswer(string query, ContextPackage? context = null);
}
