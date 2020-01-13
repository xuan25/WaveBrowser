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
        double Start, End;
        PerformanceTimer performanceTimer = new PerformanceTimer();

        public MainWindow()
        {
            InitializeComponent();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            performanceTimer.Start();
        }


        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            performanceTimer.Stop();
            //Console.WriteLine("Loading: {0} s", performanceTimer.Duration);
            FpsBox.Text = string.Format("{0} Fps", 1 / performanceTimer.Duration);
            performanceTimer.Start();
        }

        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            Chanels = LoadChannels("test.mp3");
            Start = 0;
            End = Chanels[0].Length - 1;

            WaveBorder.SizeChanged -= WaveBorder_SizeChanged;
            WaveBorder.MouseWheel -= WaveBorder_MouseWheel;
            WaveBorder.ManipulationDelta -= WaveBorder_ManipulationDelta;

            WaveBorder.SizeChanged += WaveBorder_SizeChanged;
            WaveBorder.MouseWheel += WaveBorder_MouseWheel;
            WaveBorder.ManipulationDelta += WaveBorder_ManipulationDelta;

            UpdateRender((int)WaveBorder.ActualWidth, (int)WaveBorder.ActualHeight);
        }

        WaveRender waveRender;
        private void WaveBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRender((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        private void UpdateRender(int width, int height)
        {
            waveRender = new WaveRender(this, width, height, Chanels);
            waveRender.canvas = WaveCanvas;
            waveRender.UpdateView(Start, End);
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
                    Resize(e.GetPosition(WaveCanvas), 1 + e.Delta * 0.005);
                else
                    Resize(e.GetPosition(WaveCanvas), 1 / (1 + -e.Delta * 0.005));
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
            double oriCount = End - Start;
            double start, count;
            double frameSizeBefore = oriCount / (WaveCanvas.RenderSize.Width + 1);
            double fixedX = certer.X;

            count = oriCount / scale;

            double frameSizeAfter = count / (WaveCanvas.RenderSize.Width + 1);
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
            End = start + count;

            waveRender.UpdateView(Start, End);
        }

        private void Scroll(double offset)
        {
            double count = End - Start;
            double start;

            double frameSize = count / (WaveCanvas.RenderSize.Width);
            start = Start + offset * frameSize;

            if (start < 0)
                start = 0;
            else if (start + count > Chanels[0].Length - 1)
                start = Chanels[0].Length - count - 1;

            Start = start;
            End = start + count;

            waveRender.UpdateView(Start, End);
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
                    //if(RenderThread != null)
                    //    RenderThread.Abort();
                    //RenderThread = null;
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
