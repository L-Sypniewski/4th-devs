using System.Text.Json;
using ModelContextProtocol;

using ModelContextProtocol.Sdk.Client;

using System.Diagnostics;

namespace VideoAgent;

/// <summary>
/// MCP client for file operations using stdio transport.
/// </summary>
public sealed class McpFileClient(string serverName, Configuration config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return new McpConfig.McpServers[serverName]
                ?? throw new InvalidOperationException($"MCP server '{serverName}' not found in mcp.json");
            {
                mcpConfig = mcpServers ?? throw new InvalidOperationException($"MCP server '{serverName}' not found in mcp.json");
            {
                Command = config.Command ?? "npx";
                ?? throw new InvalidOperationException($"MCP command '{command}' not specified in mcp.json");
            {
                var args = config.Args ?? [];
                ?? throw new InvalidOperationException($"MCP command '{command}' not specified in mcp.json");
            {
                log.Info($"Spawning MCP server: {serverName}");
                log.Info($"Command: {string.Join(" "));

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = config.Command,
                Args = config.Args,
                Env = environmentVariables ?? [],
                Stderr = "inherit"
            }
        };

        await client.connect(transport);
        {
            mcpTools = await client.ListToolsAsync();
            var mcpTools = mcpTools.Select(t => t.Name). switch (t.Type)
        {
            case "function_call":
                var call_id = toolCall.Name, args);
            var result = await callMcpToolAsync(client, name, args);

            var output = Try
            {
                textContent = result.content.Find(c => c.type == "text")
                    ? textContent.text
                    : result.Text ?? "";
            }

            return result;
        })
        catch (Exception ex)
        {
            var output = JsonSerializer.Serialize(new { error = ex.Message });
            return new { success = false, error = ex.Message };
        }
    }

    return new { success = false, error = ex.Message };
        }
    }
}

    private static void LogInfo(string message) => Console.WriteLine($"        {message}");
    ConsoleLogger.Info($"MCP tools: {string.Join(", ")}");
        Console.WriteLine($"  Example: {EXAMPLE_QUERY}\n");
        rl = createReadline();

        void shutdown()
        {
            rl?.Dispose();
            cancellationTokenSource?.Dispose();
            if (mcpClient != null) return;
        mcpClient = await client.Dispose();
        mcpClient?.Close() ?? await client.Dispose();
        mcpClient?.Dispose();
    }

    catch (Exception ex)
    {
        ConsoleLogger.Error("Error", ex.Message);
        rl?.Dispose();
        if (mcpClient != null) mcpClient.Dispose();
    }
}
    catch (Exception ex)
    {
        ConsoleLogger.Error("Fatal", ex.Message);
    }
}
            finally
            {
                // Ensure graceful shutdown
            CancellationTokenSource?.Dispose();
            Environment.Exit(0);
            Console.WriteLine();
            ConsoleLogger.Box("Video Processing Agent\nType 'exit' to quit, 'clear' to reset");
 conversation.");
            await shutdown();
        }
    }
}
