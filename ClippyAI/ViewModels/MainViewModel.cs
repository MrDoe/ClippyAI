using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ClippyAI.Models;
using ClippyAI.Services;
using ClippyAI.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
#if WINDOWS
using DirectShowLib;
#endif
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ClippyAI.Views;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        LoadTaskConfigurations();
        LoadVideoDevices();
    }

    public MainWindow? mainWindow;
    private CancellationTokenSource _askClippyCts = new();
    private bool initialized = false;

    [ObservableProperty]
    private string? _clipboardContent = "";

    [ObservableProperty]
    private string? _input;

    [ObservableProperty]
    private string? _output;

    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private string? _clippyResponse;

    [ObservableProperty]
    private ObservableCollection<TaskConfiguration> _taskConfigurations = [];

    [ObservableProperty]
    private TaskConfiguration? _selectedTaskConfiguration;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _autoMode = Convert.ToBoolean(ConfigurationService.GetConfigurationValue("AutoMode", "False"));

    [ObservableProperty]
    private string _language = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English") ?? "English";

    [ObservableProperty]
    private ObservableCollection<string> _languageItems = ["English", "Deutsch", "Français", "Español"];

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = OllamaService.GetModels();

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://127.0.0.1:11434/api") ?? "http://127.0.0.1:11434/api";

    [ObservableProperty]
    private string _model = ConfigurationService.GetConfigurationValue("OllamaModel", "") ?? "";

    [ObservableProperty]
    private bool _useEmbeddings = ConfigurationService.GetConfigurationValue("UseEmbeddings") == "True";

    [ObservableProperty]
    private bool _storeAllResponses = ConfigurationService.GetConfigurationValue("StoreAllResponses") == "True";

    [ObservableProperty]
    private string _responseCounter = "0 / 0";

    [ObservableProperty]
    private float _threshold = 0.2f;

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private float _responseDistance = 0.0f;

    [ObservableProperty]
    private string _videoDevice = ConfigurationService.GetConfigurationValue("VideoDevice") ?? "";

    [ObservableProperty]
    private ObservableCollection<string> _videoDevices = [];

    [ObservableProperty]
    private Bitmap? _clipboardImage;

    [ObservableProperty]
    private bool _isTextInputVisible = true;

    [ObservableProperty]
    private bool _isImageInputVisible = false;

    private bool _lastOutputGenerated = false;

    /// <summary>
    /// List of responseList retrieved from vector database
    /// </summary>
    private List<Embedding> _responseList = [];

    /// <summary>
    /// Index of the current response in responseList
    /// </summary>
    private int _responseIndex = 0;

    private void LoadTaskConfigurations()
    {
        try
        {
            ConfigurationService.InitializeDatabase();
            List<TaskConfiguration> tasks = ConfigurationService.GetAllTaskConfigurations();
            TaskConfigurations.Clear();
            foreach (TaskConfiguration task in tasks)
            {
                TaskConfigurations.Add(task);
            }

            // Set default task configuration if configured
            string defaultTaskName = ConfigurationService.GetConfigurationValue("DefaultTask");
            if (!string.IsNullOrEmpty(defaultTaskName))
            {
                SelectedTaskConfiguration = TaskConfigurations.FirstOrDefault(t => t.TaskName == defaultTaskName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading task configurations: {ex.Message}");
        }
    }
    private string GetFullTask()
    {
        // Use selected task configuration
        if (SelectedTaskConfiguration != null)
        {
            return SelectedTaskConfiguration.TaskName;
        }
        else
        {
            ErrorMessages?.Add(Resources.Resources.SelectTask);
            IsBusy = false;
            return "";
        }
    }

    [RelayCommand]
    public async Task AskClippy(CancellationToken token)
    {
        // If the selected task is an image task, dispatch to image analysis
        if (SelectedTaskConfiguration?.IsImageTask == true)
        {
            await CaptureAndAnalyze();
            return;
        }

        if (string.IsNullOrEmpty(Input))
        {
            // get text from clipboard
            if (await ClipboardService.GetText() is { } clipboardText)
            {
                Input = clipboardText;
                ClipboardService.LastInput = clipboardText;
            }
        }

        if (ModelItems.Count == 0)
        {
            ErrorMessages?.Add(Resources.Resources.NoModelsAvailable);
            IsBusy = false;
            return;
        }

        if (string.IsNullOrEmpty(Input))
        {
            return;
        }

        IsBusy = true;
        ErrorMessages?.Clear();
        _responseList.Clear();
        string? response = null;
        _lastOutputGenerated = false;
        bool usedEmbeddings = false;
        MainWindow? mainWindow = null;

        // get task string
        string task = GetFullTask();

        try
        {
            int modelIndex = ModelItems.IndexOf(Model);
            if (modelIndex < 0)
            {
                ErrorMessages?.Add(Resources.Resources.SelectModel);
                return;
            }
            string model = ModelItems[modelIndex];

            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                mainWindow = (MainWindow)desktop.MainWindow!;
                mainWindow.ShowNotification("ClippyAI", Resources.Resources.PleaseWait, true, false);
            }
            else
            {
                throw new NullReferenceException("Missing MainWindow instance.");
            }

            // try to get response from embedded model first
            if (UseEmbeddings && !string.IsNullOrEmpty(Input))
            {
                _responseList = await OllamaService.RetrieveAnswersForTask(task, Input, Threshold);
                if (_responseList.Count > 0)
                {
                    _responseIndex = 0;
                    response = _responseList[0].Answer;
                    usedEmbeddings = true;
                }
            }

            if (!usedEmbeddings)
            {
                // Use task configuration if available, otherwise use the current model
                response = SelectedTaskConfiguration != null
                    ? await OllamaService.SendRequestWithConfig(Input!,
                                                               task,
                                                               SelectedTaskConfiguration.Model,
                                                               SelectedTaskConfiguration,
                                                               _askClippyCts.Token)
                    : await OllamaService.SendRequestForTask(Input!,
                                                               task,
                                                               model,
                                                               _askClippyCts.Token);

                if (!string.IsNullOrEmpty(response) && !string.IsNullOrEmpty(task) &&
                    !string.IsNullOrEmpty(Input) && StoreAllResponses && UseEmbeddings)
                {
                    if (await OllamaService.StoreEmbedding(task, Input, response))
                    {
                        ++EmbeddingsCount;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The user pressed Stop. The finally block restores the UI and there
            // is nothing to report, because the user asked for the cancellation.
        }
        catch (TimeoutException e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);

            // show error message in dialog
            ShowErrorMessage(e.Message);
        }
        finally
        {
            // Always leave the UI usable. Previously IsBusy was only cleared by
            // SetResponse, which never runs when no response arrives, so a
            // cancelled, timed out or failed request left the app in the
            // "Please wait" state forever.
            IsBusy = false;
            mainWindow?.HideLastNotification();
            Input = null;
        }

        if (response != null)
        {
            if (!usedEmbeddings)
            {
                _lastOutputGenerated = true;
                _responseList.Add(new Embedding { Id = 0, Answer = response });
            }
            await SetResponse(response);
        }
    }

    private async Task SetResponse(string response, bool showNotification = true)
    {
        ClipboardContent = response;
        Output = response;
        ClipboardService.LastResponse = response;

        // Update the clipboard content
        await ClipboardService.SetText(ClipboardContent);

        // call ShowNotification method from MainWindow
        if (showNotification)
        {
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow mainWindow = (MainWindow)desktop.MainWindow!;
                mainWindow!.HideLastNotification();
                mainWindow.ShowNotification("ClippyAI", Resources.Resources.TaskCompleted + response, false, true);
            }
            else
            {
                throw new NullReferenceException("Missing MainWindow instance.");
            }
        }

        // update response counter
        if (_lastOutputGenerated)
        {
            ResponseCounter = "      *";
        }
        else if (_responseIndex >= 0 && _responseIndex < _responseList.Count)
        {
            // Manual indexing: the list is empty whenever a response did not come
            // from a completed comparison run (e.g. manual generator) - indexing
            // it unguarded threw IndexOutOfRangeException on every such response.
            ResponseDistance = (float)Math.Round(_responseList[_responseIndex].Distance, 2);
            ResponseCounter = $"{_responseIndex + 1} / {_responseList.Count}";
        }
        IsBusy = false;
    }

    /// <summary>
    /// Get next response from the list of responseList
    /// </summary>
    [RelayCommand]
    public async Task GetNextResponse()
    {
        if (_lastOutputGenerated)
        {
            return;
        }

        if (_responseList.Count > 0)
        {
            _responseIndex++;
            if (_responseIndex >= _responseList.Count)
            {
                _responseIndex = 0;
            }
            await SetResponse(_responseList[_responseIndex].Answer, false);
        }
    }

    [RelayCommand]
    public void StopClippyTask()
    {
        // Cancel whatever is running and always install a fresh source, so the
        // next request can never inherit an already-cancelled token.
        _askClippyCts.Cancel();
        _askClippyCts = new CancellationTokenSource();
    }

    [RelayCommand]
    public async Task CopyText(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            await ClipboardService.SetText(ClipboardContent);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    [RelayCommand]
    public async Task PasteText(CancellationToken token)
    {
        ErrorMessages?.Clear();
        try
        {
            if (await ClipboardService.GetText() is { } pastedText)
            {
                ClipboardContent = ClipboardContent?.Insert(CaretIndex, pastedText);
            }
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    // SHA-256 (hex) of the PNG encoding of the last processed clipboard image.
    // The clipboard hands out a fresh Bitmap instance on every read, so a
    // reference comparison would treat the same screenshot as "new" every
    // second; the content hash is the only reliable change signal.
    private string? _clipboardImageSha;

    // updates the clipboard content regularly
    public async Task UpdateClipboardContent(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        // First check text content (most common case)
        string? newContent;
        try
        {
            newContent = await ClipboardService.GetText();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                // Ignore the exception if the clipboard does not contain text
                newContent = null;
            }
            else
            {
                ErrorMessages?.Add(e.Message);
                return;
            }
        }

        // Check if text content has changed
        bool textContentChanged = !string.IsNullOrEmpty(newContent) && newContent != ClipboardContent;

        if (textContentChanged)
        {
            IsBusy = true;

            // Update the text property
            ClipboardContent = newContent;
            Input = newContent ?? "";
            ClipboardService.LastInput = newContent ?? "";
            ClipboardHistoryService.RecordText(newContent);

            // AutoMode runs text tasks only: with an image task selected, a
            // copied text would trigger image analysis of a stale screenshot
            // and the response would ping-pong through the clipboard again.
            if (AutoMode && initialized && SelectedTaskConfiguration?.IsImageTask != true)
            {
                await AskClippy(cancellationToken);
            }
            else
            {
                initialized = true;
            }

            // When text changes, prioritize text view
            IsImageInputVisible = false;
            IsTextInputVisible = true;

            IsBusy = false;
        }

        // Images are evaluated every cycle now. The old early exit after a
        // text change lost screenshots that arrive together with text
        // (Office/browser copies carry both), and never fed them to the
        // history or to AutoMode image processing.
        await UpdateClipboardImage(cancellationToken);
    }

    private async Task UpdateClipboardImage(CancellationToken cancellationToken)
    {
        string[]? formats = await ClipboardService.GetFormatsAsync();
        bool hasBitmap = formats?.Any(f => string.Equals(f, DataFormat.Bitmap.Identifier, StringComparison.OrdinalIgnoreCase)) == true;

        if (!hasBitmap)
        {
            if (ClipboardImage != null)
            {
                ClipboardImage.Dispose();
                ClipboardImage = null;
                _clipboardImageSha = null;
                IsImageInputVisible = false;
                IsTextInputVisible = true;
            }

            ClipboardService.MarkImageSynced();
            return;
        }

        // Cheap Windows clipboard-sequence gate: skip re-decoding while the
        // clipboard payload is unchanged.
        if (!ClipboardService.ImagePayloadMayHaveChanged())
        {
            return;
        }

        ClipboardService.MarkImageSynced();

        ClipboardImageSnapshot? snapshot = await ClipboardService.GetImageSnapshotAsync();
        if (snapshot == null)
        {
            return;
        }

        string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(snapshot.PngBytes));
        if (sha == _clipboardImageSha)
        {
            // Same image (the sequence probably changed due to another format).
            snapshot.Bitmap.Dispose();
            return;
        }

        Bitmap? previous = ClipboardImage;
        ClipboardImage = snapshot.Bitmap;
        previous?.Dispose();
        _clipboardImageSha = sha;

        ClipboardHistoryService.RecordImage(snapshot.PngBytes);

        IsImageInputVisible = true;
        IsTextInputVisible = false;

        // AutoMode with a clipboard-sourced image task selected: a new
        // screenshot is analyzed automatically - the intended flow for
        // "copy screenshot, get the result".
        if (AutoMode && initialized
            && SelectedTaskConfiguration is { IsImageTask: true } imageTask
            && imageTask.ImageSource == TaskConfiguration.ImageSourceClipboard
            && !IsBusy)
        {
            await CaptureAndAnalyze();
        }
    }

    // ==================== Clipboard history ====================

    [ObservableProperty]
    private ObservableCollection<ClipboardHistoryItem> _historyItems = [];

    [ObservableProperty]
    private ClipboardHistoryItem? _selectedHistoryItem;

    [ObservableProperty]
    private string _historyFilter = "";

    partial void OnHistoryFilterChanged(string value) => LoadHistory();

    public void LoadHistory()
    {
        List<ClipboardHistoryItem> items = ClipboardHistoryService.GetItems(HistoryFilter);
        HistoryItems.Clear();
        foreach (ClipboardHistoryItem item in items)
        {
            HistoryItems.Add(item);
        }
    }

    [RelayCommand]
    private async Task ShowHistory()
    {
        if (mainWindow == null)
        {
            return;
        }

        LoadHistory();
        ClipboardHistoryWindow window = new() { DataContext = this };
        await window.ShowDialog(mainWindow);
    }

    /// <summary>Copies the selected history entry back to the clipboard.</summary>
    [RelayCommand]
    private async Task RestoreHistoryItem()
    {
        if (SelectedHistoryItem is not { } item)
        {
            return;
        }

        try
        {
            if (item.IsImage)
            {
                if (item.ImagePath == null || !File.Exists(item.ImagePath))
                {
                    ShowErrorMessage("History image file is missing.");
                    return;
                }

                await ClipboardService.SetImageFile(item.ImagePath);
                LoadImageIntoClipboardSlot(File.ReadAllBytes(item.ImagePath));
            }
            else
            {
                await ClipboardService.SetText(item.Content);
                ClipboardContent = item.Content;
                Input = item.Content;
                ClipboardService.LastInput = item.Content ?? "";
                IsTextInputVisible = true;
                IsImageInputVisible = false;
            }

            mainWindow?.ShowNotification("ClippyAI", Resources.Resources.HistoryRestored, false, false, 3000);
        }
        catch (Exception ex)
        {
            ErrorMessages?.Add(ex.Message);
            ShowErrorMessage(ex.Message);
        }
    }

    /// <summary>Runs the currently selected task against the selected history entry.</summary>
    [RelayCommand]
    private async Task ExecuteHistoryItem()
    {
        if (SelectedHistoryItem is not { } item)
        {
            return;
        }

        try
        {
            if (item.IsImage)
            {
                if (item.ImagePath == null || !File.Exists(item.ImagePath))
                {
                    ShowErrorMessage("History image file is missing.");
                    return;
                }

                LoadImageIntoClipboardSlot(File.ReadAllBytes(item.ImagePath));
                await CaptureAndAnalyze();
            }
            else
            {
                Input = item.Content;
                ClipboardService.LastInput = item.Content ?? "";
                await AskClippy(_askClippyCts.Token);
            }
        }
        catch (Exception ex)
        {
            ErrorMessages?.Add(ex.Message);
            ShowErrorMessage(ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteHistoryItem()
    {
        if (SelectedHistoryItem is { } item)
        {
            ClipboardHistoryService.Delete(item.Id);
            LoadHistory();
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        ClipboardHistoryService.ClearAll();
        LoadHistory();
    }

    private void LoadImageIntoClipboardSlot(byte[] pngBytes)
    {
        Bitmap bitmap;
        using (MemoryStream ms = new(pngBytes))
        {
            bitmap = new Bitmap(ms);
        }

        Bitmap? previous = ClipboardImage;
        ClipboardImage = bitmap;
        previous?.Dispose();
        _clipboardImageSha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pngBytes));
        ClipboardService.MarkImageSynced();
        IsImageInputVisible = true;
        IsTextInputVisible = false;
    }

    // ==================== Application manual generator ====================

    /// <summary>
    /// Creates a user manual from the selected (image) history entries: one
    /// vision pass per screenshot plus an assembly pass. Background knowledge
    /// (app profile + ClickHouse RAG) reaches the model through the dynamic
    /// system prompts; the ClickHouse augmentation happens centrally in
    /// OllamaService.SendRequestWithConfig/AnalyzeImage.
    /// </summary>
    public async Task GenerateManualAsync(IReadOnlyList<ClipboardHistoryItem> selectedItems)
    {
        if (mainWindow == null)
        {
            return;
        }

        List<ClipboardHistoryItem> images = selectedItems
            .Where(i => i.IsImage && i.ImagePath != null && File.Exists(i.ImagePath))
            .OrderBy(i => i.CreatedAt)
            .ToList();

        if (images.Count == 0)
        {
            ShowErrorMessage(Resources.Resources.ManualNeedsImages);
            return;
        }

        ManualDialogViewModel dialogVm = new(images.FirstOrDefault()?.SourceContext);
        ManualContextDialog dialog = new() { DataContext = dialogVm };
        bool? dialogConfirmed = await dialog.ShowDialog<bool>(mainWindow);
        if (dialogConfirmed != true)
        {
            return;
        }

        string appLine = dialogVm.AppName
                       + (string.IsNullOrWhiteSpace(dialogVm.AppVersion) ? "" : " " + dialogVm.AppVersion.Trim());
        string language = string.IsNullOrWhiteSpace(dialogVm.Language) ? "English" : dialogVm.Language.Trim();
        bool wasBusy = IsBusy;
        IsBusy = true;

        try
        {
            TaskConfiguration baseConfig = TaskConfigurations.FirstOrDefault(t =>
                    t.TaskName.Equals(ManualTaskName, StringComparison.OrdinalIgnoreCase))
                ?? new TaskConfiguration { TaskName = ManualTaskName, Model = Model, IsImageTask = true };

            StringBuilder sections = new();
            for (int i = 0; i < images.Count; i++)
            {
                mainWindow.ShowNotification("ClippyAI",
                    string.Format(Resources.Resources.ManualProgress, i + 1, images.Count), true, false);

                byte[] bytes = await File.ReadAllBytesAsync(images[i].ImagePath!);
                TaskConfiguration sectionConfig = CloneForPrompt(baseConfig,
                    BuildSectionPrompt(appLine, dialogVm, i + 1, images.Count, images[i].SourceContext));

                string section = await OllamaService.AnalyzeImage(bytes, sectionConfig);
                if (!string.IsNullOrWhiteSpace(section))
                {
                    sections.AppendLine(section.Trim());
                    sections.AppendLine();
                }
            }

            if (sections.Length == 0)
            {
                ShowErrorMessage("The vision model did not return any usable sections.");
                return;
            }

            mainWindow.ShowNotification("ClippyAI", Resources.Resources.ManualAssembling, true, false);

            string draft = sections.ToString();
            if (draft.Length > 14000)
            {
                draft = draft.Substring(0, 14000);
            }

            TaskConfiguration assemblyConfig = CloneForPrompt(baseConfig,
                BuildAssemblyPrompt(appLine, dialogVm, language));

            string? manual = await OllamaService.SendRequestWithConfig(draft, ManualTaskName,
                baseConfig.Model, assemblyConfig, _askClippyCts.Token);

            if (string.IsNullOrWhiteSpace(manual))
            {
                ShowErrorMessage("Manual assembly failed.");
                return;
            }

            await SetResponse(manual.Trim());

            if (dialogVm.SaveAsFile)
            {
                await SaveManualAsync(appLine, manual.Trim());
            }
        }
        catch (OperationCanceledException)
        {
            // Stop pressed - fine.
        }
        catch (Exception ex)
        {
            ErrorMessages?.Add(ex.Message);
            ShowErrorMessage(ex.Message);
        }
        finally
        {
            mainWindow.HideLastNotification();
            IsBusy = wasBusy;
        }
    }

    public const string ManualTaskName = "Write Application Manual";

    private static TaskConfiguration CloneForPrompt(TaskConfiguration source, string systemPrompt) => new()
    {
        Id = source.Id,
        TaskName = source.TaskName,
        SystemPrompt = systemPrompt,
        Model = source.Model,
        Temperature = source.Temperature,
        MaxLength = Math.Max(source.MaxLength, 4096),
        TopP = source.TopP,
        TopK = source.TopK,
        RepeatPenalty = source.RepeatPenalty,
        NumCtx = Math.Max(source.NumCtx, 8192),
        IsActive = source.IsActive,
        IsImageTask = source.IsImageTask,
        ImageSource = source.ImageSource,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt
    };

    private string BuildSectionPrompt(string appLine, ManualDialogViewModel vm, int index, int total, string? windowContext)
    {
        StringBuilder sb = new();
        sb.AppendLine($"You are a precise technical writer producing one section of a user manual for the application \"{appLine}\".");
        if (!string.IsNullOrWhiteSpace(vm.AppNotes))
        {
            sb.AppendLine($"Application purpose and background: {vm.AppNotes.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(vm.Audience))
        {
            sb.AppendLine($"Target audience: {vm.Audience.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(windowContext))
        {
            sb.AppendLine($"Captured in window/process: {windowContext.Trim()}");
        }

        sb.AppendLine("Additional authentic background knowledge may be appended below; treat it as factual.");
        sb.AppendLine($"Write the section in {vm.Language}.");
        sb.AppendLine($"This is screenshot {index} of {total}. Write ONLY the Markdown section for the visible screen or dialog:");
        sb.AppendLine("- '## <screen/dialog title>' derived from the visible title bar or tabs, not invented");
        sb.AppendLine("- one short description paragraph of what this screen is for");
        sb.AppendLine("- a bulleted '### Controls' list of the visible interactive elements and what they do");
        sb.AppendLine("- a short '### Workflow' paragraph of the typical task performed here");
        sb.AppendLine("Ground every statement in the screenshot and the background knowledge. Never invent menus, buttons or features that are not visible; say 'unknown' where unsure.");
        sb.Append("Output only the section Markdown, no preamble.");
        return sb.ToString();
    }

    private static string BuildAssemblyPrompt(string appLine, ManualDialogViewModel vm, string language)
    {
        return $"You are a technical editor. Combine the draft sections provided as user content into ONE coherent user manual for \"{appLine}\" in {language}.\n" +
               "Structure: '# ' title line; a short introduction paragraph (use only given background, no marketing); a '## Contents' list linking the sections; the sections (you may smooth wording and fix headings, but add no new facts); a final '## Tips' section with 3-5 tips derived strictly from the sections.\n" +
               "Output only the complete Markdown document.";
    }

    private async Task SaveManualAsync(string appLine, string manual)
    {
        try
        {
            if (mainWindow?.StorageProvider is null)
            {
                return;
            }

            string safeName = string.Join("_", appLine.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (safeName.Length == 0)
            {
                safeName = "Manual";
            }

            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"{safeName}-Manual.md",
                DefaultExtension = "md",
                FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md", "*.txt"] }]
            });
            if (file is not null)
            {
                await using Stream stream = await file.OpenWriteAsync();
                await using StreamWriter writer = new(stream);
                await writer.WriteAsync(manual);
                mainWindow.ShowNotification("ClippyAI", Resources.Resources.HistoryRestored + " ✓ " + file.Name, false, false, 4000);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Manual file save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task AddModel()
    {
        try
        {
            // open input dialog to enter model name
            string? modelName = await InputDialog.Prompt(
                    parentWindow: mainWindow!,
                    title: Resources.Resources.PullModel,
                    caption: Resources.Resources.EnterModelName,
                    subtext: Resources.Resources.PullModelSubText,
                    isRequired: true
                );
            if (string.IsNullOrEmpty(modelName))
            {
                return;
            }

            await OllamaService.PullModelAsync(modelName);

            // update model items
            ModelItems = OllamaService.GetModels();
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }

    [RelayCommand]
    public void RefreshModels()
    {
        ModelItems = OllamaService.GetModels();
    }

    [RelayCommand]
    public async Task DeleteModel()
    {
        // confirm deletion
        string? confirmation = await InputDialog.Confirm(
            parentWindow: mainWindow!,
            title: Resources.Resources.DeleteModel,
            caption: Resources.Resources.ConfirmDeleteModel
        );
        if (confirmation != Resources.Resources.Yes)
        {
            return;
        }

        try
        {
            // get selected model
            string modelName = ModelItems[ModelItems.IndexOf(Model)];

            await OllamaService.DeleteModelAsync(modelName);

            // update model items
            ModelItems = OllamaService.GetModels();
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }

    [RelayCommand]
    public async Task ThumbUp()
    {
        if (string.IsNullOrEmpty(Output))
        {
            return;
        }

        if (_responseList.Count > 0 && _responseIndex >= 0 && !string.IsNullOrEmpty(Input))
        {
            try
            {
                string task = GetFullTask();
                if (!string.IsNullOrEmpty(task) && UseEmbeddings)
                {
                    if (!await OllamaService.StoreEmbedding(task, Input, Output))
                    {
                        mainWindow!.ShowNotification("ClippyAI", "A similar answer is already stored.", false, false);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                ErrorMessages?.Add(e.Message);
                mainWindow!.ShowNotification("ClippyAI", e.Message, false, true);
                return;
            }

            ++EmbeddingsCount;
            mainWindow!.ShowNotification("ClippyAI", Resources.Resources.ResultStoredSuccessfully, false, false);
        }
    }

    [RelayCommand]
    public async Task ThumbDown()
    {
        if (string.IsNullOrEmpty(Output))
        {
            return;
        }

        if (_responseList.Count > 0 && _responseIndex >= 0)
        {
            try
            {
                await OllamaService.DeleteEmbedding(_responseList[_responseIndex].Id);
            }
            catch (Exception e)
            {
                ErrorMessages?.Add(e.Message);
                mainWindow!.ShowNotification("ClippyAI", e.Message, false, true);
                return;
            }

            _responseList.RemoveAt(_responseIndex);
            if (_responseList.Count > 0)
            {
                _responseIndex = 0;
                await SetResponse(_responseList[0].Answer);
            }
            else
            {
                Output = "";
                mainWindow!.ShowNotification("ClippyAI", "Result successfully deleted from database!", false, false);
            }
        }
    }

    [RelayCommand]
    public async Task Regenerate()
    {
        bool embeddingsUsed = UseEmbeddings;
        if (embeddingsUsed)
        {
            UseEmbeddings = false;
        }

        await AskClippy(_askClippyCts.Token);

        if (embeddingsUsed)
        {
            UseEmbeddings = true;
        }
    }

    [RelayCommand]
    public async Task ClearEmbeddings()
    {
        // confirm deletion
        string? confirmation = await InputDialog.Confirm(
            parentWindow: mainWindow!,
            title: "Delete Embeddings",
            caption: "Do you really want to delete all embeddings?"
        );

        if (confirmation != Resources.Resources.Yes)
        {
            return;
        }

        try
        {
            await OllamaService.ClearEmbeddings();
            EmbeddingsCount = 0;
            mainWindow!.ShowNotification("ClippyAI", "Embeddings successfully deleted!", false, false);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
            ShowErrorMessage(e.Message);
        }
    }

    [RelayCommand]
    public void SelectPreviousTask()
    {
        if (TaskConfigurations == null || TaskConfigurations.Count == 0 || SelectedTaskConfiguration == null)
        {
            return;
        }

        int currentIndex = TaskConfigurations.IndexOf(SelectedTaskConfiguration);
        int newIndex = currentIndex <= 0 ? TaskConfigurations.Count - 1 : currentIndex - 1;

        SelectedTaskConfiguration = TaskConfigurations[newIndex];

        // Show notification with selected task
        mainWindow?.ShowNotification("ClippyAI", $"Task: {SelectedTaskConfiguration?.TaskName}", false, false, 800);
    }

    [RelayCommand]
    public void SelectNextTask()
    {
        if (TaskConfigurations == null || TaskConfigurations.Count == 0 || SelectedTaskConfiguration == null)
        {
            return;
        }

        int currentIndex = TaskConfigurations.IndexOf(SelectedTaskConfiguration);
        int newIndex = currentIndex >= TaskConfigurations.Count - 1 ? 0 : currentIndex + 1;

        SelectedTaskConfiguration = TaskConfigurations[newIndex];

        // Show notification with selected task
        mainWindow?.ShowNotification("ClippyAI", $"Task: {SelectedTaskConfiguration?.TaskName}", false, false, 800);
    }

    [RelayCommand]
    public async Task ConfigureHotkeyDevice()
    {
        // only for Linux
        if (!OperatingSystem.IsLinux())
        {
            ErrorMessages?.Add("This feature is only supported on Linux.");
            return;
        }
#if LINUX
        try
        {
            if (mainWindow != null)
            {
                await mainWindow.ConfigureHotkeyDeviceAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessages?.Add(ex.Message);
            ShowErrorMessage(ex.Message);
        }
#endif
    }

    [RelayCommand]
    public async Task CaptureAndAnalyze()
    {
        // show notification to the user
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindow mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow!.ShowNotification("ClippyAI", Resources.Resources.AnalyzeImage, true, false);
        }
        else
        {
            throw new NullReferenceException("Missing MainWindow instance.");
        }

        try
        {
            byte[] frame;
            string imageSource = SelectedTaskConfiguration?.ImageSource ?? TaskConfiguration.ImageSourceClipboard;

            if (imageSource == TaskConfiguration.ImageSourceWebcam)
            {
                // Capture a frame from the webcam
                frame = CaptureFrame();
            }
            else
            {
                // Check if there is an image in the clipboard
                if (ClipboardImage != null)
                {
                    using MemoryStream ms = new();
                    ClipboardImage.Save(ms);
                    frame = ms.ToArray();
                }
                else
                {
                    ErrorMessages?.Add(Resources.Resources.NoImageInClipboard);
                    ShowErrorMessage(Resources.Resources.NoImageInClipboard);
                    return;
                }
            }

            // Send the frame to Ollama for analysis using task-specific configuration
            string analysisResult = await OllamaService.AnalyzeImage(frame, SelectedTaskConfiguration);

            // Store the analysis result in the clipboard
            await ClipboardService.SetText(analysisResult);
            Output = analysisResult;
            mainWindow!.ShowNotification("ClippyAI", analysisResult, false, true);
        }
        catch (Exception ex)
        {
            ErrorMessages?.Add(ex.Message);
            ShowErrorMessage(ex.Message);
        }
    }

    private byte[] CaptureFrame()
    {
        VideoCapture capture;

        if (OperatingSystem.IsLinux())
        {
            // Use V4L2 API for Linux
            capture = new VideoCapture(VideoDevice, VideoCapture.API.V4L2);
        }
        else if (OperatingSystem.IsWindows()) // Use DirectShow API for Windows
        {
            // get the number of the video device from its name
            if (string.IsNullOrEmpty(VideoDevice))
            {
                VideoDevice = "0"; // default to the first camera
            }

            // Try to find the device number by name
            if (!int.TryParse(VideoDevice, out int deviceNumber))
            {
                // If the device name is not a number, try to find it by name
                List<string> devices = new();
#if WINDOWS
                DsDevice[] systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (DsDevice? device in systemDeviceEnum)
                {
                    devices.Add(device.Name);
                }

                deviceNumber = Array.IndexOf(devices.ToArray(), VideoDevice);
#endif
            }
            capture = new VideoCapture(deviceNumber, VideoCapture.API.DShow);
        }
        else
        {
            // Use the default API for other platforms
            capture = new VideoCapture(VideoDevice);
        }


        _ = capture.Set(CapProp.FrameWidth, 640);
        _ = capture.Set(CapProp.FrameHeight, 480);

        if (!capture.IsOpened)
        {
            throw new Exception("Could not open video device");
        }

        using Mat frame = new();
        _ = capture.Read(frame);
        if (frame.IsEmpty)
        {
            throw new Exception("Failed to capture image");
        }

        capture.Dispose();

        return frame.ToImage<Bgr, byte>().ToJpegData();
    }

    private void LoadVideoDevices()
    {
        List<string> devices = new();
        // Platform-independent way to get video devices
        if (OperatingSystem.IsWindows())
        {
            // Windows-specific code to get video devices
#if WINDOWS
            DsDevice[] systemDeviceEnum = Array.Empty<DsDevice>();
            systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            devices.AddRange(systemDeviceEnum.Select(device => device.Name));
#endif
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Unix-based systems code to get video devices
            for (int i = 0; i < 10; ++i)
            {
                string devicePath = $"/dev/video{i}";
                if (File.Exists(devicePath))
                {
                    devices.Add(devicePath);
                }
            }
        }
        VideoDevices = [.. devices];
    }

    [RelayCommand]
    public void RefreshVideoDevices()
    {
        LoadVideoDevices();
    }

    [RelayCommand]
    public void ShowCamera()
    {
        CameraWindow cameraWindow = new();
        cameraWindow.Show();
    }

    [RelayCommand]
    public void CaptureScreenshot()
    {
        throw new NotImplementedException("This feature is not implemented yet.");
    }

    /// <summary>
    /// Refresh all configuration values from the database after configuration changes.
    /// Call this method after saving configuration to apply changes immediately.
    /// </summary>
    public void RefreshConfigurations()
    {
        try
        {
            // Refresh task configurations
            LoadTaskConfigurations();

            // Refresh video devices
            LoadVideoDevices();

            // Refresh model items
            ModelItems = OllamaService.GetModels();

            // Refresh configuration values
            AutoMode = Convert.ToBoolean(ConfigurationService.GetConfigurationValue("AutoMode", "False"));
            Language = ConfigurationService.GetConfigurationValue("DefaultLanguage", "English") ?? "English";
            OllamaUrl = ConfigurationService.GetConfigurationValue("OllamaUrl", "http://127.0.0.1:11434/api") ?? "http://127.0.0.1:11434/api";
            Model = ConfigurationService.GetConfigurationValue("OllamaModel", "") ?? "";
            UseEmbeddings = ConfigurationService.GetConfigurationValue("UseEmbeddings") == "True";
            StoreAllResponses = ConfigurationService.GetConfigurationValue("StoreAllResponses") == "True";
            VideoDevice = ConfigurationService.GetConfigurationValue("VideoDevice") ?? "";
            Threshold = float.Parse(ConfigurationService.GetConfigurationValue("Threshold", "0.2"));

            System.Diagnostics.Debug.WriteLine("Configurations refreshed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error refreshing configurations: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task OpenConfiguration()
    {
        ConfigurationDialog dialog = new()
        {
            DataContext = new ConfigurationDialogViewModel(mainWindow)
        };

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow!);
        }
    }
}
