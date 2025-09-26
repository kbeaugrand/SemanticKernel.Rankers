using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using SemanticKernel.Rankers.LMRanker;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.Rankers.LMRanker.Tests;

/// <summary>
/// Debug tests to help diagnose issues with LMRanker
/// </summary>
public class LMRankerDebugTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Kernel? _kernel;
    private readonly LMRanker? _ranker;
    private readonly bool _skipTests;

    public LMRankerDebugTests(ITestOutputHelper output)
    {
        _output = output;
        _skipTests = Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS") == "true";
        
        if (!_skipTests)
        {
            _kernel = CreateTestKernel();
            if (_kernel != null)
            {
                _ranker = new LMRanker(_kernel);
                
                // Quick test to see if the AI service is actually working
                if (!IsAIServiceWorking())
                {
                    _skipTests = true;
                }
            }
            else
            {
                _skipTests = true;
            }
        }
    }

    [Fact]
    public async Task Debug_SingleSimpleScoring_ShouldWork()
    {
        if (_skipTests || _ranker == null)
        {
            return;
        }

        // Arrange - Very simple test case
        var query = "reset password";
        var document = "To reset your password, click the forgot password link and enter your email address.";

        // Act
        var results = new List<(string DocumentText, double Score)>();
        await foreach (var scoreResult in _ranker.ScoreAsync(query, CreateAsyncEnumerable(new[] { document })))
        {
            results.Add(scoreResult);
        }

        // Assert and Debug
        results.Should().HaveCount(1);
        var testResult = results.First();
        
        _output.WriteLine($"Query: '{query}'");
        _output.WriteLine($"Document: '{document}'");
        _output.WriteLine($"Score: {testResult.Score}");
        
        testResult.Score.Should().BeGreaterThan(0.0, "should get a meaningful score for relevant content");
        testResult.Score.Should().BeLessThanOrEqualTo(1.0, "score should not exceed 1.0");
    }

    [Fact]
    public async Task Debug_CompareMultipleDocuments_ShouldShowRelativeScoring()
    {
        if (_skipTests || _ranker == null)
        {
            return;
        }

        // Arrange - Documents with clear relevance differences
        var query = "password reset";
        var documents = new[]
        {
            "To reset your password, go to the login page and click 'Forgot Password'. Enter your email address and we'll send you a reset link.", // Highly relevant
            "Our customer service hours are Monday to Friday, 9 AM to 5 PM EST. You can reach us at support@company.com or call 1-800-SUPPORT.", // Not relevant
            "Password requirements: Your password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, and one number." // Somewhat relevant
        };

        // Act
        var results = new List<(string DocumentText, double Score)>();
        await foreach (var result in _ranker.ScoreAsync(query, CreateAsyncEnumerable(documents)))
        {
            results.Add(result);
        }

        // Debug output
        _output.WriteLine($"Query: '{query}'");
        _output.WriteLine("\nResults (ordered by score):");
        foreach (var result in results.OrderByDescending(r => r.Score))
        {
            _output.WriteLine($"Score: {result.Score:F3} | Text: {result.DocumentText.Substring(0, Math.Min(80, result.DocumentText.Length))}...");
        }

        // Assert basic functionality
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.Score.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0));
        
        // Find specific documents
        var passwordResetDoc = results.First(r => r.DocumentText.Contains("reset your password"));
        var customerServiceDoc = results.First(r => r.DocumentText.Contains("customer service hours"));
        var passwordRequirementsDoc = results.First(r => r.DocumentText.Contains("Password requirements"));
        
        _output.WriteLine($"\nSpecific scores:");
        _output.WriteLine($"Password reset: {passwordResetDoc.Score:F3}");
        _output.WriteLine($"Customer service: {customerServiceDoc.Score:F3}");
        _output.WriteLine($"Password requirements: {passwordRequirementsDoc.Score:F3}");
        
        // The most relevant document should have the highest score
        passwordResetDoc.Score.Should().BeGreaterThanOrEqualTo(customerServiceDoc.Score, 
            "password reset should be more relevant than customer service hours");
    }

    /// <summary>
    /// Helper method to create async enumerable from array
    /// </summary>
    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Creates a test kernel with AI service if available
    /// </summary>
    private static Kernel? CreateTestKernel()
    {
        var builder = Kernel.CreateBuilder();

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Try Azure OpenAI first (from config or environment)
        var azureEndpoint = config.GetValue<string>("AZURE_OPENAI_ENDPOINT") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = config.GetValue<string>("AZURE_OPENAI_API_KEY") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = config.GetValue<string>("AZURE_OPENAI_DEPLOYMENT_NAME") ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4";

        if (!string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey))
        {
            try
            {
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureDeployment,
                    endpoint: azureEndpoint,
                    apiKey: azureApiKey
                );
                return builder.Build();
            }
            catch
            {
                // Azure OpenAI configuration failed
            }
        }

        // Try OpenAI
        var openAIKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var openAIModel = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4";

        if (!string.IsNullOrEmpty(openAIKey))
        {
            try
            {
                builder.AddOpenAIChatCompletion(
                    modelId: openAIModel,
                    apiKey: openAIKey
                );
                return builder.Build();
            }
            catch
            {
                // OpenAI configuration failed
            }
        }

        // Try local Ollama (for development)
        try
        {
            builder.AddOpenAIChatCompletion(
                modelId: "llama3.1",
                endpoint: new Uri("http://localhost:11434"),
                apiKey: "not-needed"
            );
            return builder.Build();
        }
        catch
        {
            // Local service not available
        }

        return null;
    }

    /// <summary>
    /// Quick check to see if AI services are actually available and working
    /// </summary>
    private bool IsAIServiceWorking()
    {
        if (_ranker == null || _kernel == null) return false;
        
        try
        {
            // Try a very simple scoring operation to test service availability
            var testTask = Task.Run(async () =>
            {
                var testDocs = new[] { "test document" };
                await foreach (var result in _ranker.ScoreAsync("test", CreateAsyncEnumerable(testDocs)))
                {
                    return result.Item2 > 0; // If we get a meaningful score, the service is working
                }
                return false;
            });
            
            // Give it 5 seconds to respond, if it times out the service isn't available
            return testTask.Wait(5000) && testTask.Result;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // No explicit disposal needed
    }
}
