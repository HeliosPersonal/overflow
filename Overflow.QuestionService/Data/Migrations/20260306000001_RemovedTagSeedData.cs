using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814

namespace QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovedTagSeedData : Migration
    {
        private static readonly string[] _seededIds =
        [
            "aspire", "keycloak", "dotnet", "ef-core", "wolverine",
            "postgresql", "signalr", "nextjs", "typescript", "microservices"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the previously seeded tags so they can be managed via UI
            foreach (var id in _seededIds)
            {
                migrationBuilder.Sql(
                    $"DELETE FROM \"Tags\" WHERE \"Id\" = '{id}' AND \"UsageCount\" = 0;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Tags",
                columns: ["Id", "Description", "Name", "Slug", "UsageCount"],
                values: new object[,]
                {
                    { "aspire",       "A composable, opinionated stack for building distributed apps with .NET. Provides dashboard, diagnostics, and simplified service orchestration.",                    "Aspire",               "aspire",       0 },
                    { "keycloak",     "An open-source identity and access management solution for modern applications and services. Integrates easily with OAuth2, OIDC, and SSO.",                       "Keycloak",             "keycloak",     0 },
                    { "dotnet",       "A modern, cross-platform development platform for building cloud, web, mobile, desktop, and IoT apps using C# and F#.",                                            ".NET",                 "dotnet",       0 },
                    { "ef-core",      "A modern object-database mapper (ORM) for .NET that supports LINQ, change tracking, and migrations for working with relational databases.",                        "Entity Framework Core","ef-core",      0 },
                    { "wolverine",    "A high-performance messaging and command-handling framework for .NET with built-in support for Mediator, queues, retries, and durable messaging.",                 "Wolverine",            "wolverine",    0 },
                    { "postgresql",   "A powerful, open-source object-relational database system known for reliability, feature richness, and standards compliance.",                                     "PostgreSQL",           "postgresql",   0 },
                    { "signalr",      "A real-time communication library for ASP.NET that enables server-to-client messaging over WebSockets, long polling, and more.",                                  "SignalR",              "signalr",      0 },
                    { "nextjs",       "A React framework for building fast, full-stack web apps with server-side rendering, routing, and static site generation.",                                        "Next.js",              "nextjs",       0 },
                    { "typescript",   "A statically typed superset of JavaScript that compiles to clean JavaScript. Helps build large-scale apps with tooling support.",                                  "TypeScript",           "typescript",   0 },
                    { "microservices","An architectural style that structures an application as a collection of loosely coupled services that can be independently deployed and scaled.",                  "Microservices",        "microservices",0 }
                });
        }
    }
}

