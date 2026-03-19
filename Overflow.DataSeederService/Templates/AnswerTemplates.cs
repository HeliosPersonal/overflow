namespace Overflow.DataSeederService.Templates;

/// <summary>
///     Fallback answer templates used when LLM is unavailable.
/// </summary>
public static class AnswerTemplates
{
    private static readonly List<string> Answers = new()
    {
        "<p>Just use <code>int.TryParse()</code> — it returns <code>false</code> instead of throwing if the input is invalid.</p>",

        "<p>Add <code>await</code> before the call and make the method <code>async Task</code>. That should fix it.</p>",

        "<p>You're missing the dependency array in <code>useEffect</code>. Add <code>[]</code> as the second argument to run it only once on mount.</p>",

        "<p>Use <code>docker logs -f container_name</code> to tail the logs in real time.</p>",

        "<p>The issue is you're comparing references, not values. Use <code>.equals()</code> for content comparison.</p>",

        "<p>Use <code>const</code> by default, <code>let</code> when you need reassignment, never <code>var</code>.</p>",

        "<p>I ran into this exact same issue. What worked for me was wrapping it in a <code>try-catch</code> and checking the inner exception.</p><pre><code>try {\n    await DoSomethingAsync();\n} catch (AggregateException ex) {\n    Console.WriteLine(ex.InnerException?.Message);\n}</code></pre>",

        "<p>The recommended approach is to use <code>async/await</code> with proper cancellation token propagation:</p><ol><li>Mark the method with <code>async</code></li><li>Return <code>Task</code> or <code>Task&lt;T&gt;</code></li><li>Await all async calls</li><li>Pass <code>CancellationToken</code> to downstream operations</li></ol>",

        "<p>Here's how to solve this step by step:</p><ol><li>Install the package: <code>npm install lodash.debounce</code></li><li>Import it: <code>import debounce from 'lodash.debounce'</code></li><li>Wrap your handler with debounce</li><li>Clean up on unmount with <code>debouncedFn.cancel()</code></li></ol>",

        "<p>Here's a working implementation:</p><pre><code>public static async Task&lt;T&gt; RetryAsync&lt;T&gt;(\n    Func&lt;Task&lt;T&gt;&gt; action, int maxRetries = 3)\n{\n    for (int i = 0; i &lt;= maxRetries; i++)\n    {\n        try { return await action(); }\n        catch when (i &lt; maxRetries)\n        {\n            await Task.Delay(1000 * (i + 1));\n        }\n    }\n    throw new Exception(\"Unreachable\");\n}</code></pre>",

        "<p>Switch to an <code>alpine</code> base image and add a <code>.dockerignore</code> file. We went from 1.8GB to 200MB with just those two changes.</p><p>Also, put your <code>COPY package*.json</code> and <code>RUN npm install</code> BEFORE copying source code — Docker's layer caching will save rebuild time.</p>",

        "<p>For your case with 20 components and moderate complexity, I'd start with Context + <code>useReducer</code> and only reach for Redux if you need middleware or devtools.</p>"
    };

    public static string GetRandomAnswer()
    {
        return Answers[Random.Shared.Next(Answers.Count)];
    }
}