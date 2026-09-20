using Microsoft.Extensions.AI;

namespace DemoMAFHarness;

internal static class Settings
{
    public const string LocalConfigurationFile = "local.settings.json";
    public const string SecretsConfigurationFile = "secrets.settings.json";
    public const bool ConfigurationFilesOptional = true;
    public const bool ReloadConfigurationOnChange = true;
    public const string AzureOpenAIEndpointKey = "AzureOpenAI:Endpoint";
    public const string AzureOpenAIApiKeyKey = "AzureOpenAI:APIKey";
    public const string AzureOpenAIModelDeploymentNameKey = "AzureOpenAI:ModelDeploymentName";
    public const string OpenAIApiPath = "/openai/v1";

    public const string WebIqApiKeyKey = "AzureOpenAI:WebIQGroundingAPIKey";
    public const string WebIqMcpEndpointKey = "WebIQ:McpEndpoint";
    public const string WebIqMcpEndpoint = "https://api.microsoft.ai/v3/mcp/";
    public const string WebIqApiKeyHeader = "x-apikey";
    public const string WebIqServerName = "MAIGrounding-MCP";
    public const string WebIqRemoteToolName = "web";
    public const string WebIqToolName = "WebIQGrounding";
    public static readonly TimeSpan WebIqTimeout = TimeSpan.FromSeconds(60);

    public const int MaxRetries = 5;
    public static readonly TimeSpan AIRequestTimeout = TimeSpan.FromSeconds(200);
    public const int MaxContextWindowTokens = 1_050_000;
    public const int MaxOutputTokens = 128_000;
    public const int MaxIterations = 10;
    public static readonly ReasoningEffort ResearchReasoningEffort = ReasoningEffort.Medium;

    public const string TracingSourceName = "Harness.Research";
    public const string ResearchAnalystAgentName = "ResearchAnalystAgent";
    public const string ResearchAnalystHarnessAgentName = "ResearchAnalystHarnessAgent";
    public const string PlanMode = "plan";
    public const string ExecuteMode = "execute";

    public const string TelemetryFolderName = "Telemetry";
    public const string AgentFilesFolderName = "agent-files";
    public static string TelemetryDirectory => Path.Combine(AppContext.BaseDirectory, TelemetryFolderName);
    public static string AgentFilesDirectory => Path.Combine(AppContext.BaseDirectory, AgentFilesFolderName);

    public const bool AutoWireChatClientTelemetry = true;
    public const bool DisableOpenTelemetry = false;
    public const bool DisableAgentModeProvider = false;
    public const bool DisableTodoProvider = false;
    public const bool DisableFileMemory = false;
    public const bool DisableToolAutoApproval = false;
    public const bool DisableWebSearch = true;
    public const bool AllowPublicNetworks = true;

    public const ConsoleColor BannerColor = ConsoleColor.Yellow;
    public const ConsoleColor MenuColor = ConsoleColor.Cyan;
    public const ConsoleColor InstructionsColor = ConsoleColor.Magenta;
    public const ConsoleColor PromptColor = ConsoleColor.Cyan;
    public const ConsoleColor ResponseColor = ConsoleColor.Green;
    public const ConsoleColor ToolCallColor = ConsoleColor.DarkYellow;
    public const ConsoleColor ErrorColor = ConsoleColor.Red;
}
