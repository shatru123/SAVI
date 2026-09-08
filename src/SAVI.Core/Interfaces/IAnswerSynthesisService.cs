using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IAnswerSynthesisService
{
    Task<SynthesizedAnswer> SynthesizeAsync(
        string userQuestion,
        QueryAnalysisResult analysis,
        IReadOnlyList<ProviderEvidence> evidenceList,
        ContextPackage context,
        bool isVoiceMode,
        CancellationToken cancellationToken = default);
}
