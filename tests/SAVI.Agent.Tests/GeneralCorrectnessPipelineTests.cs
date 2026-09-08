using SAVI.Agent.Synthesis;
using SAVI.Agent.Understanding;
using SAVI.Core.Models;
using Xunit;

namespace SAVI.Agent.Tests;

public sealed class GeneralCorrectnessPipelineTests
{
    [Fact]
    public void UnseenEntity_IsMatchedFromEvidenceWithoutEntitySpecificRules()
    {
        var analysis = new QueryUnderstandingService().Analyze("What is Rust and why is it used?");
        var evidence = new ProviderEvidence
        {
            Title = "Rust (programming language)",
            Content = "Rust is a systems programming language focused on memory safety and performance.",
            Confidence = 0.9
        };

        var evaluated = new EvidenceEvaluator().Evaluate(analysis, evidence);

        Assert.True(evaluated.IsRelevant);
        Assert.True(evaluated.RelevanceScore > 0.35);
    }

    [Fact]
    public void AmbiguousEvidence_IsRejectedWhenItAnswersAnotherMeaning()
    {
        var analysis = new QueryUnderstandingService().Analyze("What is Rust used for in software?");
        var evidence = new ProviderEvidence
        {
            Title = "Rust (corrosion)",
            Content = "Rust is the reddish-brown coating that forms on iron in the presence of oxygen and moisture.",
            Confidence = 0.9
        };

        var evaluated = new EvidenceEvaluator().Evaluate(analysis, evidence);

        Assert.False(evaluated.IsRelevant);
        Assert.NotNull(evaluated.RejectionReason);
    }

    [Fact]
    public void CurrentQuestionsDeclareFreshnessRequirement()
    {
        var analysis = new QueryUnderstandingService().Analyze("What is the latest exchange rate between INR and USD?");

        Assert.True(analysis.RequiresFreshness);
        Assert.Contains(analysis.InformationRequirements, requirement => requirement is "definition" or "answer");
        Assert.NotEmpty(analysis.RetrievalQueries);
    }
}
