using Azure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using OpenAI;
using OpenAI.Responses;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;


namespace DemoMAFHarness
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
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
                    Endpoint = new Uri($"{azureOpenAIEndpoint!.TrimEnd('/')}/openai/v1")
                });

            // Instructions for the agent to follow when responding to prompts
            var decisionInstructions = "You are a helpful decision agent that provides insights and recommendations based on decision-making frameworks.";

            // A simple Decision Intelligence prompt to help with describing decision-making frameworks
            var simpleDecisionPrompt = """
            Identify and list 5 decision-making frameworks that can enhance the quality of decisions. 
            Briefly describe how each decision-making framework supports better analysis and reasoning in various scenarios. 
            """;

            // Create a chat client for the Azure OpenAI model deployment (Uses Chat Completions API)
            var chatClient = azureOpenAIClient.GetChatClient(azureOpenAIModelDeploymentName)
                .AsIChatClient();
            // Create a responses client for the Azure OpenAI model deployment (Uses Responses API)
            #pragma warning disable OPENAI001
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
                Instructions = decisionInstructions,
                Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.Medium,
                    Output = ReasoningOutput.Full
                }
            };

            var harnessAgentOptions = new HarnessAgentOptions
            {
                ChatOptions = chatOptions
            };

            // Ensure to use the Responses API for the harness agent to enable reasoning and decision-making capabilities
            AIAgent decisionHarnessAgent = responsesChatClient.AsHarnessAgent(harnessAgentOptions);
            var decisionHarnessAgentResponse = await decisionHarnessAgent.RunAsync(simpleDecisionPrompt);
            Console.WriteLine(decisionHarnessAgentResponse.Text);
        }
    }
}
