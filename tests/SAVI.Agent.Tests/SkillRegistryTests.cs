using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Tools.Skills;
using Xunit;

namespace SAVI.Agent.Tests;

public class TestEchoSkill : ISaviSkill
{
    public string Name => "echo";
    public string Description => "Echoes back prompt.";
    public IReadOnlyCollection<string> Capabilities => new[] { "echo" };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SkillResult.Succeeded($"Echo: {context.Prompt}"));
    }
}

public class TestFaultySkill : ISaviSkill
{
    public string Name => "faulty";
    public string Description => "Throws an unhandled exception.";
    public IReadOnlyCollection<string> Capabilities => new[] { "faulty" };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Simulated internal skill crash.");
    }
}

public class SkillRegistryTests
{
    [Fact]
    public void Register_And_Find_Skill_By_Name()
    {
        var registry = new SkillRegistry(new ISaviSkill[] { new TestEchoSkill() });

        var skill = registry.FindSkill("echo");
        Assert.NotNull(skill);
        Assert.Equal("echo", skill.Name);
    }

    [Fact]
    public void GetSkillsForCapability_Returns_Matching_Skills()
    {
        var registry = new SkillRegistry(new ISaviSkill[] { new TestEchoSkill(), new TestFaultySkill() });

        var skills = registry.GetSkillsForCapability("echo");
        Assert.Single(skills);
        Assert.Equal("echo", skills[0].Name);
    }

    [Fact]
    public async Task ExecuteSkillAsync_Success_Returns_Succeeded_Result()
    {
        var registry = new SkillRegistry(new ISaviSkill[] { new TestEchoSkill() });
        var context = new SkillContext
        {
            Prompt = "Hello SAVI",
            Parameters = new Dictionary<string, string>()
        };

        var result = await registry.ExecuteSkillAsync("echo", context);

        Assert.True(result.Success);
        Assert.Equal("Echo: Hello SAVI", result.Message);
        Assert.True(result.ExecutionTimeMs >= 0);
    }

    [Fact]
    public async Task ExecuteSkillAsync_Faulty_Skill_Is_Isolated_Without_Crashing()
    {
        var registry = new SkillRegistry(new ISaviSkill[] { new TestFaultySkill() });
        var context = new SkillContext
        {
            Prompt = "Fail please",
            Parameters = new Dictionary<string, string>()
        };

        var result = await registry.ExecuteSkillAsync("faulty", context);

        Assert.False(result.Success);
        Assert.Contains("Simulated internal skill crash.", result.Error);
    }

    [Fact]
    public async Task ExecuteSkillAsync_NonExistent_Skill_Returns_Failure()
    {
        var registry = new SkillRegistry(Array.Empty<ISaviSkill>());
        var context = new SkillContext
        {
            Prompt = "test",
            Parameters = new Dictionary<string, string>()
        };

        var result = await registry.ExecuteSkillAsync("ghost", context);

        Assert.False(result.Success);
        Assert.Contains("Skill 'ghost' was not found", result.Error);
    }
}
