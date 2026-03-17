using Microsoft.EntityFrameworkCore;
using Overflow.QuestionService.Models;

namespace Overflow.QuestionService.Data;

/// <summary>
/// Seeds default programming tags on startup if the Tags table is empty.
/// Runs after EF Core migrations, so the table always exists.
/// </summary>
public static class TagSeeder
{
    private static readonly (string Slug, string Name, string Description)[] DefaultTags =
    [
        ("csharp", "C#",
            "A modern, object-oriented programming language developed by Microsoft for the .NET platform."),
        ("javascript", "JavaScript",
            "A high-level, interpreted scripting language used for web development and beyond."),
        ("typescript", "TypeScript", "A strongly typed superset of JavaScript that compiles to plain JavaScript."),
        ("python", "Python",
            "A versatile, high-level programming language known for its readability and broad ecosystem."),
        ("java", "Java",
            "A widely-used, class-based, object-oriented programming language for enterprise and Android development."),
        ("dotnet", ".NET",
            "A free, cross-platform, open-source developer platform for building many types of applications."),
        ("react", "React", "A JavaScript library for building user interfaces, maintained by Meta."),
        ("nextjs", "Next.js",
            "A React framework for production with server-side rendering, static generation, and more."),
        ("angular", "Angular", "A TypeScript-based web application framework led by the Angular Team at Google."),
        ("nodejs", "Node.js",
            "A JavaScript runtime built on Chrome's V8 engine for building scalable network applications."),
        ("sql", "SQL", "Structured Query Language for managing and querying relational databases."),
        ("postgresql", "PostgreSQL",
            "A powerful, open-source object-relational database system with a strong reputation for reliability."),
        ("docker", "Docker",
            "A platform for developing, shipping, and running applications in lightweight containers."),
        ("kubernetes", "Kubernetes",
            "An open-source container orchestration system for automating deployment, scaling, and management."),
        ("git", "Git",
            "A distributed version control system for tracking changes in source code during software development."),
        ("rest-api", "REST API",
            "Representational State Transfer — an architectural style for designing networked applications."),
        ("graphql", "GraphQL",
            "A query language for APIs and a runtime for fulfilling those queries with existing data."),
        ("html", "HTML",
            "HyperText Markup Language — the standard markup language for documents designed for web browsers."),
        ("css", "CSS",
            "Cascading Style Sheets — a style sheet language used for describing the presentation of a document."),
        ("entity-framework", "Entity Framework",
            "A modern object-relational mapper for .NET that simplifies data access."),
        ("asp-net-core", "ASP.NET Core",
            "A cross-platform, high-performance framework for building modern web applications with .NET."),
        ("testing", "Testing",
            "Software testing practices including unit testing, integration testing, and end-to-end testing."),
        ("security", "Security",
            "Application security topics including authentication, authorization, and vulnerability prevention."),
        ("performance", "Performance",
            "Optimization techniques for improving application speed, memory usage, and scalability."),
        ("devops", "DevOps",
            "Practices combining software development and IT operations to shorten the development lifecycle."),
        ("algorithms", "Algorithms", "Step-by-step procedures for solving computational problems and data processing."),
        ("data-structures", "Data Structures",
            "Organized ways of storing and managing data for efficient access and modification."),
        ("microservices", "Microservices",
            "An architectural style that structures an application as a collection of loosely coupled services."),
        ("rabbitmq", "RabbitMQ", "An open-source message broker that supports multiple messaging protocols."),
        ("redis", "Redis", "An in-memory data structure store used as a database, cache, message broker, and queue."),
    ];

    public static async Task SeedDefaultTagsAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuestionDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<QuestionDbContext>>();

        if (await db.Tags.AnyAsync())
        {
            logger.LogDebug("Tags table already has data — skipping seed");
            return;
        }

        logger.LogInformation("Seeding {Count} default tags...", DefaultTags.Length);

        foreach (var (slug, name, description) in DefaultTags)
        {
            db.Tags.Add(new Tag
            {
                Id = slug,
                Name = name,
                Slug = slug,
                Description = description
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("✅ Seeded {Count} default tags", DefaultTags.Length);
    }
}