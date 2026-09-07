using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.Documents;

public class DocumentReaderTool : ITool
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".csv", ".xml", ".yaml", ".yml", ".cs", ".html", ".log"
    };

    public string Name => "document_reader";
    public string Description => "Inspects, reads, and extracts structured text from documents and data files.";
    public PermissionLevel PermissionLevel => PermissionLevel.Safe;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var path = input.Arguments.GetValueOrDefault("path") ?? "";
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolExecutionResult.Failed(Name, "File path is required.");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return ToolExecutionResult.Failed(Name, $"Document file does not exist: {path}");
        }

        var ext = Path.GetExtension(fullPath);
        if (!SupportedExtensions.Contains(ext))
        {
            return ToolExecutionResult.Failed(Name, $"Unsupported document format '{ext}'. Supported formats: {string.Join(", ", SupportedExtensions)}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var lineCount = content.Split('\n').Length;
            var summary = $"Document: {Path.GetFileName(fullPath)} ({lineCount} lines, {content.Length} characters)\n\n";

            if (content.Length > 15000)
            {
                content = content[..14900] + "\n... [Remaining content omitted]";
            }

            return ToolExecutionResult.Succeeded(Name, summary + content);
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failed(Name, $"Error reading document: {ex.Message}");
        }
    }
}
