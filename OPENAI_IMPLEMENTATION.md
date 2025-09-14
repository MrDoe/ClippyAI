# OpenAI API Support Implementation

## Overview
Added support for OpenAI API as an alternative to Ollama, allowing users to choose between local AI (Ollama) and cloud AI (OpenAI) services.

## Key Changes

### 1. Architecture Changes
- **Abstraction Layer**: Created `IAIProvider` interface to abstract AI provider functionality
- **Provider Pattern**: Both Ollama and OpenAI implement the same interface
- **Backward Compatibility**: Existing code continues to work unchanged

### 2. New Files Added
- `ClippyAI/Interfaces/IAIProvider.cs` - AI provider interface
- `ClippyAI/Models/OpenAIModels.cs` - OpenAI request/response models  
- `ClippyAI/Services/OpenAIService.cs` - OpenAI API implementation

### 3. Updated Files
- `ClippyAI/Services/OllamaService.cs` - Refactored to use provider pattern
- `ClippyAI/ViewModels/ConfigurationDialogViewModel.cs` - Added OpenAI configuration properties
- `ClippyAI/Views/ConfigurationDialog.axaml` - Updated UI for provider selection
- `ClippyAI/App.config` - Added default OpenAI configuration values

### 4. New Configuration Options
```xml
<add key="AIProvider" value="Ollama" />                    <!-- "Ollama" or "OpenAI" -->
<add key="OpenAIApiKey" value="" />                        <!-- Your OpenAI API key -->
<add key="OpenAIBaseUrl" value="https://api.openai.com/v1" />  <!-- OpenAI API URL -->
<add key="OpenAIModel" value="gpt-3.5-turbo" />           <!-- Default OpenAI model -->
<add key="OpenAIVisionModel" value="gpt-4-vision-preview" />   <!-- Vision model -->
```

## How It Works

### Provider Selection
Users can select between "Ollama" and "OpenAI" in the configuration dialog. The application automatically routes requests to the appropriate provider.

### Feature Support Matrix
| Feature | Ollama | OpenAI |
|---------|--------|--------|
| Text Generation | ✅ | ✅ |
| Vision/Image Analysis | ✅ | ✅ |
| Model Pulling | ✅ | ❌ |
| Model Deletion | ✅ | ❌ |
| Streaming | ✅ | ✅* |
| Embeddings | ✅ | ❌** |

*OpenAI streaming not implemented in this version but supported by the interface
**OpenAI embeddings would require separate API calls

### Configuration UI
The General Settings tab now includes:
1. **AI Provider dropdown** - Choose between Ollama and OpenAI
2. **Ollama Settings section** - URL configuration for local Ollama
3. **OpenAI Settings section** - API key, base URL, and model configuration
4. **Model Management** - Pull/delete buttons only work with Ollama

## Usage Examples

### OpenAI Setup
1. Obtain an OpenAI API key from https://platform.openai.com/
2. Open Configuration → General Settings
3. Select "OpenAI" from AI Provider dropdown
4. Enter your API key in the OpenAI API Key field
5. Optionally customize the OpenAI Model (default: gpt-3.5-turbo)
6. Save configuration

### Switching Providers
Users can switch between providers at any time:
- Change AI Provider in configuration
- Save settings
- The application immediately uses the new provider for all requests

## Implementation Details

### OllamaService Changes
The `OllamaService` class now acts as a facade that:
1. Determines the current provider from configuration
2. Routes calls to the appropriate provider instance
3. Maintains all existing static methods for backward compatibility

### OpenAI Integration
- Uses OpenAI Chat Completions API
- Supports GPT-3.5-turbo, GPT-4, and vision models
- Automatically handles message formatting (system + user prompts)
- Maps task configuration parameters to OpenAI equivalents

### Error Handling
- OpenAI API key validation
- Graceful fallback for model listing when API calls fail
- Provider-specific error messages
- Capability-based feature enabling/disabling

## Future Enhancements
1. **OpenAI Streaming** - Implement real-time response streaming
2. **Additional Providers** - Anthropic Claude, Cohere, etc.
3. **Provider-specific Settings** - Advanced configuration per provider
4. **Cost Tracking** - Monitor OpenAI API usage and costs
5. **Embedding Support** - OpenAI embeddings for similarity search