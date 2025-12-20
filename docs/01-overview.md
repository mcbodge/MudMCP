# Overview

This document provides a high-level overview of the Mud MCP server, its purpose, architecture, and design principles.

## Table of Contents

- [What is Mud MCP?](#what-is-mud-mcp)
- [Why Mud MCP?](#why-mud-mcp)
- [Target Audience](#target-audience)
- [Key Features](#key-features)
- [Architecture Overview](#architecture-overview)
- [Design Principles](#design-principles)
- [Technology Stack](#technology-stack)
- [Contributing](#contributing)

---

## What is Mud MCP?

Mud MCP is a **Model Context Protocol (MCP) server** that provides AI assistants with structured, real-time access to MudBlazor component documentation. It acts as a bridge between AI coding assistants (like GitHub Copilot, Claude, and other MCP-compatible clients) and the comprehensive MudBlazor component library documentation.

### How It Works

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   AI Assistant  │────▶│     Mud MCP     │────▶│ MudBlazor Repo  │
│  (Copilot/Claude)│◀────│   MCP Server    │◀────│  (GitHub Clone) │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

The server:
1. **Clones** the official MudBlazor repository from GitHub
2. **Parses** C# source files and Razor documentation using Roslyn
3. **Indexes** ~85 components with their parameters, events, methods, and examples
4. **Exposes** this data through standardized MCP tools

---

## Why Mud MCP?

### The Problem

AI assistants have general knowledge about MudBlazor but lack:
- **Real-time accuracy**: Documentation knowledge may be outdated
- **Parameter specifics**: Exact parameter names, types, and default values
- **Code examples**: Working examples from actual documentation
- **Related components**: Understanding of component relationships

### The Solution

Mud MCP provides AI assistants with:

| Capability | Description |
|------------|-------------|
| **Live Documentation** | Always synchronized with MudBlazor's dev branch |
| **Structured Data** | Parameters, events, methods in machine-readable format |
| **Real Examples** | Actual code examples extracted from documentation |
| **Semantic Search** | Find components by functionality, not just name |
| **Relationship Discovery** | Understand parent/child and sibling components |

### Example Interaction

**Without Mud MCP:**
> *User: "What parameters does MudButton support?"*  
> *AI: "MudButton supports Color, Variant, Size..." (potentially incomplete/outdated)*

**With Mud MCP:**
> *User: "What parameters does MudButton support?"*  
> *AI: [Calls `get_component_parameters`]*  
> *AI: "MudButton has 23 parameters including Color (Color enum), Variant (Variant enum), Size (Size enum), Disabled (bool, default: false), DisableElevation (bool), DisableRipple (bool)..." (complete, accurate list)*

---

## Target Audience

### Primary Users

| User Type | Use Case |
|-----------|----------|
| **AI Agents** | GitHub Copilot, Claude, custom MCP clients accessing documentation programmatically |
| **Blazor Developers** | Building applications with MudBlazor and using AI-assisted coding |
| **DevOps Engineers** | Deploying and maintaining the MCP server infrastructure |
| **Contributors** | Extending the server with new tools and capabilities |

### Prerequisites

- Familiarity with C# and .NET ecosystem
- Basic understanding of MudBlazor components
- For AI integration: Understanding of MCP protocol concepts

---

## Key Features

### 1. Component Discovery
- List all ~85 MudBlazor components
- Filter by category (Buttons, Forms, Navigation, etc.)
- Include parameter counts and descriptions

### 2. Detailed Documentation
- Complete parameter information with types and defaults
- Event callbacks with argument types
- Public methods with signatures
- Inheritance hierarchy

### 3. Code Examples
- Real examples from MudBlazor documentation
- Both Razor markup and C# code-behind
- Feature annotations for each example

### 4. Search Capabilities
- Search by component name
- Search by description content
- Search by parameter names
- Configurable result limits

### 5. API Reference
- Full API documentation for components
- Enum value listings with descriptions
- Type information and inheritance

### 6. Health & Observability
- Health check endpoints
- OpenTelemetry integration via Aspire
- Detailed index status reporting

---

## Architecture Overview

Mud MCP follows a clean, layered architecture:

```
┌─────────────────────────────────────────────────────────────────────┐
│                         MCP Protocol Layer                          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌────────────┐ │
│  │ComponentList │ │ComponentDetail│ │ComponentSearch│ │ApiReference│ │
│  │   Tools      │ │    Tools     │ │    Tools     │ │   Tools    │ │
│  └──────────────┘ └──────────────┘ └──────────────┘ └────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Service Layer                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    ComponentIndexer                          │   │
│  │         (In-memory index of all component data)              │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Parsing Layer                                 │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌────────────┐ │
│  │ XmlDocParser │ │RazorDocParser│ │ExampleExtract│ │CategoryMap │ │
│  │   (Roslyn)   │ │              │ │              │ │            │ │
│  └──────────────┘ └──────────────┘ └──────────────┘ └────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                             │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              GitRepositoryService (LibGit2Sharp)             │   │
│  │           Clone/Update MudBlazor repository                  │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility |
|-------|----------------|
| **MCP Protocol** | Handles JSON-RPC communication, tool registration, parameter validation |
| **Service** | Builds and queries the component index, manages search operations |
| **Parsing** | Extracts data from C#, Razor, and documentation files |
| **Infrastructure** | Git operations, file system access, caching |

---

## Design Principles

### 1. Immutability
All domain models are implemented as immutable C# records:
```csharp
public sealed record ComponentInfo(
    string Name,
    string Namespace,
    string Summary,
    // ...
);
```

### 2. Dependency Injection
Services are registered in the DI container and injected where needed:
```csharp
builder.Services.AddSingleton<IComponentIndexer, ComponentIndexer>();
builder.Services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
```

### 3. Fail-Fast Validation
Tool parameters are validated immediately with helpful error messages:
```csharp
ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
ToolValidation.RequireInRange(maxResults, 1, 50, nameof(maxResults));
```

### 4. LLM-Friendly Output
All responses are formatted as Markdown for optimal AI consumption:
- Clear section headers
- Tables for structured data
- Code blocks with language hints
- Actionable suggestions

### 5. Graceful Degradation
The server handles errors gracefully:
- Returns helpful error messages via `McpException`
- Continues operating even if some components fail to parse
- Health checks report degraded status when appropriate

---

## Technology Stack

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 10.0 | Runtime and SDK |
| **C#** | 13 | Programming language |
| **ASP.NET Core** | 10.0 | Web framework |
| **Aspire** | 13.1 | Orchestration and observability |
| **ModelContextProtocol.AspNetCore** | Latest | MCP SDK |
| **Microsoft.CodeAnalysis (Roslyn)** | Latest | C# source parsing |
| **LibGit2Sharp** | Latest | Git operations |
| **xUnit + Moq** | Latest | Testing framework |

### NuGet Packages

```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" />
<PackageReference Include="LibGit2Sharp" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
```

---

## Contributing

We welcome contributions! Here's how to get started:

### Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/MudBlazor.Mcp.git
   cd MudBlazor.Mcp
   ```

2. **Install prerequisites**
   - .NET 10 SDK (Preview)
   - Git
   - Your preferred IDE (VS Code, Visual Studio, Rider)

3. **Build and test**
   ```bash
   dotnet build
   dotnet test
   ```

### Contribution Guidelines

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/my-feature`
3. **Commit** your changes with clear messages
4. **Add tests** for new functionality
5. **Update documentation** as needed
6. **Submit** a pull request

### Code Standards

- Follow existing code style and patterns
- Include XML documentation for public APIs
- Add `[Description]` attributes for tool parameters
- Maintain immutability for domain models
- Include unit tests for new tools

### Areas for Contribution

- **New MCP Tools**: Additional query capabilities
- **Parsing Improvements**: Better extraction of documentation
- **Performance**: Caching and indexing optimizations
- **Documentation**: Tutorials, examples, translations
- **Testing**: Integration tests, edge cases

---

## Next Steps

- [Getting Started](./02-getting-started.md) — Installation and first run
- [Architecture](./03-architecture.md) — Deep dive into technical implementation
- [Tools Reference](./05-tools-reference.md) — Complete tool documentation
