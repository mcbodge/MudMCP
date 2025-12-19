// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using ModelContextProtocol;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// Shared validation utilities for MCP tools.
/// Provides consistent error handling using <see cref="McpException"/>.
/// Tool validation errors are thrown as McpException so LLMs can see and self-correct.
/// </summary>
internal static class ToolValidation
{
    /// <summary>
    /// Validates that a string parameter is not null or whitespace.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name for the error message.</param>
    /// <exception cref="McpException">Thrown when the value is null or whitespace.</exception>
    public static void RequireNonEmpty([NotNull] string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpException($"Parameter '{parameterName}' cannot be null or empty.");
        }
    }

    /// <summary>
    /// Validates that a number is within a valid range.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="min">Minimum allowed value (inclusive).</param>
    /// <param name="max">Maximum allowed value (inclusive).</param>
    /// <param name="parameterName">The parameter name for the error message.</param>
    /// <exception cref="McpException">Thrown when the value is outside the range.</exception>
    public static void RequireInRange(int value, int min, int max, string parameterName)
    {
        if (value < min || value > max)
        {
            throw new McpException($"Parameter '{parameterName}' must be between {min} and {max}. Got: {value}");
        }
    }

    /// <summary>
    /// Validates that a number is positive (greater than zero).
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="parameterName">The parameter name for the error message.</param>
    /// <exception cref="McpException">Thrown when the value is not positive.</exception>
    public static void RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new McpException($"Parameter '{parameterName}' must be greater than zero. Got: {value}");
        }
    }

    /// <summary>
    /// Validates that a value is one of the allowed options (case-insensitive).
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="allowedValues">Array of allowed values.</param>
    /// <param name="parameterName">The parameter name for the error message.</param>
    /// <exception cref="McpException">Thrown when the value is not in allowed values.</exception>
    public static void RequireValidOption(string? value, string[] allowedValues, string parameterName)
    {
        if (value is null) return;

        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            throw new McpException($"Parameter '{parameterName}' must be one of: {string.Join(", ", allowedValues)}. Got: '{value}'");
        }
    }

    /// <summary>
    /// Throws when a component is not found.
    /// </summary>
    /// <param name="componentName">The name of the component that was not found.</param>
    /// <exception cref="McpException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowComponentNotFound(string componentName)
    {
        throw new McpException($"Component '{componentName}' not found. Use 'list_components' to see available components.");
    }

    /// <summary>
    /// Throws when a type is not found.
    /// </summary>
    /// <param name="typeName">The name of the type that was not found.</param>
    /// <exception cref="McpException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowTypeNotFound(string typeName)
    {
        throw new McpException($"Type '{typeName}' not found. Use 'list_components' or check the type name.");
    }

    /// <summary>
    /// Throws when a category is not found.
    /// </summary>
    /// <param name="categoryName">The name of the category that was not found.</param>
    /// <param name="availableCategories">Optional list of available categories.</param>
    /// <exception cref="McpException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowCategoryNotFound(string categoryName, IEnumerable<string>? availableCategories = null)
    {
        var message = $"Category '{categoryName}' not found.";
        if (availableCategories is not null)
        {
            message += $" Available categories: {string.Join(", ", availableCategories)}";
        }
        else
        {
            message += " Use 'list_categories' to see available categories.";
        }

        throw new McpException(message);
    }

    /// <summary>
    /// Throws when an example is not found.
    /// </summary>
    /// <param name="exampleName">The name of the example that was not found.</param>
    /// <param name="componentName">The component that was searched.</param>
    /// <param name="availableExamples">Optional list of available examples.</param>
    /// <exception cref="McpException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowExampleNotFound(string exampleName, string componentName, IEnumerable<string>? availableExamples = null)
    {
        var message = $"Example '{exampleName}' not found for component '{componentName}'.";
        if (availableExamples is not null)
        {
            message += $" Available examples: {string.Join(", ", availableExamples)}";
        }

        throw new McpException(message);
    }

    /// <summary>
    /// Throws when the component index is not ready.
    /// </summary>
    /// <exception cref="McpException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowIndexNotReady()
    {
        throw new McpException("Component index is not ready. The server may still be initializing. Please try again in a moment.");
    }
}
