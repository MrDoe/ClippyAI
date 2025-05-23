using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Configuration;
using System.Threading.Tasks;
using DirectShowLib;
namespace ClippyAI.Views;

public partial class CameraWindow : Window
{
    private VideoCapture Capture;
    private string VideoDevice = ConfigurationManager.AppSettings["VideoDevice"] ?? "";
    private bool IsCapturing;

    public CameraWindow()
    {
        InitializeComponent();

        if (OperatingSystem.IsLinux())
        {
            // Use V4L2 API for Linux
            Capture = new VideoCapture(VideoDevice, VideoCapture.API.V4L2);
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
            Capture = new VideoCapture(deviceNumber, VideoCapture.API.DShow);
        }
        else
        {
            // Use the default API for other platforms
            Capture = new VideoCapture(VideoDevice);
        }

        Capture.Set(CapProp.FrameWidth, 640);
        Capture.Set(CapProp.FrameHeight, 480);
        IsCapturing = true;
        Task.Run(CaptureLoop);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async Task CaptureLoop()
    {
        while (IsCapturing)
        {
            try
            {
                using var frame = new Mat();
                Capture.Read(frame);
                if (!frame.IsEmpty)
                {
                    // Process image data in background thread
                    Emgu.CV.Image<Bgr, byte> image = frame.ToImage<Bgr, byte>();
                    byte[] jpgImage = image.ToJpegData();

                    // Move all UI operations including bitmap creation to the UI thread
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            using var ms = new System.IO.MemoryStream(jpgImage);
                            var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);

                            var imgCamera = this.FindControl<Image>("imgCamera");
                            if (imgCamera != null)
                            {
                                imgCamera.Source = bitmap;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"UI update error: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Capture error: {ex.Message}");
            }

            await Task.Delay(30);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        IsCapturing = false;
        Capture.Dispose();
        base.OnClosed(e);
    }
}

