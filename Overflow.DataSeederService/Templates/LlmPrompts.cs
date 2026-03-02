using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Templates;

/// <summary>
/// Holds the system + user prompt pair and generation parameters for an LLM request.
/// </summary>
public record LlmPrompt(string SystemPrompt, string UserPrompt, int MaxTokens = 500, double Temperature = 0.7);

/// <summary>
/// Centralized LLM prompt templates with variability-driven generation.
/// All prompt construction logic lives here — LlmClient only handles HTTP.
/// </summary>
public static class LlmPrompts
{
    // ─────────────────────────────────────────────
    //  Question (Title + Content in a single call)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Generates both the title AND body of a question in a single LLM call
    /// so they are guaranteed to be about the same topic.
    /// The response format uses a ===TITLE=== / ===BODY=== separator.
    /// </summary>
    public static LlmPrompt QuestionTitleAndContent(string tag, ContentVariability v)
    {
        var depthPersona = v.Depth switch
        {
            ContentDepth.Beginner => "a junior developer who just started learning",
            ContentDepth.Intermediate => "a mid-level developer with a few years of experience",
            ContentDepth.Expert => "a senior/staff engineer working on complex production systems",
            _ => "a developer"
        };

        var complexityHint = v.Complexity switch
        {
            ContentComplexity.Simple => "a simple, straightforward how-to question",
            ContentComplexity.Moderate => "a moderately complex question involving multiple concepts",
            ContentComplexity.Complex =>
                "a complex question about architecture, performance, edge cases, or debugging a subtle issue",
            _ => "a technical question"
        };

        var (sentenceRange, codeBlockHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => (
                "2-4 sentences total. Be very brief.",
                "Do NOT include code blocks unless absolutely essential.",
                400),
            ContentLength.Medium => (
                "5-8 sentences total.",
                "Include 1 code block if relevant.",
                700),
            ContentLength.Long => (
                "10-15 sentences total. Be thorough and detailed.",
                "Include 2-3 code blocks showing your setup, what you tried, and the error/unexpected output.",
                1200),
            _ => ("5-8 sentences total.", "Include 1 code block if relevant.", 700)
        };

        var depthTone = v.Depth switch
        {
            ContentDepth.Beginner =>
                "Write as a beginner — you may not know the correct terminology. Show confusion and ask for explanation. Avoid jargon.",
            ContentDepth.Intermediate =>
                "Write as someone who understands the basics but is stuck on a specific issue. Use proper terminology.",
            ContentDepth.Expert =>
                "Write as an experienced developer. Reference specific versions, configurations, or internals. Show you've already researched the issue deeply.",
            _ => ""
        };

        var complexityStructure = v.Complexity switch
        {
            ContentComplexity.Simple =>
                "Ask a single focused question — e.g., 'How do I do X?' or 'What does Y mean?'",
            ContentComplexity.Moderate =>
                "Describe a specific scenario where something doesn't work as expected. Mention what you expected vs what happened.",
            ContentComplexity.Complex =>
                "Describe a multi-layered problem — e.g., performance issue under load, race condition, architectural trade-off, or migration challenge. Mention environment details, constraints, and what you've already tried.",
            _ => ""
        };

        var system =
            $"You are {depthPersona} asking {complexityHint} on a Stack Overflow-like platform.\n" +
            "You must generate BOTH the question title and the question body about the SAME specific topic.\n" +
            "The body must directly elaborate on and be consistent with the title. They must be about the EXACT same problem.\n" +
            "Write the body in HTML format compatible with TipTap editor.\n" +
            "Use proper HTML tags: <p> for paragraphs, <code> for inline code, <pre><code> for code blocks, " +
            "<strong> for bold, <em> for italic, <ul>/<ol>/<li> for lists.\n" +
            depthTone;

        var user =
            $"Generate a realistic Stack Overflow question about {tag}.\n\n" +
            "You MUST output exactly this format (no markdown, no extra text):\n" +
            "===TITLE===\n" +
            "<the question title, 15-120 characters, specific to a concrete problem>\n" +
            "===BODY===\n" +
            "<the question body in HTML>\n\n" +
            "CRITICAL: The body MUST be about the EXACT same topic as the title. " +
            "If the title asks about exception handling, the body must be about exception handling. " +
            "If the title asks about performance, the body must describe a performance problem.\n\n" +
            $"Length: {sentenceRange}\n" +
            $"Code: {codeBlockHint}\n" +
            $"Style: {complexityStructure}\n\n" +
            "Use <pre><code>...</code></pre> for code blocks, <code>...</code> for inline code.\n" +
            "Return ONLY the ===TITLE=== / ===BODY=== format, nothing else.";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.8);
    }

    // Keep legacy single-purpose methods as fallbacks

    public static LlmPrompt QuestionTitle(string tag)
    {
        var v = ContentVariability.RandomForQuestion();

        var depthPersona = v.Depth switch
        {
            ContentDepth.Beginner => "a junior developer who just started learning",
            ContentDepth.Intermediate => "a mid-level developer with a few years of experience",
            ContentDepth.Expert => "a senior/staff engineer working on complex production systems",
            _ => "a developer"
        };

        var complexityHint = v.Complexity switch
        {
            ContentComplexity.Simple => "a simple, straightforward how-to question",
            ContentComplexity.Moderate => "a moderately complex question involving multiple concepts",
            ContentComplexity.Complex =>
                "a complex question about architecture, performance, edge cases, or debugging a subtle issue",
            _ => "a technical question"
        };

        var system =
            $"You are {depthPersona} asking {complexityHint} on a Stack Overflow-like platform. " +
            "Generate only the question title, nothing else.";

        var user =
            $"Generate a realistic, unique Stack Overflow question title about {tag}. " +
            "The title should be specific to a concrete problem or scenario (not generic). " +
            "Between 15-120 characters. Return ONLY the title text, no quotes or extra formatting.";

        return new LlmPrompt(system, user);
    }

    public static LlmPrompt QuestionContent(string title, string tag)
    {
        var v = ContentVariability.RandomForQuestion();

        var (sentenceRange, codeBlockHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => (
                "2-4 sentences total. Be very brief.",
                "Do NOT include code blocks unless absolutely essential.",
                250),
            ContentLength.Medium => (
                "5-8 sentences total.",
                "Include 1 code block if relevant.",
                500),
            ContentLength.Long => (
                "10-15 sentences total. Be thorough and detailed.",
                "Include 2-3 code blocks showing your setup, what you tried, and the error/unexpected output.",
                1000),
            _ => ("5-8 sentences total.", "Include 1 code block if relevant.", 500)
        };

        var depthTone = v.Depth switch
        {
            ContentDepth.Beginner =>
                "Write as a beginner — you may not know the correct terminology. Show confusion and ask for explanation. Avoid jargon.",
            ContentDepth.Intermediate =>
                "Write as someone who understands the basics but is stuck on a specific issue. Use proper terminology.",
            ContentDepth.Expert =>
                "Write as an experienced developer. Reference specific versions, configurations, or internals. Show you've already researched the issue deeply.",
            _ => ""
        };

        var complexityStructure = v.Complexity switch
        {
            ContentComplexity.Simple =>
                "Ask a single focused question — e.g., 'How do I do X?' or 'What does Y mean?'",
            ContentComplexity.Moderate =>
                "Describe a specific scenario where something doesn't work as expected. Mention what you expected vs what happened.",
            ContentComplexity.Complex =>
                "Describe a multi-layered problem — e.g., performance issue under load, race condition, architectural trade-off, or migration challenge. Mention environment details, constraints, and what you've already tried.",
            _ => ""
        };

        var system =
            "You are a developer asking a technical question. Write in HTML format compatible with TipTap editor.\n" +
            "Use proper HTML tags: <p> for paragraphs, <code> for inline code, <pre><code> for code blocks, " +
            "<strong> for bold, <em> for italic, <ul>/<ol>/<li> for lists.\n" +
            depthTone;

        var user =
            $"Write the question body for this title: '{title}'. Topic: {tag}.\n\n" +
            "CRITICAL: The body MUST be about the EXACT same topic as the title. " +
            "Do NOT write about a different topic or problem.\n\n" +
            $"Length: {sentenceRange}\n" +
            $"Code: {codeBlockHint}\n" +
            $"Style: {complexityStructure}\n\n" +
            "Use <pre><code>...</code></pre> for code blocks, <code>...</code> for inline code.\n" +
            "Return ONLY the HTML content, no wrapping markdown.";

        return new LlmPrompt(system, user, MaxTokens: maxTokens);
    }

    // ─────────────────────────────────────────────
    //  Answer
    // ─────────────────────────────────────────────

    public static LlmPrompt Answer(string questionTitle, string questionContent)
    {
        var v = ContentVariability.RandomForAnswer();

        var (sentenceRange, maxTokens) = v.Length switch
        {
            ContentLength.Short => (
                "1-3 sentences. Be extremely concise — give a direct answer or one-liner fix.", 200),
            ContentLength.Medium => (
                "4-7 sentences. Explain the solution clearly with some context.", 500),
            ContentLength.Long => (
                "8-14 sentences. Provide a comprehensive answer with explanation, code examples, caveats, and alternative approaches.",
                1000),
            _ => ("4-7 sentences.", 500)
        };

        var styleInstruction = v.Style switch
        {
            AnswerStyle.Conversational =>
                "Write in a friendly, conversational tone. Use phrases like 'I ran into this too', 'What worked for me is...'",
            AnswerStyle.Formal =>
                "Write in a professional, documentation-like tone. Be precise and structured.",
            AnswerStyle.ProsAndCons =>
                "Structure your answer by comparing approaches. Use a clear pros/cons format or comparison list.",
            AnswerStyle.StepByStep =>
                "Structure your answer as numbered steps. Walk through the solution step by step.",
            AnswerStyle.CodeHeavy =>
                "Focus on code. Provide a working code example with minimal explanation — let the code speak.",
            AnswerStyle.Opinionated =>
                "Share your opinion based on experience. Mention what you prefer and why, while acknowledging alternatives.",
            _ => "Write a helpful, clear answer."
        };

        var depthAngle = v.Depth switch
        {
            ContentDepth.Beginner =>
                "Explain concepts simply. Don't assume the reader knows advanced topics.",
            ContentDepth.Intermediate =>
                "Assume the reader has basic knowledge. Focus on the specific solution.",
            ContentDepth.Expert =>
                "You can reference advanced concepts, internals, or subtle edge cases. Provide deep technical insight.",
            _ => ""
        };

        var system =
            "You are an experienced developer providing a helpful answer on a Stack Overflow-like platform.\n" +
            "Write in HTML format compatible with TipTap editor.\n" +
            "Use proper HTML tags: <p> for paragraphs, <code> for inline code, <pre><code> for code blocks, " +
            "<strong> for bold, <em> for italic, <ul>/<ol>/<li> for lists.\n" +
            "CRITICAL: Your answer MUST directly address the SPECIFIC question asked. " +
            "Read the question title AND body carefully, then answer THAT exact problem. " +
            "Do NOT provide a generic answer about a different topic.\n" +
            styleInstruction + "\n" +
            depthAngle;

        var user =
            "Read this question carefully and provide an answer that DIRECTLY addresses it:\n\n" +
            $"Title: {questionTitle}\n\nQuestion body:\n{questionContent}\n\n" +
            "Your answer MUST be relevant to the specific problem described above. " +
            "If the question asks about performance regression, answer about performance regression. " +
            "If it asks about error handling, answer about error handling. Stay on topic.\n\n" +
            $"Length: {sentenceRange}\n" +
            "Use <pre><code>...</code></pre> for code blocks, <code>...</code> for inline code.\n" +
            "Return ONLY the HTML content, no wrapping markdown.";

        var temperature = v.Style switch
        {
            AnswerStyle.Formal => 0.5,
            AnswerStyle.Opinionated => 0.9,
            AnswerStyle.Conversational => 0.85,
            _ => 0.6 + Random.Shared.NextDouble() * 0.3 // 0.6-0.9
        };

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: temperature);
    }

    // ─────────────────────────────────────────────
    //  Best Answer Selection
    // ─────────────────────────────────────────────

    public static LlmPrompt SelectBestAnswer(string questionTitle, List<string> answers)
    {
        var answersText = string.Join("\n\n---\n\n", answers.Select((a, i) => $"Answer {i}: {a}"));

        var system = "You are evaluating technical answers for quality and helpfulness.";

        var user =
            $"Question: {questionTitle}\n\nAnswers:\n{answersText}\n\n" +
            $"Which answer index (0-{answers.Count - 1}) is most helpful and accurate? Respond with ONLY the number.";

        return new LlmPrompt(system, user);
    }
}

