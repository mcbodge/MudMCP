# MudBlazor MCP Server

An MCP (Model Context Protocol) server that provides AI assistants with access to MudBlazor component documentation, examples, and usage information.

> **Note:** This project is not affiliated with MudBlazor. It extracts documentation from the official MudBlazor repository.

## Features

- **Component Discovery**: List all MudBlazor components with filtering by category
- **Detailed Documentation**: Get comprehensive component details including parameters, events, and methods
- **Code Examples**: Access real code examples from the MudBlazor documentation
- **Search**: Search components by name, description, or parameters
- **API Reference**: Get full API reference for components and enums
- **Related Components**: Discover related components through inheritance and common usage

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `list_components` | Lists all MudBlazor components, optionally filtered by category |
| `list_categories` | Lists all component categories with descriptions |
| `get_component_detail` | Gets comprehensive details about a specific component |
| `get_component_parameters` | Gets all parameters for a component |
| `get_component_examples` | Gets code examples for a component |
| `get_example_by_name` | Gets a specific example by name |
| `list_component_examples` | Lists all example names for a component |
| `search_components` | Searches components by query |
| `get_components_by_category` | Gets all components in a specific category |
| `get_related_components` | Gets components related to a specific component |
| `get_api_reference` | Gets full API reference for a type |
| `get_enum_values` | Gets all values for a MudBlazor enum |

## Prerequisites

- .NET 10 SDK (Preview)
- Git

## Getting Started

### Clone and Build

```bash
git clone https://github.com/yourusername/MudBlazor.Mcp.git
cd MudBlazor.Mcp
dotnet restore
dotnet build
```

### Run the Server

```bash
cd src/MudBlazor.Mcp
dotnet run
```

The server will:
1. Clone the MudBlazor repository (or update if it exists)
2. Index all component documentation
3. Start the MCP server on `http://localhost:5180`

### Run with Aspire

```bash
cd src/MudBlazor.Mcp.AppHost
dotnet run
```

## Configuration

Configure the server via `appsettings.json` or environment variables:

```json
{
  "MudBlazor": {
    "Repository": {
      "Url": "https://github.com/MudBlazor/MudBlazor.git",
      "Branch": "dev",
      "LocalPath": "./mudblazor-repo",
      "AutoUpdate": true
    },
    "Cache": {
      "Enabled": true,
      "SlidingExpirationMinutes": 60,
      "AbsoluteExpirationMinutes": 1440
    }
  }
}
```

## Using with AI Assistants

### VS Code with GitHub Copilot

Add to your VS Code settings:

```json
{
  "mcp.servers": {
    "mudblazor": {
      "url": "http://localhost:5180/mcp"
    }
  }
}
```

### Claude Desktop

Add to your Claude configuration:

```json
{
  "mcpServers": {
    "mudblazor": {
      "url": "http://localhost:5180/mcp"
    }
  }
}
```

## Example Usage

Once connected, you can ask your AI assistant questions like:

- "List all MudBlazor button components"
- "Show me how to use MudTextField with validation"
- "What parameters does MudDataGrid support?"
- "Show me examples of MudDialog"
- "What are the available Color enum values?"
- "Find components related to MudSelect"

## Project Structure

```
src/
├── MudBlazor.Mcp/              # Main MCP server
│   ├── Configuration/          # Options and settings
│   ├── Models/                 # Domain models
│   ├── Services/               # Core services
│   │   └── Parsing/            # Parsing utilities
│   └── Tools/                  # MCP tools
├── MudBlazor.Mcp.AppHost/      # Aspire orchestration
└── MudBlazor.Mcp.ServiceDefaults/  # Shared service defaults

tests/
└── MudBlazor.Mcp.Tests/        # Unit tests
```

## Architecture

The server follows a clean architecture pattern:

1. **Git Repository Service**: Clones and keeps the MudBlazor repository up to date
2. **Parsing Services**: Extract documentation from source files using Roslyn
3. **Component Indexer**: Builds and maintains the searchable component index
4. **MCP Tools**: Expose the indexed data through MCP protocol

```
┌─────────────────────────────────────────────────────────────┐
│                      MCP Tools                              │
│  (list_components, get_component_detail, search, etc.)      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Component Indexer                         │
│           (In-memory index of all components)               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Parsing Services                          │
│  XmlDocParser │ RazorDocParser │ ExampleExtractor           │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 Git Repository Service                      │
│              (Clone/Update MudBlazor repo)                  │
└─────────────────────────────────────────────────────────────┘
```

## Health Checks

- `/health` - Overall health status
- `/health/ready` - Readiness check (index built)
- `/health/live` - Liveness check

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project, MudBlazor.Mcp, is an independent implementation built on top of the MudBlazor framework. It is not affiliated with, endorsed by, or officially supported by the MudBlazor team.
MudBlazor is licensed under the GNU General Public License v2.0 (GPL-2.0). In compliance with this license:

The source code of this project is provided under GPL-2.0.
Original copyright and license notices from MudBlazor are retained.
Modifications and additions are clearly documented.

For more details on the GPL-2.0 license, see GNU GPL v2.0 [LICENSE](LICENSE) file for details.

## Acknowledgments

- [MudBlazor](https://mudblazor.com/) - The amazing Blazor component library
- [Model Context Protocol](https://modelcontextprotocol.io/) - The protocol specification
