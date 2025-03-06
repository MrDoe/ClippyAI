using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Threading.Tasks;

namespace ClippyAI.Views
{
    public partial class CameraWindow : Window
    {
        private VideoCapture _capture;
        private bool _isCapturing;

        public CameraWindow()
        {
            InitializeComponent();
            _capture = new VideoCapture(0, VideoCapture.API.V4L2);
            _capture.Set(CapProp.FrameWidth, 640);
            _capture.Set(CapProp.FrameHeight, 480);
            _isCapturing = true;
            Task.Run(CaptureLoop);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task CaptureLoop()
        {
            while (_isCapturing)
            {
                using var frame = new Mat();
                _capture.Read(frame);
                if (!frame.IsEmpty)
                {
                    Emgu.CV.Image<Bgr, byte> image = frame.ToImage<Bgr, byte>();
                    
                    // convert Emgu.CV.Image to Avalonia.Media.Imaging.Bitmap
                    // there is no .ToBitmap() or similar method in Emgu.CV
                    // so we have to convert it by other means
                    
                    byte[] jpgImage = image.ToJpegData();
                    using var ms = new System.IO.MemoryStream(jpgImage);
                    var bitmap = new Avalonia.Media.Imaging.Bitmap(ms);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var imgCamera = this.FindControl<Image>("imgCamera");
                        if(imgCamera != null)
                        {
                            imgCamera.Source = bitmap;
                        }
                    });
                }
                await Task.Delay(30);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isCapturing = false;
            _capture.Dispose();
            base.OnClosed(e);
        }
    }
}
