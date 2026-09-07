namespace SAVI.Core.Interfaces;

public interface IToolRegistry
{
    void Register(ITool tool);
    ITool? GetTool(string name);
    IReadOnlyCollection<ITool> GetAll();
}
