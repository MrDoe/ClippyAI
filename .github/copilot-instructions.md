# ClippyAI - Desktop AI Assistant

ClippyAI is a cross-platform desktop AI assistant built with .NET 8.0 and Avalonia UI framework. It integrates with your system clipboard and uses local Ollama AI models to process text and images, providing intelligent responses directly to your clipboard.

**Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Working Effectively

### Bootstrap and Build the Project
Bootstrap the project in this exact order:
1. `git submodule update --init --recursive` -- initializes required git submodules (takes ~1 second)
2. `dotnet restore` -- downloads NuGet packages (takes ~18 seconds on first run)
3. `dotnet build` -- compiles the project (takes ~15 seconds). NEVER CANCEL.

### Build Commands and Timing
**CRITICAL BUILD TIMINGS - NEVER CANCEL ANY BUILD COMMAND:**
- `dotnet restore`: 18 seconds on first run, 2 seconds on subsequent runs
- `dotnet build`: 15 seconds for Debug, 6 seconds for Release
- `dotnet build -c Release`: 6 seconds  
- `dotnet clean`: 1 second
- Full clean build cycle: `dotnet clean && dotnet restore && dotnet build` takes ~25 seconds total

**Set timeout to 60+ seconds minimum for any dotnet build command. These are fast builds.**

### Project Structure
- **Main Project**: `/ClippyAI/ClippyAI.csproj` - Main Avalonia UI application
- **Key Directories**:
  - `ViewModels/` - MVVM view models (MainViewModel, ConfigurationDialogViewModel, etc.)
  - `Views/` - Avalonia XAML UI files (MainView, ConfigurationDialog, etc.)
  - `Services/` - Core services (OllamaService, ClipboardService, HotkeyService, etc.)
  - `Libs/` - Git submodules for evdev-sharp and DesktopNotificationsNet8
  - `Docker/` - PostgreSQL pgai setup for embeddings database
- **Configuration**: `App.config` contains default settings for Ollama URL, models, database connection
- **Entry Point**: `Program.cs` - Standard Avalonia application startup

## Build and Test

### Testing
- **No Unit Tests**: This project does not have unit tests. `dotnet test` returns immediately.
- **Test Data**: `Test_Email_Eng` file contains sample email data for manual testing.

### Running the Application
- **Local Development**: `dotnet run --project ClippyAI/ClippyAI.csproj`
- **Note**: Application requires desktop environment and may fail in sandboxed environments due to D-Bus permissions for notifications
- **Dependencies**: Requires Ollama service running on localhost:11434 (see External Dependencies section)

### Validation After Changes
Always run these commands after making changes:
1. `dotnet build` -- ensure code compiles (15-20 seconds)
2. `dotnet run --project ClippyAI/ClippyAI.csproj` -- briefly test startup (may fail due to missing desktop environment, but should reach initialization)

### Build Warnings (Expected)
The following warnings are normal and do not indicate build failures:
- `NU1902: Package 'SSH.NET' 2020.0.1 has a known moderate severity vulnerability` - Known dependency issue
- `NETSDK1206: Found version-specific runtime identifier` - Runtime targeting warnings

## External Dependencies

### Required for Full Functionality
- **Ollama**: Local AI service at `http://localhost:11434`
  - Install from: https://ollama.com/download
  - Default model: `gemma2:latest` (configured in App.config)
  - Vision model: `llama3.2-vision:latest` for image analysis

### Optional Dependencies
- **PostgreSQL with pgai**: For embeddings-based template caching
  - Use Docker setup: `cd ClippyAI/Docker && docker-compose up`
  - Connection string in App.config: `Host=localhost;Port=5432;Username=clippy;Password=password;Database=ClippyAI`
  - Embedding model: `nomic-embed-text`

## Platform-Specific Information

### Target Frameworks
- **Linux**: `net8.0`
- **Windows**: `net8.0-windows10.0.17763.0`

### Platform-Specific Features  
- **Windows**: Uses NHotkey.Wpf for global hotkeys
- **Linux**: Uses evdev-sharp for keyboard input handling
- **Cross-platform**: Uses DesktopNotifications for system notifications

## Development Workflow

### VS Code Tasks Available
The `.vscode/tasks.json` defines several useful tasks:
- `build` - Standard debug build
- `build-release` - Release configuration build  
- `run` - Start the application
- `watch` - File watcher mode for development
- `publish-windows` - Windows-specific publish
- `publish-linux-deb/rpm/tar` - Linux package creation

### Making Changes
1. Always build first: `dotnet build`
2. Test compilation before committing
3. For UI changes: Focus on Views/ and ViewModels/ directories
4. For functionality: Services/ directory contains business logic
5. Configuration changes: App.config file

### Architecture Notes
- **MVVM Pattern**: Uses CommunityToolkit.Mvvm with ReactiveUI
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Database**: SQLite for local storage, PostgreSQL for embeddings
- **AI Integration**: Direct HTTP calls to Ollama API
- **Image Processing**: Emgu.CV for computer vision features

## Common Issues and Solutions

### Build Issues
- **Missing submodules**: Run `git submodule update --init --recursive`
- **Package restore fails**: Check internet connection, try `dotnet restore --force`
- **Build warnings about SSH.NET**: Expected - known vulnerability in older package version

### Runtime Issues
- **D-Bus permissions**: Normal in sandboxed environments - app initializes desktop notifications
- **Ollama connection**: Ensure Ollama service is running and accessible at configured URL
- **Keyboard shortcuts**: Linux requires appropriate permissions for /dev/input devices

## Validation Scenarios

### After Making Code Changes
1. **Build Validation**: `dotnet build` completes successfully
2. **Startup Test**: `dotnet run` reaches application initialization (may fail on desktop integration but should not crash immediately)
3. **Configuration Load**: App should load settings from App.config without errors

### For UI Changes  
- Focus on XAML files in Views/ directory and corresponding ViewModels
- Test different window states and dialogs if applicable
- Verify MVVM bindings work correctly

### For Service Changes
- Review Services/ directory for business logic
- Ensure proper error handling for external service calls (Ollama, database)
- Check configuration file compatibility

**Remember**: This is a desktop application with external dependencies. Full functionality requires Ollama AI service and desktop environment, but the code should build and start initialization in any .NET 8.0 environment.