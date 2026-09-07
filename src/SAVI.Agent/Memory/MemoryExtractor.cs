using System.Text.RegularExpressions;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;

namespace SAVI.Agent.Memory;

public class MemoryExtractor : IMemoryExtractor
{
    private readonly IMemoryService _memoryService;

    private static readonly Regex PreferencePattern = new(@"(?:i prefer|my preference is|always|please always|i like|i love)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ProjectPattern = new(@"(?:i am working on|i'm building|my project is|working on)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FactPattern = new(@"(?:my name is|i live in|i am based in|i am a|i work as)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExplicitRemember = new(@"(?:remember that|note that|keep in mind that)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MemoryExtractor(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public async Task<IReadOnlyList<MemoryItem>> ExtractMemoriesAsync(
        string userMessage,
        string assistantResponse,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var clean = userMessage.Trim();
        var extracted = new List<MemoryItem>();

        // 1. Explicit Remember
        var expMatch = ExplicitRemember.Match(clean);
        if (expMatch.Success)
        {
            var content = expMatch.Groups[1].Value.Trim().TrimEnd('.');
            var dto = await _memoryService.AddAsync(new CreateMemoryDto
            {
                Type = MemoryType.Preference,
                Content = $"User specified: {content}",
                Importance = 1.0
            }, conversationId, cancellationToken);

            extracted.Add(new MemoryItem { Id = dto.Id, Type = dto.Type, Content = dto.Content, Importance = dto.Importance });
            return extracted;
        }

        // 2. Preferences
        var prefMatch = PreferencePattern.Match(clean);
        if (prefMatch.Success)
        {
            var content = prefMatch.Groups[1].Value.Trim().TrimEnd('.');
            var dto = await _memoryService.AddAsync(new CreateMemoryDto
            {
                Type = MemoryType.Preference,
                Content = $"Preference: User prefers {content}",
                Importance = 0.9
            }, conversationId, cancellationToken);
            extracted.Add(new MemoryItem { Id = dto.Id, Type = dto.Type, Content = dto.Content, Importance = dto.Importance });
        }

        // 3. Projects
        var projMatch = ProjectPattern.Match(clean);
        if (projMatch.Success)
        {
            var content = projMatch.Groups[1].Value.Trim().TrimEnd('.');
            var dto = await _memoryService.AddAsync(new CreateMemoryDto
            {
                Type = MemoryType.Project,
                Content = $"Active project: {content}",
                Importance = 0.85
            }, conversationId, cancellationToken);
            extracted.Add(new MemoryItem { Id = dto.Id, Type = dto.Type, Content = dto.Content, Importance = dto.Importance });
        }

        // 4. Personal Context Facts
        var factMatch = FactPattern.Match(clean);
        if (factMatch.Success)
        {
            var content = factMatch.Groups[1].Value.Trim().TrimEnd('.');
            var dto = await _memoryService.AddAsync(new CreateMemoryDto
            {
                Type = MemoryType.PersonalContext,
                Content = $"User detail: {content}",
                Importance = 0.9
            }, conversationId, cancellationToken);
            extracted.Add(new MemoryItem { Id = dto.Id, Type = dto.Type, Content = dto.Content, Importance = dto.Importance });
        }

        return extracted;
    }
}
