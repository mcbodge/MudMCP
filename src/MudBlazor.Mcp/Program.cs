// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Cli;
using MudBlazor.Mcp.Hosting;

return CliApp.IsCliInvocation(args) ? await CliApp.RunAsync(args) : await McpServerHost.RunAsync(args);
