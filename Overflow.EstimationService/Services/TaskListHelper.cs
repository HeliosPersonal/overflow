using System.Text.Json;

namespace Overflow.EstimationService.Services;

internal static class TaskListHelper
{
    public static List<string>? ParseTasks(string? tasksJson)
    {
        if (string.IsNullOrWhiteSpace(tasksJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tasksJson);
        }
        catch
        {
            return null;
        }
    }

    public static string? SerializeTasks(List<string>? tasks)
    {
        if (tasks is not { Count: > 0 })
            return null;

        var cleaned = tasks.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
        return cleaned.Count > 0 ? JsonSerializer.Serialize(cleaned) : null;
    }

    public static string? GetTaskName(string? tasksJson, int roundNumber)
    {
        var tasks = ParseTasks(tasksJson);
        return tasks is { Count: > 0 } && roundNumber <= tasks.Count
            ? tasks[roundNumber - 1]
            : null;
    }
}