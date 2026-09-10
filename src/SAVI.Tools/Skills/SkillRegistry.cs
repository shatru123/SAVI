using System.Collections.Concurrent;
using System.Diagnostics;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Tools.Skills;

public class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, ISaviSkill> _skills = new(StringComparer.OrdinalIgnoreCase);

    public SkillRegistry(IEnumerable<ISaviSkill>? initialSkills = null)
    {
        if (initialSkills != null)
        {
            foreach (var skill in initialSkills)
            {
                Register(skill);
            }
        }
    }

    public void Register(ISaviSkill skill)
    {
        _skills[skill.Name] = skill;
    }

    public ISaviSkill? FindSkill(string name)
    {
        _skills.TryGetValue(name, out var skill);
        return skill;
    }

    public IReadOnlyList<ISaviSkill> GetSkillsForCapability(string capability)
    {
        return _skills.Values
            .Where(s => s.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<ISaviSkill> GetAllSkills()
    {
        return _skills.Values.OrderBy(s => s.Name).ToList();
    }

    public async Task<SkillResult> ExecuteSkillAsync(string name, SkillContext context, CancellationToken cancellationToken = default)
    {
        var skill = FindSkill(name);
        if (skill == null)
        {
            return SkillResult.Failed($"Skill '{name}' was not found in the registry.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await skill.ExecuteAsync(context, cancellationToken);
            sw.Stop();
            return result with { ExecutionTimeMs = sw.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return SkillResult.Failed($"Skill '{name}' was cancelled.", retryable: true, timeMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return SkillResult.Failed($"Skill '{name}' failed with error: {ex.Message}", retryable: false, timeMs: sw.ElapsedMilliseconds);
        }
    }
}
