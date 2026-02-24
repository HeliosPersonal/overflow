namespace Overflow.DataSeederService.Templates;

public static class QuestionTemplates
{
    private static readonly Dictionary<string, List<string>> TitleTemplates = new()
    {
        ["csharp"] = new()
        {
            "How to implement async/await pattern in C#?",
            "Best practices for dependency injection in .NET",
            "Difference between IEnumerable and IQueryable",
            "How to handle exceptions in async methods?",
            "What is the difference between Task and ValueTask?",
            "How to use LINQ to filter and transform collections?",
            "Understanding nullable reference types in C# 9+",
            "How to implement the repository pattern with EF Core?"
        },
        ["javascript"] = new()
        {
            "How to use async/await in JavaScript?",
            "Understanding closures in JavaScript",
            "What is the difference between let, const, and var?",
            "How to handle promises in JavaScript?",
            "Best practices for error handling in Node.js",
            "How to use map, filter, and reduce effectively?",
            "Understanding event loop in JavaScript",
            "How to implement debounce and throttle?"
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
            "How to optimize React performance?"
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
            "How to implement async functions in Python?"
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
            "Best practices for Docker security?"
        }
    };

    private static readonly Dictionary<string, List<string>> ContentTemplates = new()
    {
        ["csharp"] = new()
        {
            "I'm working on a .NET application and trying to understand the best way to implement asynchronous operations. I've read about async/await but I'm not sure when to use it.\n\nHere's my current code:\n```csharp\npublic void ProcessData() {\n    var data = GetData();\n    SaveData(data);\n}\n```\n\nHow should I convert this to use async/await properly? What are the benefits?",
            "I'm building a web API using ASP.NET Core and I want to implement dependency injection properly. I understand the basics but I'm confused about service lifetimes (Singleton, Scoped, Transient).\n\nCan someone explain when to use each lifetime and provide examples?",
            "I'm using Entity Framework Core in my application and I'm seeing performance issues when querying large datasets. I've heard about the difference between IEnumerable and IQueryable but I'm not sure which one to use.\n\nWhat's the practical difference and how does it affect database queries?"
        },
        ["javascript"] = new()
        {
            "I'm new to JavaScript and trying to understand promises. I have this code that makes API calls:\n```javascript\nfunction getData() {\n    fetch('/api/data')\n        .then(response => response.json())\n        .then(data => console.log(data));\n}\n```\n\nHow can I convert this to use async/await? Is it better?",
            "I'm working on a React application and I need to implement a search feature. I want to debounce the API calls to avoid making too many requests.\n\nHow do I implement debouncing in JavaScript? Should I use a library or write my own?",
            "I'm confused about closures in JavaScript. I have this code:\n```javascript\nfor (var i = 0; i < 5; i++) {\n    setTimeout(() => console.log(i), 1000);\n}\n```\n\nIt prints 5 five times instead of 0,1,2,3,4. Why does this happen and how do I fix it?"
        },
        ["react"] = new()
        {
            "I'm building a React application and my components are re-rendering too often, causing performance issues. I've heard about React.memo and useMemo but I'm not sure when to use each.\n\nCan someone explain the difference and provide examples of when to use them?",
            "I'm new to React hooks and I'm trying to fetch data when a component mounts. I've tried this:\n```jsx\nfunction MyComponent() {\n    const [data, setData] = useState(null);\n    \n    useEffect(() => {\n        fetchData().then(setData);\n    });\n    \n    return <div>{data}</div>;\n}\n```\n\nBut it seems to fetch continuously. What am I doing wrong?",
            "I need to share state between multiple components in my React app. Should I use Context API or should I look into Redux? What are the pros and cons of each approach?"
        },
        ["python"] = new()
        {
            "I'm working with large datasets in Python and I'm running into memory issues. I've heard that generators can help with this.\n\nCan someone explain how generators work and how they can reduce memory usage? When should I use them?",
            "I'm trying to understand Python decorators. I see them used everywhere but I don't understand how they work.\n```python\n@property\ndef name(self):\n    return self._name\n```\n\nHow do decorators work internally and how can I write my own?",
            "I'm working on a Python project and I need to handle file operations safely. I've heard about context managers and the 'with' statement.\n\nHow do context managers work and how can I create my own?"
        },
        ["docker"] = new()
        {
            "I'm deploying my application with Docker and the image size is over 2GB. This is causing slow deployments.\n\nWhat are the best practices for reducing Docker image size? I've heard about multi-stage builds but I'm not sure how to implement them.",
            "I'm trying to connect multiple Docker containers together. I have a web application and a database, and they need to communicate.\n\nHow does Docker networking work? Should I use bridge networks or something else?",
            "I'm confused about Docker volumes vs bind mounts. When should I use each? I need my database data to persist across container restarts."
        }
    };

    public static (string title, string content) GetRandomQuestion(string tag)
    {
        var normalizedTag = tag.ToLower();
        
        var titles = TitleTemplates.ContainsKey(normalizedTag) 
            ? TitleTemplates[normalizedTag] 
            : TitleTemplates["csharp"]; // fallback
            
        var contents = ContentTemplates.ContainsKey(normalizedTag)
            ? ContentTemplates[normalizedTag]
            : ContentTemplates["csharp"]; // fallback

        var title = titles[Random.Shared.Next(titles.Count)];
        var content = contents[Random.Shared.Next(contents.Count)];

        return (title, content);
    }
}
