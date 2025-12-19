// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Maps components to their categories based on MudBlazor's menu structure.
/// </summary>
public sealed class CategoryMapper
{
    private readonly ILogger<CategoryMapper> _logger;
    private readonly Dictionary<string, ComponentCategory> _categoryMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ComponentCategory> _categories = [];
    private bool _isInitialized;

    public CategoryMapper(ILogger<CategoryMapper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the category mapper from the repository.
    /// </summary>
    /// <param name="repositoryPath">The path to the MudBlazor repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public Task InitializeAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (_isInitialized)
            return Task.CompletedTask;

        // Initialize with MudBlazor's known categories
        // These are derived from MenuService.cs in MudBlazor
        InitializeKnownCategories();
        
        _isInitialized = true;
        _logger.LogInformation("Category mapper initialized with {Count} categories", _categories.Count);
        
        return Task.CompletedTask;
    }

    private void InitializeKnownCategories()
    {
        // Categories from MudBlazor's MenuService
        AddCategory("Form Inputs & Controls", "Form Inputs & Controls", "Components for user input and form handling",
            "MudAutocomplete", "MudCheckBox", "MudSwitch", "MudTextField", "MudNumericField",
            "MudSelect", "MudSlider", "MudRating", "MudRadio", "MudRadioGroup",
            "MudDatePicker", "MudTimePicker", "MudColorPicker", "MudField",
            "MudInput", "MudInputLabel", "MudFileUpload", "MudForm", "MudMask");

        AddCategory("Buttons", "Buttons", "Interactive button components",
            "MudButton", "MudButtonGroup", "MudIconButton", "MudFab", "MudToggleIconButton");

        AddCategory("Navigation", "Navigation", "Components for navigation and routing",
            "MudNavMenu", "MudNavGroup", "MudNavLink", "MudBreadcrumbs",
            "MudDrawer", "MudAppBar", "MudTabs", "MudTabPanel", "MudLink",
            "MudMenu", "MudMenuItem", "MudMenuList");

        AddCategory("Layout", "Layout", "Components for page structure and layout",
            "MudContainer", "MudGrid", "MudItem", "MudHidden", "MudBreakpointProvider",
            "MudSpacer", "MudStack", "MudDivider", "MudExpansionPanels", "MudExpansionPanel");

        AddCategory("Data Display", "Data Display", "Components for displaying data and content",
            "MudTable", "MudSimpleTable", "MudDataGrid", "MudTreeView", "MudTreeViewItem",
            "MudList", "MudListItem", "MudListSubheader", "MudVirtualize",
            "MudChip", "MudChipSet", "MudBadge", "MudHighlighter");

        AddCategory("Feedback", "Feedback", "Components for user feedback and notifications",
            "MudAlert", "MudSnackbar", "MudProgressLinear", "MudProgressCircular",
            "MudSkeleton", "MudOverlay", "MudDialog", "MudDialogInstance", "MudDialogProvider",
            "MudMessageBox", "MudTooltip", "MudPopover");

        AddCategory("Charts", "Charts", "Data visualization components",
            "MudChart", "MudTimeSeriesChart", "MudSparkLine",
            "MudBarChart", "MudLineChart", "MudPieChart", "MudDonutChart");

        AddCategory("Pickers", "Pickers", "Selection and picker components",
            "MudDatePicker", "MudTimePicker", "MudDateRangePicker", "MudColorPicker");

        AddCategory("Cards", "Cards", "Card-based layout components",
            "MudCard", "MudCardActions", "MudCardContent", "MudCardHeader", "MudCardMedia",
            "MudPaper");

        AddCategory("Typography", "Typography", "Text and typography components",
            "MudText", "MudLink");

        AddCategory("Icons", "Icons", "Icon display components",
            "MudIcon", "MudAvatar", "MudAvatarGroup");

        AddCategory("Media", "Media", "Media display components",
            "MudImage", "MudCarousel", "MudCarouselItem");

        AddCategory("Utilities", "Utilities", "Utility components and helpers",
            "MudElement", "MudRender", "MudRTLProvider", "MudPopoverProvider",
            "MudScrollToTop", "MudFocusTrap", "MudSwipeArea");

        AddCategory("Services", "Services", "MudBlazor services",
            "ISnackbar", "IDialogService", "IScrollManager", "IBreakpointService",
            "IJsApiService", "IKeyInterceptor", "IScrollListener", "IScrollSpy",
            "IResizeObserver", "IResizeService");
    }

    private void AddCategory(string name, string title, string description, params string[] components)
    {
        var category = new ComponentCategory(
            Name: name,
            Title: title,
            Description: description,
            ComponentNames: components.ToList()
        );

        _categories.Add(category);

        foreach (var component in components)
        {
            _categoryMap[component] = category;
        }
    }

    /// <summary>
    /// Gets all categories.
    /// </summary>
    public IReadOnlyList<ComponentCategory> GetCategories()
    {
        return _categories.AsReadOnly();
    }

    /// <summary>
    /// Gets the category for a component.
    /// </summary>
    /// <param name="componentName">The component name.</param>
    /// <returns>The category, or null if not found.</returns>
    public ComponentCategory? GetCategoryForComponent(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        return _categoryMap.GetValueOrDefault(componentName);
    }

    /// <summary>
    /// Gets the category name for a component.
    /// </summary>
    /// <param name="componentName">The component name.</param>
    /// <returns>The category name, or null if not found.</returns>
    public string? GetCategoryName(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        return _categoryMap.TryGetValue(componentName, out var category) ? category.Name : null;
    }

    /// <summary>
    /// Gets components in a specific category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <returns>A list of component names in the category.</returns>
    public IReadOnlyList<string> GetComponentsInCategory(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);

        var category = _categories.FirstOrDefault(c => 
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) ||
            (c.Title?.Equals(categoryName, StringComparison.OrdinalIgnoreCase) == true));

        return category?.ComponentNames ?? [];
    }

    /// <summary>
    /// Tries to determine category from component name patterns.
    /// </summary>
    /// <param name="componentName">The component name to analyze.</param>
    /// <returns>The inferred category name.</returns>
    public string? InferCategoryFromName(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);

        // Remove "Mud" prefix for pattern matching
        var baseName = componentName.StartsWith("Mud") ? componentName[3..] : componentName;
        
        // Pattern-based category inference
        return baseName.ToLowerInvariant() switch
        {
            var n when n.Contains("button") => "Buttons",
            var n when n.Contains("input") || n.Contains("field") || n.Contains("select") ||
                       n.Contains("checkbox") || n.Contains("switch") || n.Contains("radio") ||
                       n.Contains("slider") || n.Contains("rating") || n.Contains("autocomplete") ||
                       n.Contains("form") || n.Contains("mask") => "Form Inputs & Controls",
            var n when n.Contains("nav") || n.Contains("drawer") || n.Contains("appbar") ||
                       n.Contains("tab") || n.Contains("menu") || n.Contains("breadcrumb") ||
                       n.Contains("link") => "Navigation",
            var n when n.Contains("grid") || n.Contains("container") || n.Contains("stack") ||
                       n.Contains("spacer") || n.Contains("divider") || n.Contains("expansion") ||
                       n.Contains("hidden") => "Layout",
            var n when n.Contains("table") || n.Contains("list") || n.Contains("tree") ||
                       n.Contains("chip") || n.Contains("badge") || n.Contains("virtualize") ||
                       n.Contains("highlight") => "Data Display",
            var n when n.Contains("alert") || n.Contains("snackbar") || n.Contains("progress") ||
                       n.Contains("skeleton") || n.Contains("overlay") || n.Contains("dialog") ||
                       n.Contains("tooltip") || n.Contains("popover") || n.Contains("message") => "Feedback",
            var n when n.Contains("chart") || n.Contains("sparkline") => "Charts",
            var n when n.Contains("picker") => "Pickers",
            var n when n.Contains("card") || n.Contains("paper") => "Cards",
            var n when n.Contains("text") && !n.Contains("field") => "Typography",
            var n when n.Contains("icon") || n.Contains("avatar") => "Icons",
            var n when n.Contains("image") || n.Contains("carousel") => "Media",
            _ => "Utilities"
        };
    }
}
