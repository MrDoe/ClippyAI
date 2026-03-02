using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClippyAI.Services;
using DirectShowLib;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
namespace ClippyAI.Views;

public partial class CameraWindow : Window
{
    private readonly VideoCapture Capture;
    private readonly string VideoDevice = ConfigurationService.GetConfigurationValue("VideoDevice");
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
            if (!int.TryParse(VideoDevice, out int deviceNumber))
            {
                // If the device name is not a number, try to find it by name
                List<string> devices = [];
                DsDevice[] systemDeviceEnum = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (DsDevice? device in systemDeviceEnum)
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

        _ = Capture.Set(CapProp.FrameWidth, 640);
        _ = Capture.Set(CapProp.FrameHeight, 480);
        IsCapturing = true;
        _ = Task.Run(CaptureLoop);
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
                using Mat frame = new();
                _ = Capture.Read(frame);
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
                            using MemoryStream ms = new(jpgImage);
                            Bitmap bitmap = new(ms);

                            Image? imgCamera = this.FindControl<Image>("imgCamera");
                            _ = imgCamera?.Source = bitmap;
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

