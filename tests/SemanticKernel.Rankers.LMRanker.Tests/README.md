# LMRanker Tests

This test suite contains comprehensive tests for the Language Model Reranker implementation.

## Test Categories

### Unit Tests (Always Run)
- **LMRankerBasicTests**: Core functionality and edge cases that don't require AI services
- These tests run in all environments including CI pipelines

### Integration Tests (Conditionally Run)
- **LMRankerIntegrationTests**: Real-world scenarios with actual AI services
- **LMRankerPerformanceTests**: Performance and stress testing
- **LMRankerDebugTests**: Debugging and diagnostic tests

Integration tests are automatically skipped when:
- `SKIP_INTEGRATION_TESTS` environment variable is set to `"true"`
- No AI service is configured or available
- AI service fails to respond within the timeout period

## Configuration

### CI Environments
Tests are designed to work in CI environments without requiring AI services. Integration tests will automatically skip when no service is available.

### Local Development
To run integration tests locally, configure an AI service using one of these methods:

#### Option 1: Environment Variables
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

#### Option 2: OpenAI
```bash
export OPENAI_API_KEY="your-openai-key"
export OPENAI_MODEL="gpt-4"
```

#### Option 3: Local Ollama
```bash
# Start Ollama locally on localhost:11434 with llama3.1 model
ollama serve
ollama pull llama3.1
```

#### Option 4: Update appsettings.json
Edit `appsettings.json` in this directory with your AI service credentials.

## Running Tests

```bash
# Run all tests (integration tests will skip if no AI service available)
dotnet test

# Force skip integration tests
SKIP_INTEGRATION_TESTS=true dotnet test

# Run only unit tests (no AI service required)  
dotnet test --filter "FullyQualifiedName~BasicTests"
```