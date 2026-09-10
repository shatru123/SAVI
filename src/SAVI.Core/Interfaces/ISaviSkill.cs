using SAVI.Core.Enums;
using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface ISaviSkill
{
    string Name { get; }
    string Description { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    SkillPermissionLevel RequiredPermission { get; }
    Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default);
}

public interface ISkillRegistry
{
    void Register(ISaviSkill skill);
    ISaviSkill? FindSkill(string name);
    IReadOnlyList<ISaviSkill> GetSkillsForCapability(string capability);
    IReadOnlyList<ISaviSkill> GetAllSkills();
    Task<SkillResult> ExecuteSkillAsync(string name, SkillContext context, CancellationToken cancellationToken = default);
}
