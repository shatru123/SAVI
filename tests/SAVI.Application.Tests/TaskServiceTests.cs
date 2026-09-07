using Moq;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Application.Tests;

public class TaskServiceTests
{
    [Fact]
    public async Task CreateTaskAsync_ShouldInitializeStepsAndMarkDangerousApproval()
    {
        var mockRepo = new Mock<ITaskRepository>();
        var service = new TaskService(mockRepo.Object);

        var steps = new[] { "Scan directory", "Delete temporary cache files", "Generate report" };
        var created = await service.CreateTaskAsync("Clean Build Artifacts", "Automated cleanup", steps);

        Assert.NotNull(created);
        Assert.Equal(TaskState.Running, created.State);
        Assert.Equal(3, created.Steps.Count);
        Assert.True(created.Steps[1].RequiresApproval);
        Assert.False(created.Steps[0].RequiresApproval);
    }
}
