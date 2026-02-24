namespace Overflow.DataSeederService.Models;

public class LlmRequest
{
    public string model { get; set; } = "ai/smollm2";
    public LlmMessage[] messages { get; set; } = Array.Empty<LlmMessage>();
    public double temperature { get; set; } = 0.7;
    public int max_tokens { get; set; } = 1000;
}

public class LlmMessage
{
    public string role { get; set; } = "user";
    public string content { get; set; } = "";
}

public class LlmResponse
{
    public LlmChoice[]? choices { get; set; }
    public int created { get; set; }
    public string? id { get; set; }
    public string? model { get; set; }
    public LlmUsage? usage { get; set; }
}

public class LlmChoice
{
    public LlmMessage? message { get; set; }
    public int index { get; set; }
    public string? finish_reason { get; set; }
}

public class LlmUsage
{
    public int prompt_tokens { get; set; }
    public int completion_tokens { get; set; }
    public int total_tokens { get; set; }
}
