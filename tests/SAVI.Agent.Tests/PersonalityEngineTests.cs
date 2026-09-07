using SAVI.Agent.Personality;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Agent.Tests;

public class PersonalityEngineTests
{
    private readonly PersonalityEngine _engine = new();

    [Fact]
    public void FormatResponse_Friendly_ShouldSoundWarmAndPersonal()
    {
        var response = _engine.FormatResponse("The current temperature in Tokyo is 18°C.", PersonalityMode.Friendly, Array.Empty<MemoryItem>());
        Assert.Contains("Tokyo is 18°C", response);
    }

    [Fact]
    public void FormatResponse_UserPrefersConcise_ShouldAutomaticallyAdaptToConcise()
    {
        var memories = new List<MemoryItem>
        {
            new() { Type = MemoryType.Preference, Content = "User prefers concise answers" }
        };

        var response = _engine.FormatResponse("Done. The current temperature in Tokyo is 18°C.", PersonalityMode.Friendly, memories);
        Assert.DoesNotContain("Sure, Shatru", response);
    }

    [Fact]
    public void FormatFriendlyGreeting_ShouldMentionShatru()
    {
        var greeting = _engine.FormatFriendlyGreeting();
        Assert.Contains("Shatru", greeting);
    }
}
