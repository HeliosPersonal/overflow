using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Templates;

public static class AnswerTemplates
{
    // Brief / short answers (1-3 sentences)
    private static readonly List<string> BriefAnswers = new()
    {
        "<p>Just use <code>int.TryParse()</code> — it returns <code>false</code> instead of throwing if the input is invalid.</p>",

        "<p>Add <code>await</code> before the call and make the method <code>async Task</code>. That's it.</p>",

        "<p>You're missing the dependency array in <code>useEffect</code>. Add <code>[]</code> as the second argument to run it only once on mount.</p>",

        "<p>Use <code>docker logs -f container_name</code> to tail the logs in real time.</p>",

        "<p>The issue is you're comparing references, not values. Use <code>.equals()</code> for content comparison.</p>",

        "<p>TL;DR: Use <code>const</code> by default, <code>let</code> when you need reassignment, never <code>var</code>.</p>"
    };

    // Conversational answers
    private static readonly List<string> ConversationalAnswers = new()
    {
        "<p>Oh, I ran into this exact same issue last month! What worked for me was wrapping the whole thing in a <code>try-catch</code> and checking the inner exception — turns out the real error was being swallowed.</p><p>Something like this did the trick:</p><pre><code>try {\n    await DoSomethingAsync();\n} catch (AggregateException ex) {\n    Console.WriteLine(ex.InnerException?.Message);\n}</code></pre><p>Hope that helps! Let me know if you need more details.</p>",

        "<p>Been there! The thing that finally clicked for me was thinking of closures as \"backpacks\" — the function carries the variables from its outer scope with it wherever it goes.</p><p>So when you use <code>var</code> in a loop, all the closures share the same backpack (same variable). Switch to <code>let</code> and each iteration gets its own copy. Simple as that.</p>",

        "<p>I feel your pain with Docker image sizes 😅 What made the biggest difference for us was switching to <code>alpine</code> base images and adding a proper <code>.dockerignore</code> file. We went from 1.8GB to about 200MB.</p><p>Also, pro tip: put your <code>COPY package*.json</code> and <code>RUN npm install</code> BEFORE copying the rest of your code — Docker's layer caching will save you tons of rebuild time.</p>"
    };

    // Formal / documentation-style answers
    private static readonly List<string> FormalAnswers = new()
    {
        "<p>The recommended approach for handling asynchronous operations in this context is to utilize the <code>async/await</code> pattern in combination with proper cancellation token propagation.</p><p>The implementation should follow these guidelines:</p><ol><li>Mark the method with the <code>async</code> modifier and return <code>Task</code> or <code>Task&lt;T&gt;</code></li><li>Await all asynchronous calls using the <code>await</code> keyword</li><li>Accept a <code>CancellationToken</code> parameter and pass it to all downstream async operations</li><li>Handle <code>OperationCanceledException</code> appropriately at the call site</li></ol><p>Refer to the official Microsoft documentation on <em>Asynchronous programming patterns</em> for comprehensive guidance.</p>",

        "<p>The distinction between these two approaches is significant from both a performance and correctness perspective.</p><p><strong>Option A</strong> evaluates eagerly, loading all results into memory before any further processing occurs. This is suitable for small, bounded datasets.</p><p><strong>Option B</strong> evaluates lazily, deferring execution to the database server. This is the preferred approach for queries against large tables, as it allows the database engine to optimize the query plan.</p><p>In production environments, Option B should be the default choice unless there is a specific requirement for client-side evaluation.</p>",

        "<p>The correct configuration requires the following modifications to the <code>docker-compose.yml</code> file:</p><pre><code>services:\n  app:\n    networks:\n      - backend\n  db:\n    networks:\n      - backend\n    volumes:\n      - db_data:/var/lib/postgresql/data\n\nnetworks:\n  backend:\n    driver: bridge\n\nvolumes:\n  db_data:</code></pre><p>Services on the same user-defined bridge network can resolve each other by service name. The default bridge network does not provide this DNS resolution capability.</p>"
    };

    // Step-by-step answers
    private static readonly List<string> StepByStepAnswers = new()
    {
        "<p>Here's how to solve this step by step:</p><ol><li><strong>Step 1:</strong> Install the required package: <code>npm install lodash.debounce</code></li><li><strong>Step 2:</strong> Import it in your component: <code>import debounce from 'lodash.debounce'</code></li><li><strong>Step 3:</strong> Wrap your handler:</li></ol><pre><code>const debouncedSearch = useMemo(\n  () => debounce((query) => fetchResults(query), 300),\n  []\n);</code></pre><ol start=\"4\"><li><strong>Step 4:</strong> Clean up on unmount:</li></ol><pre><code>useEffect(() => {\n  return () => debouncedSearch.cancel();\n}, [debouncedSearch]);</code></pre><p>That's it! The search will now wait 300ms after the user stops typing before making the API call.</p>",

        "<p>Follow these steps to reduce your Docker image size:</p><ol><li><strong>Switch to a slim base image:</strong> Replace <code>FROM node:20</code> with <code>FROM node:20-alpine</code></li><li><strong>Use multi-stage builds:</strong> Have a build stage and a runtime stage</li><li><strong>Add a <code>.dockerignore</code>:</strong> Exclude <code>node_modules</code>, <code>.git</code>, test files, etc.</li><li><strong>Order layers by change frequency:</strong> Copy dependency files first, then source code</li><li><strong>Clean up in the same layer:</strong> <code>RUN apt-get update && apt-get install -y X && rm -rf /var/lib/apt/lists/*</code></li></ol><p>These steps typically reduce image size by 60-80%.</p>",

        "<p>To implement the repository pattern with EF Core:</p><ol><li><strong>Define the interface:</strong></li></ol><pre><code>public interface IRepository&lt;T&gt; where T : class\n{\n    Task&lt;T?&gt; GetByIdAsync(int id);\n    Task&lt;IEnumerable&lt;T&gt;&gt; GetAllAsync();\n    Task AddAsync(T entity);\n    void Update(T entity);\n    void Delete(T entity);\n}</code></pre><ol start=\"2\"><li><strong>Implement the generic repository</strong></li><li><strong>Register in DI:</strong> <code>services.AddScoped(typeof(IRepository&lt;&gt;), typeof(Repository&lt;&gt;));</code></li><li><strong>Add a Unit of Work</strong> if you need to coordinate multiple repositories in a single transaction</li></ol><p>Note: Some argue that EF Core's <code>DbContext</code> already implements both patterns. Consider whether the abstraction is worth the added complexity in your case.</p>"
    };

    // Code-heavy answers
    private static readonly List<string> CodeHeavyAnswers = new()
    {
        "<p>Here's a working implementation:</p><pre><code>public class RetryPolicy\n{\n    public static async Task&lt;T&gt; ExecuteAsync&lt;T&gt;(\n        Func&lt;Task&lt;T&gt;&gt; action,\n        int maxRetries = 3,\n        int delayMs = 1000)\n    {\n        for (int i = 0; i &lt;= maxRetries; i++)\n        {\n            try { return await action(); }\n            catch when (i &lt; maxRetries)\n            {\n                await Task.Delay(delayMs * (i + 1));\n            }\n        }\n        throw new InvalidOperationException(\"Unreachable\");\n    }\n}</code></pre><p>Usage: <code>var result = await RetryPolicy.ExecuteAsync(() => httpClient.GetAsync(url));</code></p>",

        "<pre><code>// Custom hook for debounced value\nfunction useDebouncedValue(value, delay = 300) {\n  const [debouncedValue, setDebouncedValue] = useState(value);\n\n  useEffect(() => {\n    const timer = setTimeout(() => setDebouncedValue(value), delay);\n    return () => clearTimeout(timer);\n  }, [value, delay]);\n\n  return debouncedValue;\n}\n\n// Usage in component\nfunction SearchComponent() {\n  const [query, setQuery] = useState('');\n  const debouncedQuery = useDebouncedValue(query, 500);\n\n  useEffect(() => {\n    if (debouncedQuery) fetchResults(debouncedQuery);\n  }, [debouncedQuery]);\n\n  return &lt;input onChange={e => setQuery(e.target.value)} /&gt;;\n}</code></pre>",

        "<pre><code># Python context manager with __enter__ and __exit__\nclass DatabaseConnection:\n    def __init__(self, connection_string):\n        self.connection_string = connection_string\n        self.connection = None\n    \n    def __enter__(self):\n        self.connection = create_connection(self.connection_string)\n        return self.connection\n    \n    def __exit__(self, exc_type, exc_val, exc_tb):\n        if self.connection:\n            if exc_type:\n                self.connection.rollback()\n            else:\n                self.connection.commit()\n            self.connection.close()\n        return False  # Don't suppress exceptions\n\n# Usage\nwith DatabaseConnection('postgresql://...') as conn:\n    conn.execute('INSERT INTO ...')</code></pre><p>The <code>__exit__</code> method is always called, even if an exception occurs inside the <code>with</code> block.</p>"
    };

    // Pros/Cons comparison answers
    private static readonly List<string> ProsConsAnswers = new()
    {
        "<p>Here's a comparison of the two approaches:</p><p><strong>Context API:</strong></p><ul><li>✅ Built into React — no extra dependencies</li><li>✅ Simple API, easy to learn</li><li>✅ Great for low-frequency updates (theme, locale, auth)</li><li>❌ Re-renders all consumers on any state change</li><li>❌ No built-in middleware, devtools, or time-travel debugging</li></ul><p><strong>Redux (or Redux Toolkit):</strong></p><ul><li>✅ Excellent devtools and middleware ecosystem</li><li>✅ Predictable state updates with immutability</li><li>✅ Fine-grained subscriptions via selectors</li><li>❌ More boilerplate (though RTK reduces this significantly)</li><li>❌ Overkill for small apps</li></ul><p>For your case with 20 components and moderate complexity, I'd start with Context + <code>useReducer</code> and only reach for Redux if you need middleware or devtools.</p>",

        "<p>Both approaches have trade-offs:</p><p><strong>IEnumerable (client-side evaluation):</strong></p><ul><li>✅ Full LINQ support including custom methods</li><li>✅ Works with any data source</li><li>❌ Loads all data into memory first</li><li>❌ Filtering happens in your app, not the database</li></ul><p><strong>IQueryable (server-side evaluation):</strong></p><ul><li>✅ Translates to SQL — filtering happens in the database</li><li>✅ Only transfers matching rows over the network</li><li>❌ Limited to operations the provider can translate</li><li>❌ Deferred execution can cause surprises if the context is disposed</li></ul><p>Rule of thumb: use <code>IQueryable</code> when working with a database, <code>IEnumerable</code> when working with in-memory collections.</p>",

        "<p>Here's how the options compare:</p><table><tr><th></th><th>Docker-in-Docker</th><th>Socket Mount</th><th>Kaniko</th></tr><tr><td><strong>Security</strong></td><td>Good isolation</td><td>Poor — host access</td><td>Excellent — no daemon</td></tr><tr><td><strong>Caching</strong></td><td>Lost between builds</td><td>Persistent</td><td>Registry-based</td></tr><tr><td><strong>Setup</strong></td><td>Moderate</td><td>Easy</td><td>Moderate</td></tr><tr><td><strong>Speed</strong></td><td>Slow (no cache)</td><td>Fast</td><td>Moderate</td></tr></table><p>For CI/CD with security concerns, I'd recommend <strong>Kaniko</strong>. It doesn't need a Docker daemon and builds run as a regular process.</p>"
    };

    // Opinionated answers
    private static readonly List<string> OpinionatedAnswers = new()
    {
        "<p>Honestly, in my experience, the repository pattern on top of EF Core is mostly unnecessary abstraction. EF Core's <code>DbContext</code> already IS a unit of work, and <code>DbSet&lt;T&gt;</code> already IS a repository.</p><p>I've worked on projects with and without the extra repository layer, and the ones without it were simpler to maintain. The \"what if we switch ORMs\" argument almost never materializes in practice.</p><p>That said, if you need to abstract away for <strong>testing purposes</strong>, consider using EF Core's in-memory provider or a thin service layer instead.</p>",

        "<p>I know this might be unpopular, but I'd strongly recommend <strong>against</strong> writing your own debounce implementation. Use <code>lodash.debounce</code> or <code>use-debounce</code> from npm.</p><p>I've seen too many \"simple\" custom implementations that miss edge cases: cleanup on unmount, leading/trailing options, cancel support. The library handles all of this and it's 2KB gzipped.</p><p>Don't reinvent the wheel unless you're doing it to learn.</p>",

        "<p>Having done multiple Docker-to-Kubernetes migrations, my honest advice: <strong>skip Docker Compose for anything beyond local dev</strong>. If you're heading toward production, invest that time in learning Kubernetes or even a simpler alternative like Fly.io.</p><p>Docker Compose is great for local development, but it gives you a false sense of production readiness. Service discovery, health checks, rolling updates, secrets management — you'll have to relearn all of these for your actual orchestrator.</p>"
    };

    private static readonly Dictionary<AnswerStyle, List<string>> AnswersByStyle = new()
    {
        [AnswerStyle.Neutral] = BriefAnswers,
        [AnswerStyle.Conversational] = ConversationalAnswers,
        [AnswerStyle.Formal] = FormalAnswers,
        [AnswerStyle.StepByStep] = StepByStepAnswers,
        [AnswerStyle.CodeHeavy] = CodeHeavyAnswers,
        [AnswerStyle.ProsAndCons] = ProsConsAnswers,
        [AnswerStyle.Opinionated] = OpinionatedAnswers
    };

    public static string GetRandomAnswer()
    {
        // Pick a random style, then a random answer from that style
        var styles = Enum.GetValues<AnswerStyle>();
        var style = styles[Random.Shared.Next(styles.Length)];
        return GetRandomAnswer(style);
    }

    public static string GetRandomAnswer(AnswerStyle style)
    {
        var answers = AnswersByStyle.GetValueOrDefault(style, BriefAnswers);
        return answers[Random.Shared.Next(answers.Count)];
    }
}
