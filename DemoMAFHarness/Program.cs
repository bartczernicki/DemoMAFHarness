using DemoMAFHarness.Prompts;
using DemoMAFHarness.Tools;
using Harness.Shared.Console;
using Harness.Shared.Console.OpenAI;
using Harness.Shared.Console.ToolFormatters;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
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
                    RetryPolicy = new ClientRetryPolicy(maxRetries: Settings.MaxRetries)
                });

            switch (selection)
            {
                case "1":
                    using (var chatClient = new OpenTelemetryChatClient(
                        azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName).AsIChatClient(),
                        logger: null,
                        sourceName: Settings.TracingSourceName))
                    {
                        WriteColored($"\nInstructions:\n{ResearchPrompts.BasicInstructions}\n", Settings.InstructionsColor);
                        WriteColored($"Prompt:\n{ResearchPrompts.SampleResearchPrompt}\n", Settings.PromptColor);
                        WriteColored("AI response:", Settings.ResponseColor);
                        var response = await chatClient.GetResponseAsync(
                        [
                            new ChatMessage(ChatRole.System, ResearchPrompts.BasicInstructions),
                            new ChatMessage(ChatRole.User, ResearchPrompts.SampleResearchPrompt),
                        ]);
                        WriteColored(response.Text, Settings.ResponseColor);
                    }
                    break;
                case "2":
                    using (var chatClient = azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName).AsIChatClient())
                    {
                        using var researchAnalystAgent = new OpenTelemetryAgent(
                            new ChatClientAgent(chatClient, instructions: ResearchPrompts.BasicInstructions, name: Settings.ResearchAnalystAgentName),
                            sourceName: Settings.TracingSourceName,
                            autoWireChatClient: Settings.AutoWireChatClientTelemetry);
                        WriteColored($"\nInstructions:\n{ResearchPrompts.BasicInstructions}\n", Settings.InstructionsColor);
                        WriteColored($"Prompt:\n{ResearchPrompts.SampleResearchPrompt}\n", Settings.PromptColor);
                        WriteColored("AI response:", Settings.ResponseColor);
                        var response = await researchAnalystAgent.RunAsync(ResearchPrompts.SampleResearchPrompt);
                        WriteColored(response.Text, Settings.ResponseColor);
                    }
                    break;
                case "3":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, initialMode: Settings.ExecuteMode);
                    break;
                case "4":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, initialMode: Settings.PlanMode);
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
              0) Exit

              """, Settings.MenuColor);

            while (true)
            {
                WriteColored("Select an option (0-4): ", Settings.MenuColor, newLine: false);
                var selection = Console.ReadLine()?.Trim();
                if (selection is null or "0" or "1" or "2" or "3" or "4")
                {
                    return selection;
                }

                WriteColored("Invalid selection. Enter a number from 0 to 4.", Settings.MenuColor);
            }
        }

        private static void WriteColored(string text, ConsoleColor color, bool newLine = true)
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

        private static async Task RunResearchHarnessAsync(OpenAIClient client, string modelDeploymentName, string initialMode)
        {
            using var responsesChatClient = client.GetResponsesClient().AsIChatClient(modelDeploymentName);

            var chatOptions = new ChatOptions
            {
                Instructions = ResearchPrompts.HarnessInstructions,
                // Add a local web browsing tool that converts html to markdown.
                Tools =
                [
                    new WebBrowsingTool(
                        new WebBrowsingToolOptions { AllowPublicNetworks = Settings.AllowPublicNetworks }),
                ],
                MaxOutputTokens = Settings.MaxOutputTokens,
                Reasoning = new ReasoningOptions
                {
                    Effort = Settings.ResearchReasoningEffort,
                }
            };

            // Set the initial mode while retaining interactive mode switching.
            var agentModeProviderOptions = new AgentModeProviderOptions
            {
                DefaultMode = initialMode
            };

            var harnessAgentOptions = new HarnessAgentOptions
            {
                Name = Settings.ResearchAnalystHarnessAgentName,
                MaxContextWindowTokens = Settings.MaxContextWindowTokens,
                MaxOutputTokens = Settings.MaxOutputTokens,
                OpenTelemetrySourceName = Settings.TracingSourceName,
                FileMemoryStore = new FileSystemAgentFileStore(Settings.AgentFilesDirectory),
                LoopEvaluators =
                    [
                        new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = [Settings.ExecuteMode] }),
                    ],
                LoopAgentOptions = new LoopAgentOptions { MaxIterations = Settings.MaxIterations },

                AgentModeProviderOptions = agentModeProviderOptions,
                ChatOptions = chatOptions,

                DisableOpenTelemetry = Settings.DisableOpenTelemetry,
                DisableAgentModeProvider = Settings.DisableAgentModeProvider,
                DisableTodoProvider = Settings.DisableTodoProvider,
                DisableFileMemory = Settings.DisableFileMemory,
                DisableToolAutoApproval = Settings.DisableToolAutoApproval,
            };

            // Use the Responses API for the research harness's reasoning and web research capabilities.
            AIAgent researchAnalystHarnessAgent = responsesChatClient.AsHarnessAgent(harnessAgentOptions);

            // https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness

            // Run the interactive console session using the shared HarnessConsole helper.
            await HarnessConsole.RunAgentAsync(
                researchAnalystHarnessAgent,
                userPrompt: ResearchPrompts.ResearchTopicPlaceholder,
                new HarnessConsoleOptions
                {
                    InitialMessage = $"Instructions:\n{ResearchPrompts.HarnessInstructions}\n",
                    InitialMessageColor = Settings.InstructionsColor,
                    Observers =
                    [
                        new OpenAIResponsesWebSearchDisplayObserver(),
                        new OpenAIResponsesErrorObserver(),
                        .. HarnessConsoleOptions.BuildObserversWithPlanning(
                            researchAnalystHarnessAgent,
                            planModeName: Settings.PlanMode,
                            executionModeName: Settings.ExecuteMode,
                            maxContextWindowTokens: Settings.MaxContextWindowTokens,
                            maxOutputTokens: Settings.MaxOutputTokens,
                            toolFormatters: [new DownloadUriToolFormatter(), .. ToolCallFormatter.BuildDefaultToolFormatters()])
                    ],
                    CommandHandlers = HarnessConsoleOptions.BuildDefaultCommandHandlers(researchAnalystHarnessAgent),
                });
        }
    }
}
