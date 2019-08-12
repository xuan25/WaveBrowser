using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WaveBrowser
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        float[][] Chanels;
        double WaveformWidth, WaveformHeight, Start, Count;
        WriteableBitmap WaveformWriteableBitmap;
        Thread RenderThread;
        System.Drawing.Color WaveformColor = System.Drawing.Color.FromArgb(0xCC, 0x00, 0x80, 0xFF);

        public MainWindow()
        {
            InitializeComponent();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            WaveformWriteableBitmap = new WriteableBitmap(1, 1, 72, 72, PixelFormats.Pbgra32, null);
            WaveImage.Source = WaveformWriteableBitmap;

            Loaded += MainWindow_Loaded;
            WaveBorder.SizeChanged += WaveBorder_SizeChanged;
            WaveBorder.MouseWheel += WaveBorder_MouseWheel;
            WaveBorder.ManipulationDelta += WaveBorder_ManipulationDelta;
        }

        private void Render()
        {
            double widthLast = 0;
            double heightLast = 0;
            double startLast = 0;
            double countLast = 0;
            double width = 0;
            double height = 0;
            double start = 0;
            double count = 0;
            Bitmap bitmap = null;
            while (true)
            {
                if(Chanels != null && WaveformWriteableBitmap != null)
                {
                    // Calc propertise
                    try
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            width = WaveImage.RenderSize.Width;
                            height = WaveImage.RenderSize.Height;
                            start = Start;
                            count = Count;
                        }));
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    if (width != widthLast || height != heightLast || start != startLast || count != countLast)
                    {
                        widthLast = width;
                        heightLast = height;
                        startLast = start;
                        countLast = count;

                        try
                        {
                            Dispatcher.Invoke(new Action(() =>
                            {
                                WaveformWriteableBitmap.Lock();
                                bitmap = new Bitmap(WaveformWriteableBitmap.PixelWidth, WaveformWriteableBitmap.PixelHeight, WaveformWriteableBitmap.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, WaveformWriteableBitmap.BackBuffer);
                            }));
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }

                        double channelHeight = height / Chanels.Length;
                        double yScale = channelHeight / Chanels.Length;
                        if (Count > width * 4)
                        {
                            using (Graphics graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(System.Drawing.Color.White);
                                for (int c = 0; c < Chanels.Length; c++)
                                {
                                    PointF[] pointFs = new PointF[(int)Math.Ceiling(width) * 2];
                                    double channelYOffset = c * channelHeight;
                                    bool flag = false;
                                    for (int x = 0; x < width; x++)
                                    {
                                        double frameSize = Count / (width + 1);
                                        int startIndex = (int)(Start + frameSize * x);

                                        float max = -1;
                                        float min = 1;
                                        for (int i = 0; i < Math.Ceiling(frameSize); i++)
                                        {
                                            float sample = Chanels[c][startIndex + i];
                                            if (sample > max)
                                                max = sample;
                                            if (sample < min)
                                                min = sample;
                                        }

                                        if (flag)
                                        {
                                            pointFs[x * 2] = new PointF(x, (float)((-max + 1) * yScale + channelYOffset));
                                            pointFs[x * 2 + 1] = new PointF(x, (float)((-min + 1) * yScale + channelYOffset));
                                        }
                                        else
                                        {
                                            pointFs[x * 2 + 1] = new PointF(x, (float)((-max + 1) * yScale + channelYOffset));
                                            pointFs[x * 2] = new PointF(x, (float)((-min + 1) * yScale + channelYOffset));
                                        }

                                    }
                                    graphics.DrawLines(new System.Drawing.Pen(WaveformColor), pointFs);
                                }
                                graphics.Flush();
                            }

                        }
                        else
                        {
                            using (Graphics graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(System.Drawing.Color.White);
                                for (int c = 0; c < Chanels.Length; c++)
                                {
                                    float channelYOffset = c * (float)channelHeight;
                                    PointF[] points = new PointF[(int)Math.Ceiling(Count + 1)];
                                    double sampleInterval = width / (Count - 1);
                                    double offset = (Start - Math.Floor(Start)) * sampleInterval;
                                    for (int i = 0; i < Count + 1; i++)
                                    {
                                        points[i] = new PointF((float)(i * sampleInterval - offset), (float)((-Chanels[c][(int)Math.Floor(Start + i)] + 1) * yScale + channelYOffset));
                                    }
                                    graphics.DrawCurve(new System.Drawing.Pen(WaveformColor), points);
                                }
                                graphics.Flush();
                            }
                        }

                        try
                        {
                            Dispatcher.Invoke(new Action(() =>
                            {
                                WaveformWriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, WaveformWriteableBitmap.PixelWidth, WaveformWriteableBitmap.PixelHeight));
                                WaveformWriteableBitmap.Unlock();
                            }));
                        }
                        catch (TaskCanceledException)
                        {
                            return;
                        }
                    }
                }
            }

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Chanels = LoadChannels("test.mp3");
            Start = 0;
            Count = Chanels[0].Length - 1;
        }

        private void WaveBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(RenderThread != null)
                RenderThread.Abort();
            
            WaveformWidth = e.NewSize.Width;
            WaveformHeight = e.NewSize.Height;
            WaveformWriteableBitmap = new WriteableBitmap((int)WaveformWidth, (int)WaveformHeight, 72, 72, PixelFormats.Pbgra32, null);
            WaveImage.Source = WaveformWriteableBitmap;
            RenderThread = new Thread(Render);
            RenderThread.Start();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            var source = PresentationSource.FromVisual(WaveBorder);
            ((HwndSource)source)?.AddHook(Hook);
        }

        const int WM_MOUSEHWHEEL = 0x020E;
        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_MOUSEHWHEEL:
                    int tilt = (short)HIWORD(wParam);
                    OnMouseTilt(tilt);
                    return (IntPtr)1;
            }
            return IntPtr.Zero;
        }

        private static int HIWORD(IntPtr ptr)
        {
            var val32 = ptr.ToInt32();
            return ((val32 >> 16) & 0xFFFF);
        }

        private static int LOWORD(IntPtr ptr)
        {
            var val32 = ptr.ToInt32();
            return (val32 & 0xFFFF);
        }

        private void OnMouseTilt(int tilt)
        {
            Scroll(tilt);
        }

        private void WaveBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Chanels == null)
                return;

            if (Keyboard.IsKeyDown(Key.LeftShift))
                Scroll(-e.Delta);
            else
                if (e.Delta > 0)
                    Resize(e.GetPosition(WaveImage), 1 + e.Delta * 0.005);
                else
                    Resize(e.GetPosition(WaveImage), 1 / (1 + -e.Delta * 0.005));
        }

        private void WaveBorder_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (Chanels == null || e.DeltaManipulation.Scale.X == 1)
                return;

            Scroll(-e.DeltaManipulation.Translation.X);
            Resize(e.ManipulationOrigin, e.DeltaManipulation.Scale.X);
        }

        private void Resize(System.Windows.Point certer, double scale)
        {
            double start, count;
            double frameSizeBefore = Count / (WaveImage.RenderSize.Width + 1);
            double fixedX = certer.X;

            count = Count / scale;

            double frameSizeAfter = count / (WaveImage.RenderSize.Width + 1);
            double SampleOffset = (frameSizeBefore - frameSizeAfter) * fixedX;
            start = Start + SampleOffset;

            if (count > Chanels[0].Length - 1)
                count = Chanels[0].Length - 1;
            else if (count < 4)
                count = 4;

            if (start < 0)
                start = 0;
            else if (start + count > Chanels[0].Length - 1)
                start = Chanels[0].Length - count - 1;

            Start = start;
            Count = count;
        }

        private void Scroll(double offset)
        {
            double start;

            double frameSize = Count / (WaveImage.RenderSize.Width + 1);
            start = Start + offset * frameSize;

            if (start < 0)
                start = 0;
            else if (start + Count > Chanels[0].Length - 1)
                start = Chanels[0].Length - Count - 1;

            Start = start;
        }

        private float[][] LoadChannels(string filename)
        {
            ISampleProvider sampleProvider = new AudioFileReader(filename);

            List<float>[] channels = new List<float>[sampleProvider.WaveFormat.Channels];

            for (int i = 0; i < channels.Length; i++)
                channels[i] = new List<float>();

            float[] buffer = new float[channels.Length];
            while (sampleProvider.Read(buffer, 0, buffer.Length) == buffer.Length)
                for (int i = 0; i < channels.Length; i++)
                    channels[i].Add(buffer[i]);

            float[][] result = new float[channels.Length][];

            for (int i = 0; i < channels.Length; i++)
                result[i] = channels[i].ToArray();

            return result;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if(RenderThread != null)
                        RenderThread.Abort();
                    RenderThread = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                Chanels = null;

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MainWindow()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
