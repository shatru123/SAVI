using SAVI.Core.Interfaces;

namespace SAVI.Tools;

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        foreach (var t in tools)
        {
            _tools[t.Name] = t;
        }
    }

    public void Register(ITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public ITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyCollection<ITool> GetAll() => _tools.Values.ToList();
}
