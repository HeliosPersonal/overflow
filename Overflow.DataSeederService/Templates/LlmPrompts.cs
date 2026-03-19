using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Templates;

public record LlmPrompt(string SystemPrompt, string UserPrompt, int MaxTokens = 500, double Temperature = 0.7);

/// <summary>Prompt templates for the 5-step generation pipeline. All prompts request strict JSON output.</summary>
public static class LlmPrompts
{
    /// <summary>Temperature: 0.7 — creative topic variety.</summary>
    public static LlmPrompt TopicSeed(string tag, ComplexityLevel complexity = ComplexityLevel.Intermediate)
    {
        var difficultyLabel = complexity.ToPromptLabel();

        const string system =
            "You are a technical problem designer. Your job is to invent realistic, specific software problems " +
            "that a developer might encounter. You must respond with ONLY valid JSON matching the exact schema provided. " +
            "Do NOT include markdown fences, explanations, or any text outside the JSON object.";

        var user =
            $"Design a specific software problem related to the topic: \"{tag}\"\n" +
            $"Difficulty level: \"{difficultyLabel}\"\n\n" +
            "Return ONLY this JSON object (no markdown, no extra text):\n" +
            "{\n" +
            "  \"topic\": \"specific sub-topic or library name\",\n" +
            $"  \"difficulty\": \"{difficultyLabel}\",\n" +
            "  \"problem_type\": \"short description of the problem category\",\n" +
            "  \"bug_reason\": \"root cause of the issue in one sentence\",\n" +
            "  \"key_entities\": [\"entity1\", \"entity2\", \"entity3\"],\n" +
            "  \"solution_hint\": \"one sentence describing how to fix it\"\n" +
            "}\n\n" +
            "Example:\n" +
            "{\n" +
            "  \"topic\": \"python tkinter\",\n" +
            "  \"difficulty\": \"beginner\",\n" +
            "  \"problem_type\": \"ui refresh\",\n" +
            "  \"bug_reason\": \"label text not updating because StringVar is not used\",\n" +
            "  \"key_entities\": [\"Button\", \"Label\", \"StringVar\"],\n" +
            "  \"solution_hint\": \"use StringVar and configure() to update the label text\"\n" +
            "}";

        return new LlmPrompt(system, user, 300, 0.7);
    }

    /// <summary>Temperature: 0.5 — balanced creativity and structure.</summary>
    public static LlmPrompt StructuredQuestion(TopicSeedDto topic)
    {
        var entitiesHint = topic.KeyEntities.Count > 0
            ? string.Join(", ", topic.KeyEntities)
            : topic.Topic;

        const string system =
            "You are a software developer writing a StackOverflow question about a real problem you hit.\n" +
            "\n" +
            "STRICT RULES — violating these makes your output unusable:\n" +
            "1. Return ONLY valid JSON. No markdown fences. No text outside the JSON object.\n" +
            "2. Each field contains RAW TEXT ONLY — no markdown formatting, no bold, no headings.\n" +
            "3. 'context': prose only. NEVER put code inside context. 1-2 short paragraphs max.\n" +
            "4. 'code_example': plain code only. No backticks, no fences, no comments explaining the field.\n" +
            "5. 'expected_behavior' and 'actual_behavior': one sentence each.\n" +
            "6. 'title': plain text, 5-14 words, question-style like real StackOverflow titles.\n" +
            "7. NEVER generate: vote counts, '4 Answers', 'Viewed N times', usernames, badges, sidebar text.\n" +
            "8. NEVER use filler phrases: 'Hope this helps', 'Thanks in advance', 'Let me know'.";

        var user =
            $"Write a StackOverflow question about this problem:\n" +
            $"  Topic: {topic.Topic}\n" +
            $"  Difficulty: {topic.Difficulty}\n" +
            $"  Problem type: {topic.ProblemType}\n" +
            $"  Root cause: {topic.BugReason}\n" +
            $"  Key entities: {entitiesHint}\n\n" +
            "Return ONLY this JSON object:\n" +
            "{\n" +
            "  \"title\": \"plain-text title, 5-14 words\",\n" +
            "  \"context\": \"1-2 short paragraphs of prose. No code. Describe what you are doing and where it fails.\",\n" +
            "  \"code_example\": \"5-20 lines of plain code that reproduces the problem. No backticks.\",\n" +
            "  \"language\": \"language name e.g. python, csharp, javascript, go\",\n" +
            "  \"expected_behavior\": \"one sentence: what you expected\",\n" +
            "  \"actual_behavior\": \"one sentence: what actually happens\",\n" +
            "  \"tags\": [\"tag1\", \"tag2\"]\n" +
            "}\n\n" +
            "=== FEW-SHOT EXAMPLE 1 (python/tkinter) ===\n" +
            "{\n" +
            "  \"title\": \"Tkinter label text not updating after button click\",\n" +
            "  \"context\": \"I have a simple Tkinter app with a button and a label. When the button is clicked it should update the label text.\\n\\nI tried assigning directly to label.text but nothing changes on screen.\",\n" +
            "  \"code_example\": \"import tkinter as tk\\n\\nroot = tk.Tk()\\nlabel = tk.Label(root, text='Hello')\\nlabel.pack()\\n\\ndef on_click():\\n    label.text = 'Updated'\\n\\nbtn = tk.Button(root, text='Click', command=on_click)\\nbtn.pack()\\nroot.mainloop()\",\n" +
            "  \"language\": \"python\",\n" +
            "  \"expected_behavior\": \"The label shows 'Updated' after the button is clicked.\",\n" +
            "  \"actual_behavior\": \"The label still shows 'Hello' — nothing changes.\",\n" +
            "  \"tags\": [\"python\", \"tkinter\", \"gui\"]\n" +
            "}\n\n" +
            "=== FEW-SHOT EXAMPLE 2 (javascript/promises) ===\n" +
            "{\n" +
            "  \"title\": \"How to avoid deeply nested .then() chains with multiple fetch calls\",\n" +
            "  \"context\": \"I have three sequential API calls where each one depends on the result of the previous. The code works but the nested .then() callbacks are getting very hard to follow.\\n\\nI want a cleaner way to write this without switching to a completely different pattern.\",\n" +
            "  \"code_example\": \"fetch('/api/user')\\n  .then(r => r.json())\\n  .then(user => fetch('/api/orders?userId=' + user.id))\\n  .then(r => r.json())\\n  .then(orders => fetch('/api/items?orderId=' + orders[0].id))\\n  .then(r => r.json())\\n  .then(items => console.log(items));\",\n" +
            "  \"language\": \"javascript\",\n" +
            "  \"expected_behavior\": \"A flat, readable way to chain dependent fetch calls.\",\n" +
            "  \"actual_behavior\": \"Three levels of nested .then() callbacks that are hard to read and debug.\",\n" +
            "  \"tags\": [\"javascript\", \"promises\", \"async\"]\n" +
            "}\n\n" +
            "=== FEW-SHOT EXAMPLE 3 (csharp/ef-core) ===\n" +
            "{\n" +
            "  \"title\": \"EF Core issues N+1 queries even after adding Include()\",\n" +
            "  \"context\": \"I am loading a list of orders with their related customers. After adding Include() the app still fires a separate SQL query for every order in the loop.\\n\\nProfiling shows one query for orders and then one per customer, which is the N+1 problem.\",\n" +
            "  \"code_example\": \"var orders = await _context.Orders\\n    .Include(o => o.Customer)\\n    .ToListAsync();\\n\\nforeach (var order in orders)\\n{\\n    Console.WriteLine(order.Customer.Name);\\n}\",\n" +
            "  \"language\": \"csharp\",\n" +
            "  \"expected_behavior\": \"One SQL query with a JOIN fetches all orders and their customers.\",\n" +
            "  \"actual_behavior\": \"One query for orders plus a separate query per customer — N+1 queries total.\",\n" +
            "  \"tags\": [\"csharp\", \"entity-framework-core\", \"linq\"]\n" +
            "}";

        return new LlmPrompt(system, user, 1200, 0.5);
    }

    /// <summary>Temperature: 0.4 — accurate and focused.</summary>
    public static LlmPrompt StructuredAnswer(QuestionGenerationDto question, AnswerStyle style,
        ComplexityLevel complexity = ComplexityLevel.Intermediate)
    {
        var styleHint = style switch
        {
            AnswerStyle.StepByStep =>
                "Provide 2-4 numbered steps in the \"fix_steps\" array explaining how the fix works.",
            AnswerStyle.CodeHeavy => "Keep explanation very short. Focus on the corrected code.",
            AnswerStyle.Conversational => "Write explanation in a friendly, clear tone.",
            AnswerStyle.Formal => "Write explanation in a professional, precise tone.",
            _ => "Write a clear, direct explanation."
        };

        var complexityHint = complexity switch
        {
            ComplexityLevel.Beginner =>
                "Keep the explanation simple and beginner-friendly. Avoid jargon. Use short, clear sentences.",
            ComplexityLevel.Advanced =>
                "Assume expert knowledge. Include deeper reasoning, edge cases, or performance considerations.",
            _ => "Write for an intermediate developer with solid fundamentals."
        };

        var codeContext = string.IsNullOrWhiteSpace(question.CodeExample)
            ? ""
            : $"\nProblematic code:\n{question.CodeExample}\nLanguage: {question.Language}";

        var questionContext =
            $"Title: {question.Title}\n" +
            (string.IsNullOrWhiteSpace(question.Context) ? "" : $"Context: {question.Context}\n") +
            codeContext +
            (string.IsNullOrWhiteSpace(question.ExpectedBehavior) ? "" : $"\nExpected: {question.ExpectedBehavior}\n") +
            (string.IsNullOrWhiteSpace(question.ActualBehavior) ? "" : $"Actual: {question.ActualBehavior}\n");

        const string system =
            "You are an experienced developer answering a StackOverflow question.\n" +
            "\n" +
            "STRICT RULES:\n" +
            "1. Return ONLY valid JSON. No markdown fences. No text outside the JSON object.\n" +
            "2. Each field contains RAW TEXT ONLY — no markdown formatting inside field values.\n" +
            "3. 'explanation': prose only. No code. 1-3 sentences on the root cause.\n" +
            "4. 'fix_steps': array of 1-5 action sentences. Each step is one sentence.\n" +
            "5. 'code_snippet': plain corrected code only. No backticks, no fences.\n" +
            "6. 'notes': one or two sentences of extra tips, or empty string.\n" +
            "7. NEVER write: 'Hope this helps', 'Let me know', 'Thanks', 'Good luck', 'Try restarting'.\n" +
            "8. NEVER generate vote counts, usernames, badges, or any StackOverflow UI text.";

        var user =
            $"Answer this StackOverflow question.\n{questionContext}\n" +
            $"Style: {styleHint}\n" +
            $"Complexity: {complexityHint}\n\n" +
            "Return ONLY this JSON object:\n" +
            "{\n" +
            "  \"explanation\": \"1-3 sentences on the root cause. No code.\",\n" +
            "  \"fix_steps\": [\"First do this.\", \"Then do that.\"],\n" +
            "  \"code_snippet\": \"corrected plain code, no backticks\",\n" +
            "  \"language\": \"same language as the question\",\n" +
            "  \"notes\": \"optional extra tip or empty string\",\n" +
            "  \"accepted\": false\n" +
            "}";

        return new LlmPrompt(system, user, 900, 0.4);
    }

    /// <summary>Temperature: 0.2 — deterministic evaluation.</summary>
    public static LlmPrompt Critic(QuestionGenerationDto question, AnswerGenerationDto answer)
    {
        const string system =
            "You are a strict technical content reviewer for a Q&A platform. " +
            "Your job is to identify quality problems in question-answer pairs. " +
            "RULES: " +
            "1. Return ONLY valid JSON. No markdown fences. No text outside the JSON. " +
            "2. Be strict: flag off-topic answers, broken code, incoherent logic, UI contamination. " +
            "3. If the answer correctly solves the stated problem and the question is clear, mark valid=true with empty issues.";

        var user =
            "Evaluate this question-answer pair. Check for:\n" +
            "- title relevance (does it match the problem described?)\n" +
            "- context clarity (is the problem clear? no code embedded in context?)\n" +
            "- code_example presence (is there a reproducible code example?)\n" +
            "- answer correctness (does it directly solve the stated problem?)\n" +
            "- code_snippet relevance (is the code snippet relevant to the problem? placeholder code like 'var x = 10' is wrong)\n" +
            "- UI contamination ('N answers', 'Viewed N times', vote counts, usernames, badges)\n" +
            "- filler phrases ('Hope this helps', 'Let me know', etc.)\n\n" +
            $"QUESTION TITLE: {question.Title}\n" +
            $"QUESTION CONTEXT: {question.Context}\n" +
            $"QUESTION CODE ({question.Language}):\n{question.CodeExample}\n" +
            $"EXPECTED: {question.ExpectedBehavior}\n" +
            $"ACTUAL: {question.ActualBehavior}\n\n" +
            $"ANSWER EXPLANATION: {answer.Explanation}\n" +
            $"ANSWER FIX STEPS: {string.Join(" | ", answer.FixSteps)}\n" +
            $"ANSWER CODE ({answer.Language}):\n{answer.CodeSnippet}\n\n" +
            "Return ONLY:\n" +
            "{ \"valid\": true, \"issues\": [] }\n" +
            "If invalid: { \"valid\": false, \"issues\": [\"specific issue\"] }";

        return new LlmPrompt(system, user, 250, 0.2);
    }

    /// <summary>Temperature: 0.3 — targeted corrections only.</summary>
    public static LlmPrompt Repair(QuestionGenerationDto question, AnswerGenerationDto answer,
        CriticResultDto critic)
    {
        var issuesList = string.Join("\n", critic.Issues.Select(i => $"- {i}"));

        const string system =
            "You are a technical content editor. Fix quality problems in StackOverflow-style Q&A pairs. " +
            "RULES: " +
            "1. Return ONLY valid JSON. No markdown fences. No text outside the JSON. " +
            "2. Fix ONLY the identified issues. Do not rewrite content unnecessarily. " +
            "3. All code fields must contain raw code only — no fences, no backticks.";

        var user =
            $"ISSUES TO FIX:\n{issuesList}\n\n" +
            "ORIGINAL QUESTION:\n" +
            $"  title: {question.Title}\n" +
            $"  context: {question.Context}\n" +
            $"  code_example ({question.Language}): {question.CodeExample}\n" +
            $"  expected_behavior: {question.ExpectedBehavior}\n" +
            $"  actual_behavior: {question.ActualBehavior}\n" +
            $"  tags: {string.Join(", ", question.Tags)}\n\n" +
            "ORIGINAL ANSWER:\n" +
            $"  explanation: {answer.Explanation}\n" +
            $"  fix_steps: {string.Join(" | ", answer.FixSteps)}\n" +
            $"  code_snippet ({answer.Language}): {answer.CodeSnippet}\n" +
            $"  notes: {answer.Notes}\n\n" +
            "Return ONLY this JSON with corrected values:\n" +
            "{\n" +
            "  \"question\": {\n" +
            "    \"title\": \"\", \"context\": \"\", \"code_example\": \"\",\n" +
            "    \"language\": \"\", \"expected_behavior\": \"\", \"actual_behavior\": \"\",\n" +
            "    \"tags\": []\n" +
            "  },\n" +
            "  \"answer\": {\n" +
            "    \"explanation\": \"\", \"fix_steps\": [], \"code_snippet\": \"\",\n" +
            "    \"language\": \"\", \"notes\": \"\", \"accepted\": false\n" +
            "  }\n" +
            "}";

        return new LlmPrompt(system, user, 900, 0.3);
    }


    public static LlmPrompt SelectBestAnswer(string questionTitle, List<string> answers)
    {
        var answersText = string.Join("\n\n---\n\n", answers.Select((a, i) => $"Answer {i}: {a}"));
        var system = "You evaluate technical answers. Respond with ONLY a number.";
        var user =
            $"Question: {questionTitle}\n\n{answersText}\n\n" +
            $"Which answer (0-{answers.Count - 1}) is best? Reply with the number only.";

        return new LlmPrompt(system, user, 5, 0.1);
    }
}