using Azure;
using DemoMAFHarness.Tools;
using Harness.Shared.Console;
using Harness.Shared.Console.OpenAI;
using Harness.Shared.Console.ToolFormatters;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

#pragma warning disable OPENAI001 // Suppress experimental API warnings for Responses API usage.
#pragma warning disable MAAI001  // Suppress experimental API warnings for Agents AI experiments.

namespace DemoMAFHarness
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            const int MaxContextWindowTokens = 1_050_000;
            const int MaxOutputTokens = 128_000;
            const string TracingSourceName = "Harness.Research";

            // Set up OpenTelemetry tracing that writes spans to a text file.
            // This captures all agent activity (tool calls, model invocations, compaction, etc.)
            // as well as HTTP requests made by the underlying HttpClient transport.
            using var tracerProvider = HarnessTracing.CreateFileTracerProvider(TracingSourceName);

            Console.WriteLine("Hello MAF Agents");

            // Load the configuration settings from the local.settings.json and secrets.settings.json files
            // The secrets.settings.json file is used to store sensitive information such as API keys
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("secrets.settings.json", optional: true, reloadOnChange: true);
            var config = configurationBuilder.Build();

            // IMPORTANT: You ONLY NEED either Azure OpenAI or OpenAI connectiopn info, not both.
            // Azure OpenAI Connection Info
            var azureOpenAIEndpoint = config["AzureOpenAI:Endpoint"];
            var azureOpenAIAPIKey = config["AzureOpenAI:APIKey"];
            var azureOpenAIModelDeploymentName = config["AzureOpenAI:ModelDeploymentName"];

            //Console.WriteLine($"Azure OpenAI Endpoint: {azureOpenAIEndpoint}");
            //Console.WriteLine($"Azure OpenAI API Key: {azureOpenAIAPIKey}");
            //Console.WriteLine($"Azure OpenAI Model Deployment Name: {azureOpenAIModelDeploymentName}");

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

            var researchInsructions =
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

            // Create a chat client for the Azure OpenAI model deployment (Uses Chat Completions API)
            var chatClient = azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName)
                .AsIChatClient();
            // Create a responses client for the Azure OpenAI model deployment (Uses Responses API)
            var responsesClient = azureOpenAIClient.GetResponsesClient();
            var responsesChatClient = responsesClient.AsIChatClient(azureOpenAIModelDeploymentName);

            //// Execute the prompt against the AI model
            //var simplePromptResponse = await chatClient.GetResponseAsync(simpleDecisionPrompt);
            //var responseString = simplePromptResponse.Text;

            //// Display the response from the AI model
            //Console.WriteLine(responseString);


            // 2) Add simple decision-making agent to demonstrate the use of an agent for decision-making tasks
            //AIAgent decisionAgent = new ChatClientAgent(
            //    chatClient,
            //    instructions: decisionInstructions);

            //var decisionAgentResponse = await decisionAgent.RunAsync(simpleDecisionPrompt);
            //Console.WriteLine(decisionAgentResponse.Text);

            // 3) Add a simple harness decision-making agent to demonstrate the use of an agent for decision-making tasks
            var chatOptions = new ChatOptions
            {
                Instructions = researchInsructions,
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

            // Start immediately in execute mode instead of plan mode.
            var agentModeProviderOptions = new AgentModeProviderOptions
            {
                // DefaultMode = "execute"
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

                // DisableWebSearch = true,
            };

            // Ensure to use the Responses API for the harness agent to enable reasoning and decision-making capabilities
            AIAgent decisionHarnessAgent = responsesChatClient.AsHarnessAgent(harnessAgentOptions);

            //var decisionHarnessAgentResponse = await decisionHarnessAgent.RunAsync(simpleDecisionPrompt);
            //Console.WriteLine(decisionHarnessAgentResponse.Text);

            // https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Harness

            // Run the interactive console session using the shared HarnessConsole helper.
            await HarnessConsole.RunAgentAsync(
                decisionHarnessAgent,
                userPrompt: "Enter a research topic to get started.",
                new HarnessConsoleOptions
                {
                    Observers = [
                        new OpenAIResponsesWebSearchDisplayObserver(),
            new OpenAIResponsesErrorObserver(),
            .. HarnessConsoleOptions.BuildObserversWithPlanning(
                decisionHarnessAgent,
                planModeName: "plan",
                executionModeName: "execute",
                maxContextWindowTokens: MaxContextWindowTokens,
                maxOutputTokens: MaxOutputTokens,
                toolFormatters: [new DownloadUriToolFormatter(), .. ToolCallFormatter.BuildDefaultToolFormatters()])],
                    CommandHandlers = HarnessConsoleOptions.BuildDefaultCommandHandlers(decisionHarnessAgent),
                });
        }
    }
}
