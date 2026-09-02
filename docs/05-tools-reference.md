# Tools Reference

Complete reference documentation for all 12 MCP tools provided by Mud MCP.

## Table of Contents

- [Overview](#overview)
- [Component Discovery Tools](#component-discovery-tools)
  - [list_components](#list_components)
  - [list_categories](#list_categories)
  - [get_components_by_category](#get_components_by_category)
- [Component Detail Tools](#component-detail-tools)
  - [get_component_detail](#get_component_detail)
  - [get_component_parameters](#get_component_parameters)
  - [get_related_components](#get_related_components)
- [Search Tools](#search-tools)
  - [search_components](#search_components)
- [Example Tools](#example-tools)
  - [get_component_examples](#get_component_examples)
  - [get_example_by_name](#get_example_by_name)
  - [list_component_examples](#list_component_examples)
- [API Reference Tools](#api-reference-tools)
  - [get_api_reference](#get_api_reference)
  - [get_enum_values](#get_enum_values)
- [Common Patterns](#common-patterns)
- [Error Handling](#error-handling)

---

## Overview

Mud MCP exposes 12 tools organized into functional categories:

| Category | Tools | Purpose |
|----------|-------|---------|
| **Discovery** | 3 | Browse and list components |
| **Detail** | 3 | Get detailed component information |
| **Search** | 1 | Find components by query |
| **Examples** | 3 | Access code examples |
| **API Reference** | 2 | Type and enum documentation |

### Tool Naming Convention

All tools use `snake_case` naming as per MCP conventions:
- `list_components` ✓
- `getComponentDetail` ✗

---

## Component Discovery Tools

### list_components

Lists all available MudBlazor components with optional filtering.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `category` | string | No | `null` | Category filter (e.g., "Buttons") |
| `includeDetails` | bool | No | `true` | Include parameter counts and descriptions |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "list_components",
    "arguments": {
      "category": "Buttons",
      "includeDetails": true
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# MudBlazor Components (5 total)

**Category:** Buttons

## Buttons

- **MudButton**: A Material Design button component
  - Parameters: 23, Events: 1, Examples: 8
- **MudButtonGroup**: Groups buttons together
  - Parameters: 12, Events: 0, Examples: 4
- **MudFab**: Floating action button
  - Parameters: 15, Events: 1, Examples: 5
- **MudIconButton**: Button with icon only
  - Parameters: 18, Events: 1, Examples: 6
- **MudToggleIconButton**: Toggle between two icons
  - Parameters: 14, Events: 2, Examples: 3

---
*Use `get_component_detail` for detailed information about a specific component.*
```

**Use Cases:**
- Browse available components
- Filter by category
- Get quick overview before diving deeper

---

### list_categories

Lists all component categories with descriptions and component counts.

**Parameters:** None

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "list_categories",
    "arguments": {}
  },
  "id": 1
}
```

**Example Output:**
```markdown
# MudBlazor Component Categories

## Buttons
*Interactive button components*
- **Components:** 5

## Form Inputs & Controls
*Components for user input and form handling*
- **Components:** 18

## Navigation
*Components for navigation and routing*
- **Components:** 12

## Layout
*Components for page structure and layout*
- **Components:** 10

...

---
*Use `list_components` with a category filter to see components in a specific category.*
```

---

### get_components_by_category

Gets all components in a specific category with detailed information.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `category` | string | Yes | - | Category name (e.g., "Form Inputs & Controls") |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_components_by_category",
    "arguments": {
      "category": "Form Inputs & Controls"
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# Form Inputs & Controls Components

Found 18 component(s):

### MudAutocomplete
Autocomplete component with search and selection.

**Key Parameters:**
- `Value` (T)
- `SearchFunc` (Func<string, CancellationToken, Task<IEnumerable<T>>>)
- `ToStringFunc` (Func<T, string>)
- `Variant` (Variant)
- `Label` (string)

*5 example(s) available*

### MudCheckBox
Checkbox component for boolean input.

...
```

---

## Component Detail Tools

### get_component_detail

Gets comprehensive details about a specific component.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name (e.g., "MudButton" or "Button") |
| `includeInheritedMembers` | bool | No | `true` | Include members inherited from base classes (annotated with the declaring type); set `false` for own-declared members only |
| `includeExamples` | bool | No | `true` | Include code examples |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_component_detail",
    "arguments": {
      "componentName": "MudButton",
      "includeExamples": true
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# MudButton

**Namespace:** `MudBlazor`
**Category:** Buttons
**Base Type:** `MudBaseButton`

## Description

A Material Design button component.

Use buttons for primary user actions and important interactions.

## Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `Color` | `Color` | The button color | `Color.Default` |
| `Variant` | `Variant` | The button variant | `Variant.Text` |
| `Size` | `Size` | The button size | `Size.Medium` |
| `Disabled` | `bool` | Whether disabled | `false` |
| `DisableElevation` | `bool` | Disable shadow | `false` |
| `DisableRipple` | `bool` | Disable ripple | `false` |
| `FullWidth` | `bool` | Fill container width | `false` |
...

## Events

| Event | Type | Description |
|-------|------|-------------|
| `OnClick` | `EventCallback<MouseEventArgs>` | Fired when clicked |

## Public Methods

### `async Task FocusAsync()`

Sets focus to the button.

## Examples

### Basic
```razor
<MudButton>Click Me</MudButton>
<MudButton Color="Color.Primary">Primary</MudButton>
<MudButton Variant="Variant.Outlined">Outlined</MudButton>
```

### Icon Button
```razor
<MudButton StartIcon="@Icons.Material.Filled.Add" Color="Color.Primary">
    Add Item
</MudButton>
```

*6 more examples available. Use `get_component_examples` for all examples.*

## Related Components

`MudIconButton`, `MudFab`, `MudButtonGroup`

## Links

- [Documentation](https://mudblazor.com/components/button)
- [Source Code](https://github.com/MudBlazor/MudBlazor/tree/dev/src/MudBlazor/Components/Button)
```

---

### get_component_parameters

Gets all parameters for a component, optionally filtered by category.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name |
| `parameterCategory` | string | No | `null` | Filter by category (e.g., "Behavior") |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_component_parameters",
    "arguments": {
      "componentName": "MudButton",
      "parameterCategory": "Appearance"
    }
  },
  "id": 1
}
```

---

### get_related_components

Gets components related through inheritance, category, or common usage.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name |
| `relationshipType` | string | No | `"all"` | Type: "all", "parent", "child", "sibling", "commonly_used_with" |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_related_components",
    "arguments": {
      "componentName": "MudSelect",
      "relationshipType": "sibling"
    }
  },
  "id": 1
}
```

---

## Search Tools

### search_components

Searches components by query across multiple fields.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `query` | string | Yes | - | Search query (e.g., "date picker") |
| `searchIn` | string | No | `"all"` | Fields: "name", "description", "parameters", "examples", "all" |
| `maxResults` | int | No | `10` | Max results (1-50) |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "search_components",
    "arguments": {
      "query": "date picker",
      "searchIn": "all",
      "maxResults": 5
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# Search Results for 'date picker'

Found 3 component(s):

## MudDatePicker

**Category:** Pickers

A date picker component for selecting dates.

**Matching Parameters:**
- `Date` (DateTime?)
- `DateFormat` (string)

---

## MudDateRangePicker

**Category:** Pickers

A date range picker for selecting start and end dates.

---

## MudTimePicker

**Category:** Pickers

A time picker component (related to date selection).

---

*Use `get_component_detail` for comprehensive information about a specific component.*
```

---

## Example Tools

### get_component_examples

Gets code examples for a component.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name |
| `maxExamples` | int | No | `5` | Max examples (1-20) |
| `filter` | string | No | `null` | Filter by name (e.g., "basic", "icon") |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_component_examples",
    "arguments": {
      "componentName": "MudDataGrid",
      "maxExamples": 3
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# MudDataGrid Examples

*12 example(s) available*

## Basic

Basic data grid usage with simple data binding.

**Features demonstrated:** Data binding, Columns

### Razor Markup

```razor
<MudDataGrid Items="@_items">
    <Columns>
        <PropertyColumn Property="x => x.Name" Title="Name" />
        <PropertyColumn Property="x => x.Value" Title="Value" />
    </Columns>
</MudDataGrid>
```

### Code-Behind

```csharp
private List<Element> _items = new()
{
    new Element { Name = "Hydrogen", Value = 1 },
    new Element { Name = "Helium", Value = 2 }
};
```

*Source: DataGridBasicExample.razor*

---

## Filtering

...

*9 more example(s) available. Increase `maxExamples` to see more.*
```

---

### get_example_by_name

Gets a specific example by name.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name |
| `exampleName` | string | Yes | - | Example name (e.g., "Basic", "Icon Button") |

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_example_by_name",
    "arguments": {
      "componentName": "MudButton",
      "exampleName": "Icon Button"
    }
  },
  "id": 1
}
```

---

### list_component_examples

Lists all example names without full code.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `componentName` | string | Yes | - | Component name |

**Example Output:**
```markdown
# MudButton Examples

*8 example(s) available*

| Example Name | Features | Has Code-Behind |
|--------------|----------|-----------------|
| Basic | Colors, Variants | No |
| Icon Button | Icons, Two-way binding | Yes |
| Disabled | Disabled state | No |
| Full Width | Layout | No |
| Loading | Loading state | Yes |
...

*Use `get_example_by_name` to get the full code for a specific example.*
```

---

## API Reference Tools

### get_api_reference

Gets full API reference for a component or type.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `typeName` | string | Yes | - | Type name (e.g., "MudButton", "Color") |
| `memberType` | string | No | `"all"` | Filter: "all", "properties", "methods", "events" |

> If `typeName` is not a component but a known enum, this tool falls back to the enum reference (equivalent to `get_enum_values`).

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_api_reference",
    "arguments": {
      "typeName": "MudButton",
      "memberType": "properties"
    }
  },
  "id": 1
}
```

---

### get_enum_values

Gets all values for a MudBlazor enum type.

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `enumName` | string | Yes | - | Enum name (e.g., "Color", "Variant", "Size") |

**Supported Enums:**

All public enums declared in the configured MudBlazor version's source are available (parsed at index-build time), including `Color`, `Size`, `Variant`, `Align`, `Position`, `Typo`, `InputType`, `Adornment`, `Origin`, and many more. Value descriptions come from each member's XML `<summary>` (falling back to `[Description]`), so they are always accurate for that version.

**Example Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_enum_values",
    "arguments": {
      "enumName": "Color"
    }
  },
  "id": 1
}
```

**Example Output:**
```markdown
# Color Enum Values (v9.0.0)

**Namespace:** `MudBlazor`

The color themes available in MudBlazor, allowing components to adapt their visual style based on the selected color.

| Value | Description |
|-------|-------------|
| `Default` | The default color theme. |
| `Primary` | The primary color theme, usually the main color used in the application. |
| `Secondary` | The secondary color theme, often used for accents and highlights. |
| `Tertiary` | The tertiary color theme, typically used for additional accents or highlights. |
| `Info` | The info color theme, used to indicate informational messages. |
| `Success` | The success color theme, used to indicate successful operations. |
| `Warning` | The warning color theme, used to indicate potential issues or warnings. |
| `Error` | The error color theme, used to indicate errors or critical issues. |
| `Dark` | The dark color theme, usually used for dark mode or dark-themed elements. |
| `Transparent` | The transparent theme, making elements see-through. |
| `Inherit` | Inherits the color from the parent element. |
| `Surface` | The surface color theme, typically used for the background or surface elements. |

## Usage Example

```razor
<MudComponent Color="Color.Default" />
```
```

---

## Common Patterns

### Component Name Resolution

Tools accept flexible component names:
- `"MudButton"` → MudButton
- `"Button"` → MudButton (auto-prefixed)
- `"mudbutton"` → MudButton (case-insensitive)

### Output Format

All tools return Markdown-formatted strings optimized for LLM consumption:
- Clear section headers
- Tables for structured data
- Code blocks with language hints
- Actionable follow-up suggestions

### Pagination

Large results include hints for more data:
```markdown
*6 more examples available. Use `get_component_examples` for all examples.*
```

---

## Error Handling

### Common Errors

**Component not found:**
```json
{
  "error": {
    "code": -32000,
    "message": "Component 'Unknown' not found. Use 'list_components' to see available components."
  }
}
```

**Invalid parameter:**
```json
{
  "error": {
    "code": -32000,
    "message": "Parameter 'maxResults' must be between 1 and 50. Got: 100"
  }
}
```

**Index not ready:**
```json
{
  "error": {
    "code": -32000,
    "message": "Component index is not ready. The server may still be initializing."
  }
}
```

### Error Recovery

All error messages include recovery hints:
- "Use 'list_components' to see available components"
- "Use 'list_categories' to see available categories"
- "Available examples: Basic, Icon, Disabled..."

---

## Next Steps

- [Configuration](./06-configuration.md) — Configure tool behavior
- [Testing](./07-testing.md) — Test tools with unit tests
- [MCP Inspector](./08-mcp-inspector.md) — Interactive tool testing
