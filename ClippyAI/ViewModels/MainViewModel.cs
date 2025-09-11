using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClippyAI.Services;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Collections.Generic;
using ClippyAI.Models;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Linq;
using DirectShowLib;
using Avalonia.Media.Imaging;
using System.IO;
namespace ClippyAI.Views;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        PopulateTasks();
        LoadVideoDevices();
    }

    public MainWindow? mainWindow;
    private CancellationTokenSource _askClippyCts = new();
    private HotkeyService? hotkeyService;
    private bool initialized = false;

    [ObservableProperty]
    private string _task = ConfigurationManager.AppSettings["DefaultTask"] ?? Resources.Resources.Task_1;

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
    private ObservableCollection<string> taskItems = [];

    [ObservableProperty]
    private string _customTask = "";

    [ObservableProperty]
    private bool _showCustomTask = false;

    [ObservableProperty]
    private bool _isBusy = false;

    [ObservableProperty]
    private bool _autoMode = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoMode"]);

    [ObservableProperty]
    private string _language = ConfigurationManager.AppSettings["DefaultLanguage"] ?? "English";

    [ObservableProperty]
    private ObservableCollection<string> _languageItems = ["English", "Deutsch", "Français", "Español"];

    [ObservableProperty]
    private ObservableCollection<string> _modelItems = OllamaService.GetModels();

    [ObservableProperty]
    private string _ollamaUrl = ConfigurationManager.AppSettings["OllamaUrl"] ?? "http://127.0.0.1:11434/api";

    [ObservableProperty]
    private string _model = ConfigurationManager.AppSettings["OllamaModel"] ?? "gemma2:latest";

    [ObservableProperty]
    private string _PostgreSqlConnection = ConfigurationManager.AppSettings["PostgreSqlConnection"] ?? "";

    [ObservableProperty]
    private string _PostgresOllamaUrl = ConfigurationManager.AppSettings["PostgresOllamaUrl"] ?? "";

    [ObservableProperty]
    private bool _useEmbeddings = ConfigurationManager.AppSettings["UseEmbeddings"] == "True";

    [ObservableProperty]
    private bool _storeAllResponses = ConfigurationManager.AppSettings["StoreAllResponses"] == "True";

    [ObservableProperty]
    private string _responseCounter = "0 / 0";

    [ObservableProperty]
    private float _threshold = 0.2f;

    [ObservableProperty]
    private int _embeddingsCount = 0;

    [ObservableProperty]
    private float _responseDistance = 0.0f;

    [ObservableProperty]
    private string _videoDevice = ConfigurationManager.AppSettings["VideoDevice"] ?? "";

    [ObservableProperty]
    private string _visionModel = ConfigurationManager.AppSettings["VisionModel"] ?? "llama3.2-vision";

    [ObservableProperty]
    private string _visionPrompt = ConfigurationManager.AppSettings["VisionPrompt"] ?? "Detect what you can find in the image. Use markdown to format the text.";

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

    private void PopulateTasks()
    {
        // iterate over Resources and add Tasks to ComboBox
        foreach (var property in typeof(Resources.Resources).GetProperties())
        {
            if (property.Name.StartsWith("Task_"))
            {
                var value = property.GetValue(null)?.ToString();
                if (value != null)
                {
                    TaskItems.Add(value);
                }
            }
        }
    }
    private string GetFullTask()
    {
        string task;

        // user defined task
        if (Task == Resources.Resources.Task_15)
            task = CustomTask;
        else
            task = Task;

        if (string.IsNullOrEmpty(task))
        {
            ErrorMessages?.Add(Resources.Resources.SelectTask);
            IsBusy = false;
            return "";
        }

        return task;
    }

    [RelayCommand]
    public async Task AskClippy(CancellationToken token)
    {
        if (string.IsNullOrEmpty(Input))
        {
            // get text from clipboard
            if (await ClipboardService.GetText() is { } clipboardText)
            {
                Input = clipboardText;
                ClipboardService.LastInput = clipboardText;
            }
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

        // get task string
        string task = GetFullTask();

        try
        {
            string model = ModelItems[ModelItems.IndexOf(Model)];
            if (model == null)
            {
                ErrorMessages?.Add(Resources.Resources.SelectModel);
                IsBusy = false;
                return;
            }

            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = (MainWindow)desktop.MainWindow!;
                mainWindow!.ShowNotification("ClippyAI", Resources.Resources.PleaseWait, true, false);
            }
            else
            {
                throw new NullReferenceException("Missing MainWindow instance.");
            }

            // try to get response from embedded model first
            if (UseEmbeddings && !string.IsNullOrEmpty(Input))
            {
                _responseList = await OllamaService.RetrieveAnswersForTask(Task, Input, Threshold);
                if (_responseList.Count > 0)
                {
                    _responseIndex = 0;
                    await SetResponse(_responseList[0].Answer);
                    return;
                }
            }

            response = await OllamaService.SendRequestForTask(Input!,
                                                       task,
                                                       model,
                                                       _askClippyCts.Token);

            if (!string.IsNullOrEmpty(response) && !string.IsNullOrEmpty(Task) &&
                !string.IsNullOrEmpty(Input) && StoreAllResponses)
            {
                await OllamaService.StoreSqlEmbedding(Task, Input, response);
                ++EmbeddingsCount;
            }
        }
        catch (OperationCanceledException)
        {
            // Handle the task cancellation here if needed
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);

            // show error message in dialog
            ShowErrorMessage(e.Message);
        }

        if (response != null)
        {
            _lastOutputGenerated = true;
            _responseList.Add(new Embedding { Id = 0, Answer = response });
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
                var mainWindow = (MainWindow)desktop.MainWindow!;
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
            ResponseCounter = "      *";
        else
        {
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
        if (!_askClippyCts.IsCancellationRequested)
        {
            _askClippyCts.Cancel(); // Cancel the ongoing task
            _askClippyCts = new CancellationTokenSource(); // Reset the CancellationTokenSource for future use
        }
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
                ClipboardContent = ClipboardContent?.Insert(CaretIndex, pastedText);
        }
        catch (Exception e)
        {
            ErrorMessages?.Add(e.Message);
        }
    }

    // updates the clipboard content regularly
    public async Task UpdateClipboardContent(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

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

            if (AutoMode && initialized)
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
            return; // Early exit - don't check image when text changed
        }

        // Only check image if text content hasn't changed (to reduce CPU load)
        Bitmap? newImage = null;
        try
        {
            newImage = await ClipboardService.GetImage();
        }
        catch (Exception e)
        {
            if (e is InvalidOperationException)
            {
                // Ignore the exception if the clipboard does not contain image
                newImage = null;
            }
            else
            {
                ErrorMessages?.Add(e.Message);
                return;
            }
        }

        // Check if the image has changed
        if (newImage != null && newImage != ClipboardImage)
        {
            ClipboardImage = newImage;
            IsImageInputVisible = true;
            IsTextInputVisible = false;
        }
        else if (newImage == null && ClipboardImage != null)
        {
            // Image was removed from clipboard
            ClipboardImage = null;
            IsImageInputVisible = false;
            IsTextInputVisible = true;
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

        if (_responseList.Count > 0 && _responseIndex >= 0 && !string.IsNullOrEmpty(Task) && !string.IsNullOrEmpty(Input))
        {
            try
            {
                GetFullTask();
                await OllamaService.StoreSqlEmbedding(Task, Input, Output);
            }
            catch (Exception e)
            {
                ErrorMessages?.Add(e.Message);
                mainWindow!.ShowNotification("ClippyAI", e.Message, false, true);
                return;
            }

            ++EmbeddingsCount;
            mainWindow!.ShowNotification("ClippyAI", "Result successfully stored as template in database!", false, false);
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
    public async Task ConfigureHotkeyDevice()
    {
        // only for Linux
        if (!OperatingSystem.IsLinux())
        {
            ErrorMessages?.Add("This feature is only supported on Linux.");
            return;
        }
        hotkeyService = new HotkeyService(mainWindow!);
        await hotkeyService.SetupHotkeyDevice();
    }

    [RelayCommand]
    public async Task CaptureAndAnalyze()
    {
        // show notification to the user
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = (MainWindow)desktop.MainWindow!;
            mainWindow!.ShowNotification("ClippyAI", Resources.Resources.AnalyzeImage, true, false);
        }
        else
        {
            throw new NullReferenceException("Missing MainWindow instance.");
        }

        try
        {
            byte[] frame;

            // Check if there is an image in the clipboard
            if (ClipboardImage != null)
            {
                using var ms = new MemoryStream();
                ClipboardImage.Save(ms);
                frame = ms.ToArray();
            }
            else
            {
                // Capture a frame from the webcam
                frame = CaptureFrame();
            }

            // Send the frame to Ollama for analysis
            var analysisResult = await OllamaService.AnalyzeImage(frame);

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
            int deviceNumber;
            if (!int.TryParse(VideoDevice, out deviceNumber))
            {
                // If the device name is not a number, try to find it by name
                var devices = new System.Collections.Generic.List<string>();
                var systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (var device in systemDeviceEnum)
                {
                    devices.Add(device.Name);
                }

                deviceNumber = Array.IndexOf(devices.ToArray(), VideoDevice);
            }
            capture = new VideoCapture(deviceNumber, VideoCapture.API.DShow);
        }
        else
        {
            // Use the default API for other platforms
            capture = new VideoCapture(VideoDevice);
        }


        capture.Set(CapProp.FrameWidth, 640);
        capture.Set(CapProp.FrameHeight, 480);

        if (!capture.IsOpened)
        {
            throw new Exception("Could not open video device");
        }

        using var frame = new Mat();
        capture.Read(frame);
        if (frame.IsEmpty)
        {
            throw new Exception("Failed to capture image");
        }

        return frame.ToImage<Bgr, byte>().ToJpegData();
    }

    private void LoadVideoDevices()
    {
        var devices = new List<string>();
        // Platform-independent way to get video devices
        if (OperatingSystem.IsWindows())
        {
            // Windows-specific code to get video devices
            var systemDeviceEnum = Array.Empty<DsDevice>();
            systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            devices.AddRange(systemDeviceEnum.Select(device => device.Name));
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // Unix-based systems code to get video devices
            for (int i = 0; i < 10; ++i)
            {
                var devicePath = $"/dev/video{i}";
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
        var cameraWindow = new CameraWindow();
        cameraWindow.Show();
    }

    [RelayCommand]
    public void CaptureScreenshot()
    {
        throw new NotImplementedException("This feature is not implemented yet.");
    }

    [RelayCommand]
    public async Task OpenConfiguration()
    {
        var dialog = new ConfigurationDialog()
        {
            DataContext = new ConfigurationDialogViewModel()
        };
        
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            await dialog.ShowDialog(desktop.MainWindow!);
        }
    }
}
