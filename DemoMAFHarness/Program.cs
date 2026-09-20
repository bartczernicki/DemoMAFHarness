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

            // Load the configuration settings from the local.settings.json and secrets.settings.json files
            // The secrets.settings.json file is used to store sensitive information such as API keys
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("secrets.settings.json", optional: true, reloadOnChange: true);
            var config = configurationBuilder.Build();

            // Azure OpenAI Connection Info
            var azureOpenAIEndpoint = config["AzureOpenAI:Endpoint"];
            var azureOpenAIAPIKey = config["AzureOpenAI:APIKey"];
            var azureOpenAIModelDeploymentName = config["AzureOpenAI:ModelDeploymentName"];

            var apiKeyCredential = new ApiKeyCredential(azureOpenAIAPIKey!);

            var azureOpenAIClient = new OpenAIClient(
                apiKeyCredential,
                new OpenAIClientOptions
                {
                    Endpoint = new Uri($"{azureOpenAIEndpoint!.TrimEnd('/')}/openai/v1"),
                    RetryPolicy = new ClientRetryPolicy(maxRetries: 5)
                });

            // Instructions for the agent to follow when responding to prompts
            var decisionInstructions =  "You are a helpful decision agent that provides insights and recommendations based on decision-making frameworks.";

            // A simple Decision Intelligence prompt to help with describing decision-making frameworks
            var simpleDecisionPrompt = """
            Identify and list 5 decision-making frameworks that can enhance the quality of decisions. 
            Briefly describe how each decision-making framework supports better analysis and reasoning in various scenarios. 
            """;

            switch (selection)
            {
                case "1":
                    using (var chatClient = azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName).AsIChatClient())
                    {
                        WriteColored($"\nInstructions:\n{simpleDecisionPrompt}\n", ConsoleColor.Magenta);
                        Console.WriteLine("AI response:");
                        var response = await chatClient.GetResponseAsync(simpleDecisionPrompt);
                        Console.WriteLine(response.Text);
                    }
                    break;
                case "2":
                    using (var chatClient = azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName).AsIChatClient())
                    {
                        AIAgent decisionAgent = new ChatClientAgent(chatClient, instructions: decisionInstructions);
                        WriteColored($"\nInstructions:\n{decisionInstructions}\n\nPrompt:\n{simpleDecisionPrompt}\n", ConsoleColor.Magenta);
                        Console.WriteLine("AI response:");
                        var response = await decisionAgent.RunAsync(simpleDecisionPrompt);
                        Console.WriteLine(response.Text);
                    }
                    break;
                case "3":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, initialMode: "execute");
                    break;
                case "4":
                    await RunResearchHarnessAsync(azureOpenAIClient, azureOpenAIModelDeploymentName!, initialMode: "plan");
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

              """, ConsoleColor.Yellow);

            WriteColored("""
              1) Direct model call
              2) Simple decision agent
              3) Research harness — execute mode
              4) Research harness — plan mode
              0) Exit

              """, ConsoleColor.Cyan);

            while (true)
            {
                WriteColored("Select an option (0-4): ", ConsoleColor.Cyan, newLine: false);
                var selection = Console.ReadLine()?.Trim();
                if (selection is null or "0" or "1" or "2" or "3" or "4")
                {
                    return selection;
                }

                WriteColored("Invalid selection. Enter a number from 0 to 4.", ConsoleColor.Cyan);
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
            const int MaxContextWindowTokens = 1_050_000;
            const int MaxOutputTokens = 128_000;
            const string TracingSourceName = "Harness.Research";

            // Capture agent activity and HTTP requests for the selected harness session.
            using var tracerProvider = HarnessTracing.CreateFileTracerProvider(TracingSourceName);
            using var responsesChatClient = client.GetResponsesClient().AsIChatClient(modelDeploymentName);

            var researchInstructions =
                    """
                    ## Research Assistant Instructions

                    You are a research assistant. When given a research topic, research it thoroughly using web search and web browsing.
                    Use your knowledge to form good search queries and hypotheses, but always verify claims with the tools available to you rather than relying on memory alone.

                    ### Research quality

                    Consult multiple sources when possible and cross-reference key claims.
                    When sources disagree, note the discrepancy and explain which source you consider more reliable and why.
                    If a web page fails to load or a search returns irrelevant results, try alternative search queries or sources before moving on.
                    Track your sources — you will need them when presenting results.

                    ### Presenting results

                    When presenting your final findings:
                    - Use Markdown formatting for clarity.
                    - Use clear sections with headings for each major topic or sub-question.
                    - Cite your sources inline (e.g., "According to [source name](URL), ...").
                    - End with a brief summary of key takeaways.
                    - In addition to returning the results to the user, save the final research report to file memory so it survives compaction and can be referenced later.
                    """;

            var chatOptions = new ChatOptions
            {
                Instructions = researchInstructions,
                // Add a local web browsing tool that converts html to markdown.
                Tools =
                [
                    new WebBrowsingTool(                        
                        new WebBrowsingToolOptions { AllowPublicNetworks = true }),
                ],
                MaxOutputTokens = MaxOutputTokens,
                Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.Medium,
                    // Output = ReasoningOutput.Summary
                }
            };

            // Set the initial mode while retaining interactive mode switching.
            var agentModeProviderOptions = new AgentModeProviderOptions
            {
                DefaultMode = initialMode
            };

            var harnessAgentOptions = new HarnessAgentOptions
            {
                Name = "DecisionHarnessAgent",
                MaxContextWindowTokens = MaxContextWindowTokens,
                MaxOutputTokens = MaxOutputTokens,
                OpenTelemetrySourceName = TracingSourceName,        // Use our custom source name so spans are captured by the TracerProvider above.
                FileMemoryStore = new FileSystemAgentFileStore(Path.Combine(AppContext.BaseDirectory, "agent-files")), // Configure the file memory provider to store files in a local folder called "agent-files".
                LoopEvaluators =
                    [
                        new TodoCompletionLoopEvaluator(new TodoCompletionLoopEvaluatorOptions { Modes = ["execute"] }),
                    ],
                LoopAgentOptions = new LoopAgentOptions { MaxIterations = 10 }, // Safety cap on the number of autonomous passes per turn.

                AgentModeProviderOptions = agentModeProviderOptions,
                ChatOptions = chatOptions,

                DisableOpenTelemetry = false,  // Enable OpenTelemetry tracing for the harness agent to capture spans for all agent activity.
                DisableAgentModeProvider = false, // Enable the agent mode provider to allow switching between plan and execute modes.
                DisableTodoProvider = false, // Enable the todo provider to allow the agent to create and manage a todo list for planning and execution.
                DisableFileMemory = false, // Enable the file memory provider to allow the agent to store and retrieve files in the local "agent-files" folder.
                DisableToolAutoApproval = false, // Enable tool auto-approval to allow the agent to automatically approve tool calls without user intervention.
                // DisableWebSearch = true,
            };

            // Ensure to use the Responses API for the harness agent to enable reasoning and decision-making capabilities
            AIAgent decisionHarnessAgent = responsesChatClient.AsHarnessAgent(harnessAgentOptions);

            // https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness

            // Run the interactive console session using the shared HarnessConsole helper.
            await HarnessConsole.RunAgentAsync(
                decisionHarnessAgent,
                userPrompt: "Enter a research topic to get started.",
                new HarnessConsoleOptions
                {
                    InitialMessage = $"Instructions:\n{researchInstructions}\n",
                    InitialMessageColor = ConsoleColor.Magenta,
                    Observers =
                    [
                        new OpenAIResponsesWebSearchDisplayObserver(),
                        new OpenAIResponsesErrorObserver(),
                        .. HarnessConsoleOptions.BuildObserversWithPlanning(
                            decisionHarnessAgent,
                            planModeName: "plan",
                            executionModeName: "execute",
                            maxContextWindowTokens: MaxContextWindowTokens,
                            maxOutputTokens: MaxOutputTokens,
                            toolFormatters: [new DownloadUriToolFormatter(), .. ToolCallFormatter.BuildDefaultToolFormatters()])
                    ],
                    CommandHandlers = HarnessConsoleOptions.BuildDefaultCommandHandlers(decisionHarnessAgent),
                });
        }
    }
}
