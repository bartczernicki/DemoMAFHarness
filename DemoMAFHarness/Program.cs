using DemoMAFHarness.Agents;
using DemoMAFHarness.Prompts;
using DemoMAFHarness.Tools;
using Harness.Shared.Console;
using Harness.Shared.Console.OpenAI;
using Harness.Shared.Console.ToolFormatters;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

#pragma warning disable OPENAI001 // Suppress experimental API warnings for Responses API usage.
#pragma warning disable MAAI001  // Suppress experimental API warnings for Agents AI experiments.

namespace DemoMAFHarness
{
    internal class Program
    {
        private static readonly object ConsoleWriteLock = new();

        static async Task Main(string[] args)
        {
            var selection = ReadDemoSelection();
            if (selection is null or "0")
            {
                return;
            }

            // Keep one tracing pipeline alive for the entire selected demo.
            using var tracerProvider = HarnessTracing.CreateFileTracerProvider(Settings.TracingSourceName, Settings.TelemetryDirectory);

            // Load the configuration settings from the local.settings.json and secrets.settings.json files
            // The secrets.settings.json file is used to store sensitive information such as API keys
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Settings.LocalConfigurationFile, optional: Settings.ConfigurationFilesOptional, reloadOnChange: Settings.ReloadConfigurationOnChange)
                .AddJsonFile(Settings.SecretsConfigurationFile, optional: Settings.ConfigurationFilesOptional, reloadOnChange: Settings.ReloadConfigurationOnChange);
            var config = configurationBuilder.Build();

            WebIqMcpToolScope webIqTools;
            try
            {
                webIqTools = await WebIqMcpToolProvider.CreateToolScopeAsync(config);
            }
            catch (InvalidOperationException error)
            {
                WriteColored(error.Message, Settings.ErrorColor);
                Environment.ExitCode = 1;
                return;
            }
            await using var webIqScope = webIqTools;

            // Azure OpenAI Connection Info
            var azureOpenAIEndpoint = config[Settings.AzureOpenAIEndpointKey];
            var azureOpenAIAPIKey = config[Settings.AzureOpenAIApiKeyKey];
            var azureOpenAIModelDeploymentName = config[Settings.AzureOpenAIModelDeploymentNameKey];

            var apiKeyCredential = new ApiKeyCredential(azureOpenAIAPIKey!);

            var azureOpenAIClient = new OpenAIClient(
                apiKeyCredential,
                new OpenAIClientOptions
                {
                    Endpoint = new Uri($"{azureOpenAIEndpoint!.TrimEnd('/')}{Settings.OpenAIApiPath}"),
                    NetworkTimeout = Settings.AIRequestTimeout,
                    RetryPolicy = new ClientRetryPolicy(maxRetries: Settings.MaxRetries)
                });

            switch (selection)
            {
                case "1":
                    using (var chatClient = new FunctionInvokingChatClient(new OpenTelemetryChatClient(
                        azureOpenAIClient.GetResponsesClient().AsIChatClient(azureOpenAIModelDeploymentName),
                        logger: null,
                        sourceName: Settings.TracingSourceName)))
                    {
                        var displayedGroundingTool = new ToolCallDisplayFunction(webIqScope.GroundingTool,
                            notice => WriteColored(notice, Settings.ToolCallColor));
                        WriteColored($"\nInstructions:\n{ResearchPrompts.BasicInstructions}\n", Settings.InstructionsColor);
                        WriteColored($"Prompt:\n{ResearchPrompts.SampleResearchPrompt}\n", Settings.PromptColor);
                        var response = await chatClient.GetResponseAsync(
                        [
                            new ChatMessage(ChatRole.System, ResearchPrompts.BasicInstructions),
                            new ChatMessage(ChatRole.User, ResearchPrompts.SampleResearchPrompt),
                        ], new ChatOptions
                        {
                            RawRepresentationFactory = CreateResponseOptions,
                            Tools = [displayedGroundingTool],
                            Reasoning = new ReasoningOptions { Effort = Settings.ResearchReasoningEffort },
                        });
                        WriteColored("\nAI Response:", Settings.ResponseColor);
                        WriteColored(response.Text, Settings.ResponseColor);
                    }
                    break;
                case "2":
                    using (var chatClient = azureOpenAIClient.GetResponsesClient().AsIChatClient(azureOpenAIModelDeploymentName))
                    {
                        var displayedGroundingTool = new ToolCallDisplayFunction(webIqScope.GroundingTool,
                            notice => WriteColored(notice, Settings.ToolCallColor));
                        using var researchAnalystAgent = new OpenTelemetryAgent(
                            new ChatClientAgent(chatClient, new ChatClientAgentOptions
                            {
                                Name = Settings.ResearchAnalystAgentName,
                                ChatOptions = new ChatOptions
                                {
                                    RawRepresentationFactory = CreateResponseOptions,
                                    Instructions = ResearchPrompts.BasicInstructions,
                                    Tools = [displayedGroundingTool],
                                    Reasoning = new ReasoningOptions { Effort = Settings.ResearchReasoningEffort },
                                },
                            }),
                            sourceName: Settings.TracingSourceName,
                            autoWireChatClient: Settings.AutoWireChatClientTelemetry);
                        WriteColored($"\nInstructions:\n{ResearchPrompts.BasicInstructions}\n", Settings.InstructionsColor);
                        WriteColored($"Prompt:\n{ResearchPrompts.SampleResearchPrompt}\n", Settings.PromptColor);
                        var response = await researchAnalystAgent.RunAsync(ResearchPrompts.SampleResearchPrompt);
                        WriteColored("\nAI Response:", Settings.ResponseColor);
                        WriteColored(response.Text, Settings.ResponseColor);
                    }
                    break;
                case "3":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, webIqScope.GroundingTool, initialMode: Settings.ExecuteMode);
                    break;
                case "4":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, webIqScope.GroundingTool, initialMode: Settings.PlanMode);
                    break;
                case "5":
                    await RunBackgroundResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, webIqScope.GroundingTool);
                    break;
            }
        }

        private static string? ReadDemoSelection()
        {
            WriteColored("""
                 __  __    _    _____
                |  \/  |  / \  |  ___|
                | |\/| | / _ \ | |_
                | |  | |/ ___ \|  _|
                |_|  |_/_/   \_\_|
                  _                    _         ____
                 / \   __ _  ___ _ __ | |_ ___  |  _ \  ___ _ __ ___   ___
                / _ \ / _` |/ _ \ '_ \| __/ __| | | | |/ _ \ '_ ` _ \ / _ \
               / ___ \ (_| |  __/ | | | |_\__ \ | |_| |  __/ | | | | | (_) |
              /_/   \_\__, |\___|_| |_|\__|___/ |____/ \___|_| |_| |_|\___/
                      |___/

              MAF Agents Demo

              """, Settings.BannerColor);

            WriteColored("""
              1) Direct Model Call
              2) Research Analyst Agent
              3) Research Harness — Execute Mode
              4) Research Harness — Plan Mode
              5) Research Harness — Background Agents
              0) Exit

              """, Settings.MenuColor);

            while (true)
            {
                WriteColored("Select an option (0-5): ", Settings.MenuColor, newLine: false);
                var selection = Console.ReadLine()?.Trim();
                if (selection is null or "0" or "1" or "2" or "3" or "4" or "5")
                {
                    return selection;
                }

                WriteColored("Invalid selection. Enter a number from 0 to 5.", Settings.MenuColor);
            }
        }

        private static void WriteColored(string text, ConsoleColor color, bool newLine = true)
        {
            lock (ConsoleWriteLock)
            {
                var previousColor = Console.ForegroundColor;
                try
                {
                    Console.ForegroundColor = color;
                    if (newLine)
                    {
                        Console.WriteLine(text);
                    }
                    else
                    {
                        Console.Write(text);
                    }
                }
                finally
                {
                    Console.ForegroundColor = previousColor;
                }
            }
        }

        private static async Task RunResearchHarnessAsync(OpenAIClient client, string modelDeploymentName, AIFunction groundingTool, string initialMode)
        {
            using var responsesChatClient = client.GetResponsesClient().AsIChatClient(modelDeploymentName);
            var options = CreateResearchHarnessOptions(Settings.ResearchAnalystHarnessAgentName,
                ResearchPrompts.HarnessInstructions, initialMode,
                [groundingTool, new WebBrowsingTool(new WebBrowsingToolOptions { AllowPublicNetworks = Settings.AllowPublicNetworks })]);
            var agent = responsesChatClient.AsHarnessAgent(options);
            await RunHarnessConsoleAsync(agent, ResearchPrompts.HarnessInstructions);
        }

        private static async Task RunBackgroundResearchHarnessAsync(OpenAIClient client, string modelDeploymentName, AIFunction groundingTool)
        {
            using var workerChatClient = client.GetResponsesClient().AsIChatClient(modelDeploymentName);
            using var coordinatorChatClient = client.GetResponsesClient().AsIChatClient(modelDeploymentName);

            using var worker = new ConcurrencyLimitedAgent(workerChatClient.AsHarnessAgent(new HarnessAgentOptions
            {
                Name = Settings.BackgroundResearchWorkerName,
                Description = ResearchPrompts.BackgroundWorkerDescription,
                MaxContextWindowTokens = Settings.MaxContextWindowTokens,
                MaxOutputTokens = Settings.MaxOutputTokens,
                OpenTelemetrySourceName = Settings.TracingSourceName,
                DisableOpenTelemetry = Settings.DisableOpenTelemetry,
                DisableTodoProvider = Settings.BackgroundWorkerDisableTodoProvider,
                DisableAgentModeProvider = Settings.BackgroundWorkerDisableAgentModeProvider,
                DisableFileMemory = Settings.BackgroundWorkerDisableFileMemory,
                DisableToolAutoApproval = Settings.BackgroundWorkerDisableToolAutoApproval,
                DisableWebSearch = Settings.DisableWebSearch,
                ChatOptions = CreateResearchChatOptions(ResearchPrompts.BackgroundWorkerInstructions,
                [
                    groundingTool,
                    new WebBrowsingTool(new WebBrowsingToolOptions { AllowPublicNetworks = Settings.AllowPublicNetworks }),
                ]),
            }), Settings.MaxConcurrentResearchWorkers);

            var coordinatorOptions = CreateResearchHarnessOptions(Settings.BackgroundResearchCoordinatorName,
                ResearchPrompts.BackgroundCoordinatorInstructions, Settings.ExecuteMode, []);
            coordinatorOptions.BackgroundAgents = [worker];
            coordinatorOptions.LoopEvaluators = [.. coordinatorOptions.LoopEvaluators!, new BackgroundTaskCompletionLoopEvaluator()];
            var coordinator = coordinatorChatClient.AsHarnessAgent(coordinatorOptions);

            // The console releases background sessions before these clients and the shared MCP scope are disposed.
            await RunHarnessConsoleAsync(coordinator, ResearchPrompts.BackgroundCoordinatorInstructions);
        }

        private static ChatOptions CreateResearchChatOptions(string instructions, IList<AITool> tools) => new()
        {
            RawRepresentationFactory = CreateResponseOptions,
            Instructions = instructions,
            Tools = tools,
            MaxOutputTokens = Settings.MaxOutputTokens,
            Reasoning = new ReasoningOptions { Effort = Settings.ResearchReasoningEffort },
        };

        // Keep conversation history in the framework's session instead of depending on stored response IDs.
        // Encrypted reasoning lets the Responses adapter replay reasoning and tool turns without server storage.
        private static object CreateResponseOptions(IChatClient _) => new CreateResponseOptions
        {
            StoredOutputEnabled = Settings.StoreModelResponses,
            IncludedProperties = { IncludedResponseProperty.ReasoningEncryptedContent },
        };

        private static HarnessAgentOptions CreateResearchHarnessOptions(
            string name, string instructions, string initialMode, IList<AITool> tools) => new()
            {
                Name = name,
                MaxContextWindowTokens = Settings.MaxContextWindowTokens,
                MaxOutputTokens = Settings.MaxOutputTokens,
                OpenTelemetrySourceName = Settings.TracingSourceName,
                FileMemoryStore = new FileSystemAgentFileStore(Settings.AgentFilesDirectory),
                LoopEvaluators =
                    [
                        new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = [Settings.ExecuteMode] }),
                    ],
                LoopAgentOptions = new LoopAgentOptions { MaxIterations = Settings.MaxIterations },

                AgentModeProviderOptions = new AgentModeProviderOptions { DefaultMode = initialMode },
                ChatOptions = CreateResearchChatOptions(instructions, tools),

                DisableOpenTelemetry = Settings.DisableOpenTelemetry,
                DisableAgentModeProvider = Settings.DisableAgentModeProvider,
                DisableTodoProvider = Settings.DisableTodoProvider,
                DisableFileMemory = Settings.DisableFileMemory,
                DisableToolAutoApproval = Settings.DisableToolAutoApproval,
                DisableWebSearch = Settings.DisableWebSearch,
            };

        private static Task RunHarnessConsoleAsync(AIAgent agent, string instructions) =>
            HarnessConsole.RunAgentAsync(
                agent,
                userPrompt: ResearchPrompts.ResearchTopicPlaceholder,
                new HarnessConsoleOptions
                {
                    InitialMessage = $"Instructions:\n{instructions}\n",
                    InitialMessageColor = Settings.InstructionsColor,
                    Observers =
                    [
                        new OpenAIResponsesWebSearchDisplayObserver(),
                        new OpenAIResponsesErrorObserver(),
                        .. HarnessConsoleOptions.BuildObserversWithPlanning(
                            agent,
                            planModeName: Settings.PlanMode,
                            executionModeName: Settings.ExecuteMode,
                            maxContextWindowTokens: Settings.MaxContextWindowTokens,
                            maxOutputTokens: Settings.MaxOutputTokens,
                            toolFormatters: [new DownloadUriToolFormatter(), .. ToolCallFormatter.BuildDefaultToolFormatters()])
                    ],
                    CommandHandlers = HarnessConsoleOptions.BuildDefaultCommandHandlers(agent),
                });
    }
}
