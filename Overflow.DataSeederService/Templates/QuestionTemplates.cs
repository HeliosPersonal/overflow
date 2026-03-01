using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Templates;

public static class QuestionTemplates
{
    private static readonly Dictionary<string, List<string>> TitleTemplates = new()
    {
        ["csharp"] = new()
        {
            // Beginner / Simple
            "How to implement async/await pattern in C#?",
            "What is the difference between Task and ValueTask?",
            "How to use LINQ to filter and transform collections?",
            "Understanding nullable reference types in C# 9+",
            "What does 'yield return' do in C#?",
            "How do I convert a string to an int in C#?",
            "Difference between 'ref' and 'out' parameters?",
            // Intermediate / Moderate
            "Best practices for dependency injection in .NET",
            "Difference between IEnumerable and IQueryable",
            "How to handle exceptions in async methods?",
            "How to implement the repository pattern with EF Core?",
            "Why is my CancellationToken not propagating correctly?",
            "How to properly implement IDisposable with managed and unmanaged resources?",
            "How to use Channels for producer-consumer in .NET?",
            "Expression trees vs compiled delegates: when to use which?",
            // Expert / Complex
            "Debugging a deadlock in ASP.NET Core with SemaphoreSlim and async code",
            "EF Core query plan regression after upgrading from .NET 7 to .NET 8",
            "Memory leak in long-running BackgroundService — how to profile and fix?",
            "How to implement a custom middleware pipeline with branching in ASP.NET Core?",
            "Source generators vs reflection for runtime type inspection — trade-offs?",
            "Thread-safe Singleton with lazy initialization — is double-check locking still needed in .NET 8?"
        },
        ["javascript"] = new()
        {
            "How to use async/await in JavaScript?",
            "Understanding closures in JavaScript",
            "What is the difference between let, const, and var?",
            "How to handle promises in JavaScript?",
            "How to use map, filter, and reduce effectively?",
            "Understanding event loop in JavaScript",
            "How to implement debounce and throttle?",
            "What is prototypal inheritance in JavaScript?",
            "How to deep clone an object in JavaScript?",
            "Best practices for error handling in Node.js",
            "WeakRef and FinalizationRegistry — when are they useful?",
            "How to structure a monorepo with npm workspaces?",
            "Why does my async generator leak memory under high throughput?",
            "Proxy and Reflect — how to build a reactive state system from scratch?",
            "V8 hidden classes: how object shape affects performance",
            "Streaming SSR with React 18 and Node.js — handling backpressure correctly"
        },
        ["react"] = new()
        {
            "How to manage state in React applications?",
            "When to use useEffect vs useLayoutEffect?",
            "Best practices for React component composition",
            "How to prevent unnecessary re-renders in React?",
            "Understanding React Context API",
            "How to implement custom hooks in React?",
            "Difference between controlled and uncontrolled components",
            "How to optimize React performance?",
            "How to share state between sibling components?",
            "Why does my useEffect cleanup not fire when I expect?",
            "React Server Components vs client components — when to use each?",
            "How to implement optimistic UI updates with React Query?",
            "Concurrent rendering and Suspense boundaries — handling race conditions",
            "Custom reconciler for a canvas-based React renderer",
            "Migrating a large class-component codebase to hooks — strategy?"
        },
        ["python"] = new()
        {
            "How to use list comprehensions in Python?",
            "Understanding decorators in Python",
            "Best practices for virtual environments",
            "How to handle exceptions in Python?",
            "What are Python generators and when to use them?",
            "How to use context managers effectively?",
            "Understanding *args and **kwargs",
            "How to implement async functions in Python?",
            "What is the GIL and when does it matter?",
            "How to type-hint a function that returns different types?",
            "Metaclasses vs __init_subclass__ — which to use for plugin systems?",
            "Why is my asyncio.gather() slower than sequential calls?",
            "Profiling memory usage in a Django app serving 10k requests/sec",
            "How to build a custom import hook in Python?",
            "Descriptor protocol deep dive — __get__, __set__, __delete__"
        },
        ["docker"] = new()
        {
            "How to optimize Docker image size?",
            "Best practices for Docker multi-stage builds",
            "How to handle environment variables in Docker?",
            "Understanding Docker networking basics",
            "How to debug containers effectively?",
            "What is the difference between CMD and ENTRYPOINT?",
            "How to use Docker volumes for persistence?",
            "Best practices for Docker security?",
            "How to set up health checks in Docker Compose?",
            "Why does my container exit immediately after starting?",
            "BuildKit cache mounts — how to speed up package installs?",
            "Docker-in-Docker vs socket mounting for CI pipelines",
            "Rootless Docker with user namespaces — security implications?",
            "Debugging DNS resolution failures between containers in custom bridge networks",
            "Multi-arch Docker builds for ARM64 and AMD64 — best practices?"
        }
    };

    // Short content templates (beginner-level, 2-4 sentences)
    private static readonly Dictionary<string, List<string>> ShortContentTemplates = new()
    {
        ["csharp"] = new()
        {
            "<p>I'm trying to convert a <code>string</code> to an <code>int</code> but I keep getting an exception. What's the safest way to do this?</p>",
            "<p>I see <code>async</code> and <code>await</code> everywhere in C# code. Can someone explain when I should use them and when I shouldn't? I'm new to C#.</p>",
            "<p>What's the difference between <code>==</code> and <code>.Equals()</code> in C#? I got unexpected behavior comparing two strings.</p>"
        },
        ["javascript"] = new()
        {
            "<p>I'm confused about <code>let</code> vs <code>const</code> vs <code>var</code>. When should I use each one?</p>",
            "<p>How do I check if a variable is <code>undefined</code> or <code>null</code> in JavaScript? What's the difference?</p>",
            "<p>I'm trying to copy an array but changes to the copy affect the original. How do I make a real copy?</p>"
        },
        ["react"] = new()
        {
            "<p>My React component keeps re-rendering in an infinite loop. I'm using <code>useEffect</code> but I think I'm doing something wrong. Help?</p>",
            "<p>How do I pass data from a child component back to the parent in React?</p>",
            "<p>I need to show/hide a div based on a button click. What's the React way to do this?</p>"
        },
        ["python"] = new()
        {
            "<p>How do I read a JSON file in Python? I keep getting <code>FileNotFoundError</code>.</p>",
            "<p>What's the difference between a <code>list</code> and a <code>tuple</code> in Python? When should I use each?</p>",
            "<p>I'm getting <code>IndentationError</code> but my code looks fine. What could be wrong?</p>"
        },
        ["docker"] = new()
        {
            "<p>My Docker container starts and immediately exits. How do I keep it running?</p>",
            "<p>How do I access my app running inside a Docker container from my browser?</p>",
            "<p>What's the difference between <code>docker run</code> and <code>docker exec</code>?</p>"
        }
    };

    // Medium content templates (intermediate-level, 5-8 sentences)
    private static readonly Dictionary<string, List<string>> MediumContentTemplates = new()
    {
        ["csharp"] = new()
        {
            "<p>I'm working on a .NET application and trying to understand the best way to implement asynchronous operations. I've read about async/await but I'm not sure when to use it.</p><p>Here's my current code:</p><pre><code>public void ProcessData() {\n    var data = GetData();\n    SaveData(data);\n}</code></pre><p>How should I convert this to use async/await properly? What are the benefits?</p>",
            "<p>I'm building a web API using ASP.NET Core and I want to implement dependency injection properly. I understand the basics but I'm confused about service lifetimes (<code>Singleton</code>, <code>Scoped</code>, <code>Transient</code>).</p><p>Can someone explain when to use each lifetime and provide examples? I've had issues where a scoped service was injected into a singleton and it caused bugs in production.</p>",
            "<p>I'm using Entity Framework Core and noticed that my queries are loading way more data than needed. I think it has to do with lazy loading vs eager loading.</p><p>Here's an example:</p><pre><code>var orders = _context.Orders.ToList();\nforeach (var order in orders)\n{\n    Console.WriteLine(order.Customer.Name); // N+1 problem?\n}</code></pre><p>What's the correct way to load related data efficiently?</p>"
        },
        ["javascript"] = new()
        {
            "<p>I'm new to JavaScript and trying to understand promises. I have this code that makes API calls:</p><pre><code>function getData() {\n    fetch('/api/data')\n        .then(response => response.json())\n        .then(data => console.log(data));\n}</code></pre><p>How can I convert this to use async/await? Is it better? Also, how should I handle errors?</p>",
            "<p>I'm working on a React application and I need to implement a search feature. I want to debounce the API calls to avoid making too many requests while the user types.</p><p>I tried <code>setTimeout</code> but it doesn't cancel the previous timer properly. What's the recommended approach?</p>",
            "<p>I'm confused about closures in JavaScript. I have this code:</p><pre><code>for (var i = 0; i < 5; i++) {\n    setTimeout(() => console.log(i), 1000);\n}</code></pre><p>It prints 5 five times instead of 0,1,2,3,4. Why does this happen and how do I fix it?</p>"
        },
        ["react"] = new()
        {
            "<p>I'm building a React application and my components are re-rendering too often, causing performance issues. I've heard about <code>React.memo</code> and <code>useMemo</code> but I'm not sure when to use each.</p><p>Can someone explain the difference and provide examples of when to use them?</p>",
            "<p>I'm new to React hooks and I'm trying to fetch data when a component mounts. I've tried this:</p><pre><code>function MyComponent() {\n    const [data, setData] = useState(null);\n    \n    useEffect(() => {\n        fetchData().then(setData);\n    });\n    \n    return &lt;div&gt;{data}&lt;/div&gt;;\n}</code></pre><p>But it seems to fetch continuously. What am I doing wrong?</p>",
            "<p>I need to share state between multiple components in my React app. Should I use Context API or should I look into Redux? What are the pros and cons of each approach?</p><p>My app has about 20 components and moderate complexity. I don't want to over-engineer it.</p>"
        },
        ["python"] = new()
        {
            "<p>I'm working with large datasets in Python and I'm running into memory issues. I've heard that generators can help with this.</p><p>Can someone explain how generators work and how they can reduce memory usage? A concrete example would be very helpful.</p>",
            "<p>I'm trying to understand Python decorators. I see them used everywhere but I don't understand how they work.</p><pre><code>@property\ndef name(self):\n    return self._name</code></pre><p>How do decorators work internally and how can I write my own?</p>",
            "<p>I'm working on a Python project and I need to handle file operations safely. I've heard about context managers and the <code>with</code> statement.</p><p>How do context managers work and how can I create my own? I want to make sure file handles are always closed even if an exception occurs.</p>"
        },
        ["docker"] = new()
        {
            "<p>I'm deploying my application with Docker and the image size is over 2GB. This is causing slow deployments.</p><p>What are the best practices for reducing Docker image size? I've heard about multi-stage builds but I'm not sure how to implement them for a .NET application.</p>",
            "<p>I'm trying to connect multiple Docker containers together. I have a web application and a database, and they need to communicate.</p><p>How does Docker networking work? Should I use bridge networks or something else? Here's my current <code>docker-compose.yml</code> setup:</p><pre><code>services:\n  web:\n    build: .\n    ports:\n      - \"8080:80\"\n  db:\n    image: postgres:16</code></pre>",
            "<p>I'm confused about Docker volumes vs bind mounts. When should I use each? I need my database data to persist across container restarts.</p><p>Also, what's the best practice for handling database migrations when using Docker volumes?</p>"
        }
    };

    // Long content templates (expert-level, 10+ sentences)
    private static readonly Dictionary<string, List<string>> LongContentTemplates = new()
    {
        ["csharp"] = new()
        {
            "<p>I'm experiencing intermittent deadlocks in our ASP.NET Core 8 application running in production under high load (~500 RPS). The issue seems to involve <code>SemaphoreSlim</code> used in a rate-limiting middleware combined with async database calls through EF Core.</p><p>Here's our middleware:</p><pre><code>public class RateLimitMiddleware\n{\n    private static readonly SemaphoreSlim _semaphore = new(100);\n    \n    public async Task InvokeAsync(HttpContext context, RequestDelegate next)\n    {\n        await _semaphore.WaitAsync();\n        try { await next(context); }\n        finally { _semaphore.Release(); }\n    }\n}</code></pre><p>And in our service layer we have:</p><pre><code>public async Task&lt;Order&gt; GetOrderAsync(int id)\n{\n    return await _context.Orders\n        .Include(o => o.Items)\n        .FirstOrDefaultAsync(o => o.Id == id);\n}</code></pre><p>The deadlock manifests as requests hanging indefinitely. Thread dumps show threads waiting on the semaphore while holding EF Core connections, and vice versa.</p><p>We're running on Kestrel with <code>ThreadPool.SetMinThreads(200, 200)</code> and using <code>Npgsql</code> with a connection pool size of 50. The issue only occurs when the semaphore's max count is lower than the connection pool size.</p><p>I've considered switching to <code>System.Threading.RateLimiting</code> from .NET 7+ but I want to understand the root cause first. Is this a classic sync-over-async issue, or is there something more subtle with how <code>SemaphoreSlim</code> interacts with the thread pool?</p><p>What would be the recommended architecture for rate limiting that doesn't conflict with async I/O operations?</p>",
            "<p>We recently migrated our microservices from .NET 7 to .NET 8 and noticed a significant performance regression in one of our EF Core queries. The query was running in ~50ms before the upgrade and now takes 300-800ms.</p><p>The query in question:</p><pre><code>var results = await _context.Products\n    .Where(p => p.CategoryId == categoryId)\n    .Where(p => p.Price >= minPrice && p.Price <= maxPrice)\n    .OrderBy(p => p.Name)\n    .Skip(page * pageSize)\n    .Take(pageSize)\n    .Select(p => new ProductDto\n    {\n        Id = p.Id,\n        Name = p.Name,\n        Price = p.Price,\n        CategoryName = p.Category.Name\n    })\n    .ToListAsync();</code></pre><p>I've checked the generated SQL and it's nearly identical. The execution plan in PostgreSQL shows the same index usage. I've compared the Npgsql versions (7.x vs 8.x) and noticed changes in parameter batching behavior.</p><p>I've also profiled with <code>dotnet-trace</code> and see more time spent in <code>Npgsql.Internal.NpgsqlConnector.ReadMessage</code>. Our connection string includes <code>Multiplexing=true</code>.</p><p>Things I've tried:</p><ul><li>Disabling multiplexing — no improvement</li><li>Pinning Npgsql to 7.x — performance returns to normal</li><li>Running the raw SQL via <code>FromSqlRaw</code> — same slow performance</li></ul><p>Has anyone seen similar regression? Is this a known issue with Npgsql 8.x or EF Core 8 query compilation?</p>"
        },
        ["javascript"] = new()
        {
            "<p>I'm building a real-time collaborative editor (similar to Google Docs) using JavaScript and WebSockets. I've implemented a basic CRDT (Conflict-free Replicated Data Type) for text operations, but I'm running into issues with operational transformation when multiple users type simultaneously in the same paragraph.</p><p>My current approach uses a simple sequence CRDT:</p><pre><code>class TextCRDT {\n  constructor(siteId) {\n    this.siteId = siteId;\n    this.counter = 0;\n    this.chars = [];\n  }\n  \n  insert(char, index) {\n    const id = { site: this.siteId, clock: ++this.counter };\n    const newChar = { id, value: char, visible: true };\n    this.chars.splice(index, 0, newChar);\n    return { type: 'insert', char: newChar, index };\n  }\n}</code></pre><p>The problem is that when two users insert text at the same position, the order becomes inconsistent across clients. My <code>site ID</code> comparison works for tie-breaking, but the resulting text doesn't match what either user intended.</p><p>I've looked at Yjs and Automerge, but I want to understand the fundamentals before using a library. Specifically:</p><ol><li>How do you handle position mapping when operations arrive out of order?</li><li>Is there a simpler approach than full OT for a text editor with ~10 concurrent users?</li><li>How do you handle undo/redo in a CRDT context?</li></ol><p>Our current stack is Node.js + WebSocket (ws library) + React on the frontend. We're storing document snapshots in MongoDB and replaying ops on reconnect.</p><p>Any guidance on the algorithm side would be hugely appreciated.</p>"
        },
        ["react"] = new()
        {
            "<p>I'm migrating a large React application (200+ components) from class components to functional components with hooks. The app was originally built in 2019 and uses Redux heavily with <code>connect()</code> HOCs, lifecycle methods, and some component inheritance patterns.</p><p>The main challenges I'm facing:</p><ol><li><strong>Higher-Order Components (HOCs) stacking</strong> — some components are wrapped in 3-4 HOCs (<code>connect</code>, <code>withRouter</code>, <code>withTheme</code>, custom HOCs). Converting these to hooks isn't straightforward.</li><li><strong>Class component inheritance</strong> — we have a <code>BaseFormComponent</code> that about 30 form components extend. It provides validation, dirty checking, and submission logic.</li><li><strong><code>componentDidCatch</code></strong> — we use error boundaries extensively and there's no hook equivalent.</li></ol><p>My current approach is:</p><pre><code>// Old pattern\nclass UserForm extends BaseFormComponent {\n  validate() { /* ... */ }\n  render() { return &lt;form&gt;...&lt;/form&gt;; }\n}\nexport default connect(mapState, mapDispatch)(withRouter(UserForm));\n\n// New pattern - but how to handle the base class?\nfunction UserForm() {\n  const dispatch = useDispatch();\n  const navigate = useNavigate();\n  const { validate, isDirty, handleSubmit } = useForm(/* ??? */);\n  return &lt;form&gt;...&lt;/form&gt;;\n}</code></pre><p>Questions:</p><ul><li>What's the best strategy for migrating incrementally without breaking everything?</li><li>How do you replace class inheritance patterns with hooks?</li><li>Should I keep error boundaries as class components or is there a better pattern?</li><li>Is there a recommended order of migration (leaf components first? most-used first?)</li></ul><p>We have about 40% test coverage with Enzyme (also needs migration to React Testing Library). I'm looking for a battle-tested strategy that others have successfully used at this scale.</p>"
        },
        ["python"] = new()
        {
            "<p>I'm running a Django application that serves approximately 10,000 requests per second in production. We recently noticed that memory usage has been steadily climbing over 24-48 hours until the workers get OOM-killed by Kubernetes.</p><p>Our setup:</p><ul><li>Django 5.0 + Gunicorn (gevent workers)</li><li>PostgreSQL via <code>psycopg2</code> with <code>CONN_MAX_AGE=600</code></li><li>Redis for caching (django-redis)</li><li>Celery for background tasks</li></ul><p>I've tried profiling with <code>tracemalloc</code> and <code>objgraph</code>:</p><pre><code>import tracemalloc\ntracemalloc.start(25)\n# ... after many requests ...\nsnapshot = tracemalloc.take_snapshot()\nfor stat in snapshot.statistics('lineno')[:10]:\n    print(stat)</code></pre><p>The top allocators point to Django's query logging (<code>connection.queries</code>) and some third-party middleware. But even after disabling <code>DEBUG</code> and removing the middleware, memory still grows, just slower.</p><p>Using <code>objgraph.show_growth()</code> shows a steady increase in <code>dict</code> and <code>list</code> objects, but I can't trace them back to a specific leak. I suspect gevent's monkey-patching might be interfering with garbage collection of cyclic references.</p><p>Has anyone dealt with similar memory leaks in gevent + Django? Should I switch to uvicorn + ASGI, or is there a way to fix this within the current stack?</p>"
        },
        ["docker"] = new()
        {
            "<p>I'm setting up CI/CD pipelines for our microservices architecture (12 services) and need to build Docker images as part of the pipeline. I'm evaluating two approaches: <strong>Docker-in-Docker (DinD)</strong> vs <strong>mounting the Docker socket</strong>.</p><p>Our current setup uses GitLab CI with shared runners. Each service has its own <code>Dockerfile</code> with multi-stage builds. Build times are currently 5-15 minutes per service.</p><p>The constraints:</p><ul><li>Security: we run untrusted code in some build steps (npm install, pip install)</li><li>Performance: we need layer caching to work reliably</li><li>Isolation: builds shouldn't be able to access other containers on the host</li></ul><p>With DinD, I've noticed that layer caching is lost between builds because the daemon runs fresh each time. I've tried using <code>--cache-from</code> with registry-based caching:</p><pre><code>docker build \\\n  --cache-from registry.example.com/myapp:cache \\\n  --build-arg BUILDKIT_INLINE_CACHE=1 \\\n  -t myapp:latest .</code></pre><p>But this is slow because it has to pull the cache image each time. With socket mounting, caching works great but I'm concerned about security — a malicious build step could <code>docker exec</code> into other containers.</p><p>I'm also considering Kaniko and Buildah as alternatives. Has anyone benchmarked these approaches for a similar setup? What would you recommend for a team of 15 developers pushing ~50 builds/day?</p>"
        }
    };

    public static (string title, string content) GetRandomQuestion(string tag)
    {
        var normalizedTag = tag.ToLower();
        
        var titles = TitleTemplates.GetValueOrDefault(normalizedTag, TitleTemplates["csharp"]);
        var title = titles[Random.Shared.Next(titles.Count)];

        // Randomly pick a length variant
        var length = (ContentLength)Random.Shared.Next(3);
        var content = GetContentForLength(normalizedTag, length);

        return (title, content);
    }

    private static string GetContentForLength(string tag, ContentLength length)
    {
        var templates = length switch
        {
            ContentLength.Short => ShortContentTemplates.GetValueOrDefault(tag, ShortContentTemplates["csharp"]),
            ContentLength.Long => LongContentTemplates.GetValueOrDefault(tag, LongContentTemplates["csharp"]),
            _ => MediumContentTemplates.GetValueOrDefault(tag, MediumContentTemplates["csharp"]),
        };

        return templates[Random.Shared.Next(templates.Count)];
    }
}
