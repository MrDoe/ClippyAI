using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace ClippyAI.Views
{
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
            else
            {
                // Use DirectShow API for Windows
                // TODO: Add a way to select the camera device
                Capture = new VideoCapture(0, VideoCapture.API.DShow);
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
}
