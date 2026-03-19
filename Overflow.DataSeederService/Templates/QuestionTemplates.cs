namespace Overflow.DataSeederService.Templates;

/// <summary>Fallback question templates used when LLM is unavailable. Titles and content are topic-consistent pairs.</summary>
public static class QuestionTemplates
{
    private static readonly List<(string title, string content)> Questions = new()
    {
        (
            "How to implement async/await pattern correctly?",
            "<p>I'm trying to use <code>async/await</code> in my application but I'm not sure if I'm doing it right. Sometimes my method returns before the async work completes.</p><p>What's the correct way to make a method async and wait for it properly?</p>"
        ),
        (
            "What is the difference between let, const, and var?",
            "<p>I keep seeing code that uses <code>let</code>, <code>const</code>, and <code>var</code> interchangeably. When should I use each one? What are the scoping differences?</p>"
        ),
        (
            "How to prevent unnecessary re-renders in React?",
            "<p>My React component re-renders every time the parent updates, even though its props haven't changed. I've heard about <code>React.memo</code> and <code>useMemo</code> but I'm not sure when to use each.</p><p>Can someone explain the difference and provide a simple example?</p>"
        ),
        (
            "Understanding Python decorators",
            "<p>I see decorators like <code>@property</code> and <code>@staticmethod</code> everywhere in Python code but I don't understand how they work internally.</p><p>How do decorators work, and how can I write my own?</p>"
        ),
        (
            "How to optimize Docker image size?",
            "<p>My Docker image is over 1GB and deployments are slow. I'm using a standard <code>FROM node:20</code> base image.</p><p>What are the best practices for reducing image size? I've heard about multi-stage builds but haven't tried them yet.</p>"
        ),
        (
            "Best practices for dependency injection in .NET",
            "<p>I'm building a web API and want to implement dependency injection properly. I understand the basics but I'm confused about service lifetimes — <code>Singleton</code>, <code>Scoped</code>, and <code>Transient</code>.</p><p>When should I use each one?</p>"
        ),
        (
            "How to handle promises in JavaScript?",
            "<p>I have nested <code>.then()</code> calls that are getting hard to read. Is there a cleaner way to chain async operations in JavaScript?</p><pre><code>fetch('/api/data')\n  .then(res => res.json())\n  .then(data => fetch('/api/other/' + data.id))\n  .then(res => res.json())\n  .then(final => console.log(final));</code></pre>"
        ),
        (
            "Understanding closures in JavaScript",
            "<p>I have a loop with <code>setTimeout</code> that doesn't behave as expected:</p><pre><code>for (var i = 0; i < 5; i++) {\n  setTimeout(() => console.log(i), 1000);\n}</code></pre><p>It prints <code>5</code> five times instead of <code>0,1,2,3,4</code>. Why does this happen?</p>"
        ),
        (
            "How to use context managers in Python?",
            "<p>I want to make sure my file handles are always closed, even if an exception occurs. I've seen the <code>with</code> statement but I'm not sure how to create my own context manager.</p>"
        ),
        (
            "Docker networking between containers",
            "<p>I have a web app container and a database container. The web app can't connect to the database using <code>localhost</code>.</p><p>How does container networking work? Should I use Docker Compose or manual networks?</p>"
        ),
        (
            "EF Core lazy loading vs eager loading",
            "<p>I noticed my app makes too many database queries. I think it's the N+1 problem with lazy loading.</p><pre><code>var orders = _context.Orders.ToList();\nforeach (var order in orders)\n    Console.WriteLine(order.Customer.Name);</code></pre><p>What's the correct way to load related data efficiently?</p>"
        ),
        (
            "How to manage state in React with hooks?",
            "<p>My React app has state scattered across many components. I need to share state between siblings. Should I use Context API, Redux, or something else?</p><p>The app has about 20 components — I don't want to over-engineer it.</p>"
        )
    };

    public static (string title, string content) GetRandomQuestion(string tag)
    {
        return Questions[Random.Shared.Next(Questions.Count)];
    }
}