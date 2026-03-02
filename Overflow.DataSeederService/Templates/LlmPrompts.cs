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
    //  Shared HTML formatting rules (injected into every prompt)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Explicit HTML formatting rules with concrete examples.
    /// Small models default to markdown — we must show exactly what we want.
    /// </summary>
    private const string HtmlFormattingRules =
        "OUTPUT FORMAT: pure HTML only. NEVER use markdown.\n" +
        "FORBIDDEN: **bold**, *italic*, `backticks`, ```code fences```, # headings, - bullet dashes, 1. numbered text.\n\n" +
        "USE THESE HTML TAGS INSTEAD:\n" +
        "  paragraph:      <p>text</p>\n" +
        "  bold:           <strong>text</strong>\n" +
        "  italic:         <em>text</em>\n" +
        "  inline code:    <code>int.Parse()</code>\n" +
        "  code block:     <pre><code>var x = 1;\nvar y = 2;</code></pre>\n" +
        "  bullet list:    <ul><li>first item</li><li>second item</li></ul>\n" +
        "  numbered list:  <ol><li>Step one</li><li>Step two</li></ol>\n\n" +
        "EXAMPLE of correct output:\n" +
        "<p>I am getting a <code>NullReferenceException</code> when calling <code>GetUser()</code>.</p>\n" +
        "<pre><code>var user = repo.GetUser(id);\nConsole.WriteLine(user.Name); // crashes here</code></pre>\n" +
        "<p>I already tried checking for null but the error still occurs.</p>\n\n" +
        "Do NOT wrap output in ```html ... ``` or any other markdown fences.\n" +
        "Do NOT output anything except the raw HTML tags.";

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
            $"You are a software developer asking a technical question on Stack Overflow. " +
            $"You are {depthPersona}.\n" +
            $"You are asking {complexityHint} specifically about the programming technology: {tag}.\n" +
            "You must generate BOTH the question title and the question body about the SAME specific topic.\n" +
            "The body must directly elaborate on and be consistent with the title.\n" +
            "The question MUST be a real software engineering / programming question. Never generate non-technical content.\n\n" +
            HtmlFormattingRules + "\n\n" +
            depthTone;

        var user =
            $"Generate a realistic Stack Overflow question about the programming technology: {tag}.\n\n" +
            "The question MUST be technical — about code, tools, frameworks, debugging, architecture, or performance.\n\n" +
            "Output EXACTLY this format — nothing else:\n" +
            "===TITLE===\n" +
            "the question title here (plain text only, 15-120 characters, NO HTML tags, NO markdown)\n" +
            "===BODY===\n" +
            "the question body here (HTML only, as described above)\n\n" +
            "CRITICAL RULES:\n" +
            $"1. The title must be plain text — do NOT use <em>, <strong>, or any other HTML tags in the title.\n" +
            "2. The body MUST be about the EXACT same topic as the title.\n" +
            $"3. The question must be about software development related to {tag}.\n\n" +
            $"Length: {sentenceRange}\n" +
            $"Code blocks: {codeBlockHint}\n" +
            $"Style: {complexityStructure}";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.7);
    }

    // ─────────────────────────────────────────────
    //  Legacy fallback: separate title call
    // ─────────────────────────────────────────────

    public static LlmPrompt QuestionTitle(string tag)
    {
        var system = "You generate Stack Overflow question titles. Output only the title text, no HTML, no markdown, no quotes.";
        var user = $"Write a realistic Stack Overflow question title about {tag}. Specific to a concrete problem. 15-120 characters. Title only.";
        return new LlmPrompt(system, user, MaxTokens: 60, Temperature: 0.8);
    }

    // ─────────────────────────────────────────────
    //  Legacy fallback: separate content call
    // ─────────────────────────────────────────────

    public static LlmPrompt QuestionContent(string title, string tag)
    {
        var v = ContentVariability.RandomForQuestion();

        var (sentenceRange, codeBlockHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => ("2-4 sentences total.", "No code blocks unless essential.", 300),
            ContentLength.Medium => ("5-8 sentences total.", "1 code block if relevant.", 600),
            ContentLength.Long => ("10-15 sentences total.", "2-3 code blocks.", 1100),
            _ => ("5-8 sentences total.", "1 code block if relevant.", 600)
        };

        var system =
            "You write the body of a Stack Overflow question.\n\n" +
            HtmlFormattingRules;

        var user =
            $"Write the question body for this title: \"{title}\"\n\n" +
            "The body MUST be about the EXACT same topic as the title above. Do not change topic.\n\n" +
            $"Length: {sentenceRange}\n" +
            $"Code blocks: {codeBlockHint}";

        return new LlmPrompt(system, user, MaxTokens: maxTokens, Temperature: 0.7);
    }

    // ─────────────────────────────────────────────
    //  Answer
    // ─────────────────────────────────────────────

    public static LlmPrompt Answer(string questionTitle, string questionContent)
    {
        var v = ContentVariability.RandomForAnswer();

        var (lengthHint, maxTokens) = v.Length switch
        {
            ContentLength.Short => ("1-3 sentences. Be extremely concise — one direct fix.", 250),
            ContentLength.Medium => ("4-7 sentences. Explain the solution with context.", 600),
            ContentLength.Long => ("8-14 sentences. Comprehensive answer with explanation, code, caveats, alternatives.", 1100),
            _ => ("4-7 sentences.", 600)
        };

        var styleInstruction = v.Style switch
        {
            AnswerStyle.Conversational =>
                "Friendly, conversational tone. Start with something like '<p>I ran into this too — here is what fixed it for me:</p>'.",
            AnswerStyle.Formal =>
                "Professional, documentation-like tone. Be precise and structured.",
            AnswerStyle.ProsAndCons =>
                "Compare approaches using <ul> or <ol> lists with <strong>pros</strong> and <strong>cons</strong> labels.",
            AnswerStyle.StepByStep =>
                "Use a numbered <ol> list. Each <li> is one concrete step.",
            AnswerStyle.CodeHeavy =>
                "Lead with a working <pre><code> example. Keep prose minimal — let the code speak.",
            AnswerStyle.Opinionated =>
                "Share your opinion based on experience. Use <p> tags. Be direct.",
            _ => "Write a helpful, clear answer using <p> tags."
        };

        var depthAngle = v.Depth switch
        {
            ContentDepth.Beginner => "Explain concepts simply. Assume the reader is new.",
            ContentDepth.Intermediate => "Assume basic knowledge. Focus on the specific solution.",
            ContentDepth.Expert => "Reference advanced concepts, internals, edge cases. Deep technical insight.",
            _ => ""
        };

        var system =
            "You are an experienced developer answering a question on a Stack Overflow-like platform.\n\n" +
            HtmlFormattingRules + "\n\n" +
            "CRITICAL: Answer ONLY the specific question asked. Read the title and body carefully. Do not answer a different question.\n\n" +
            styleInstruction + "\n" +
            depthAngle;

        var user =
            $"Question title: {questionTitle}\n\n" +
            $"Question body:\n{questionContent}\n\n" +
            "Write an answer that directly solves the problem above.\n" +
            $"Length: {lengthHint}\n" +
            "Output raw HTML only — no markdown, no ``` fences, no preamble.";

        var temperature = v.Style switch
        {
            AnswerStyle.Formal => 0.4,
            AnswerStyle.Opinionated => 0.85,
            AnswerStyle.Conversational => 0.8,
            _ => 0.55
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

        return new LlmPrompt(system, user, MaxTokens: 5, Temperature: 0.1);
    }
}

