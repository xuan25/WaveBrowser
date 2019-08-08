using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WaveBrowser
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        float[][] Samples;
        int Start, Count;
        Bitmap WaveBitmap;

        public MainWindow()
        {
            InitializeComponent();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            WaveBitmap = new Bitmap(1, 1);
            WaveImage.Source = BitmapToBitmapImage(WaveBitmap);

            Loaded += MainWindow_Loaded;
            WaveImage.SizeChanged += WaveImage_SizeChanged;
            WaveBorder.MouseWheel += WaveBorder_MouseWheel;
        }

        private void WaveBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Samples == null)
                return;
            if (e.Delta > 0)
            {
                Count = (int)(Count / 1.1);
                Bitmap bitmap = new Bitmap(WaveBitmap.Width, WaveBitmap.Height);
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.DrawImage(WaveBitmap, new System.Drawing.Rectangle(0, 0, WaveBitmap.Width, WaveBitmap.Height), new System.Drawing.Rectangle(0, 0, (int)(WaveBitmap.Width/1.1), WaveBitmap.Height), GraphicsUnit.Pixel);
                WaveBitmap = bitmap;
                WaveImage.Source = BitmapToBitmapImage(bitmap);
            }
            else
            {
                Count = (int)(Count * 1.1);
                if (Count > Samples[0].Length)
                    Count = Samples[0].Length;
                if (Start + Count > Samples[0].Length)
                    Start = Samples[0].Length - Count;
                Bitmap bitmap = new Bitmap(WaveBitmap.Width, WaveBitmap.Height);
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.DrawImage(WaveBitmap, new System.Drawing.Rectangle(0, 0, (int)(WaveBitmap.Width / 1.1), WaveBitmap.Height), new System.Drawing.Rectangle(0, 0, WaveBitmap.Width, WaveBitmap.Height), GraphicsUnit.Pixel);
                WaveBitmap = bitmap;
                WaveImage.Source = BitmapToBitmapImage(bitmap);

            }
            RenderWaveformAsync(Samples, System.Drawing.Color.FromArgb(0xCC, 0x00, 0x80, 0xFF), Start, Count);
        }

        private void WaveImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Samples != null)
                RenderWaveformAsync(Samples, System.Drawing.Color.FromArgb(0xCC, 0x00, 0x80, 0xFF), Start, Count);
        }

        CancellationTokenSource RenderCancellationTokenSource;
        private Task RenderWaveformAsync(float[][] channels, System.Drawing.Color color, int start, int count)
        {
            // Cancellation
            if (RenderCancellationTokenSource != null)
                RenderCancellationTokenSource.Cancel();
            RenderCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = RenderCancellationTokenSource.Token;

            // Calc propertise
            double width = WaveImage.RenderSize.Width;
            double height = WaveImage.RenderSize.Height;
            int samplesCount = count;
            double channelHeight = WaveImage.RenderSize.Height / channels.Length;
            double channelWidth = WaveImage.RenderSize.Width;
            float yScale = (float)channelHeight / 2;

            // Render task
            Task task = new Task(new Action(() =>
            {
                // Create Bitmap
                Bitmap bitmap = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                // Lock data
                System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
                // Bytes ptr
                IntPtr bitmapDataIntPtr = bitmapData.Scan0;
                // Copy to array
                int pixelLength = 4;
                int bytesLength = Math.Abs(bitmapData.Stride) * bitmap.Height;
                byte[] bytes = new byte[bytesLength];
                System.Runtime.InteropServices.Marshal.Copy(bitmapDataIntPtr, bytes, 0, bytesLength);
                // Render
                for (int c = 0; c < channels.Length; c++)
                {
                    float channelYOffset = c * (float)channelHeight;
                    for (int x = 0; x < channelWidth; x++)
                    {
                        double frameSize = samplesCount / (channelWidth + 1);
                        int startIndex = start + (int)(frameSize * x);

                        float max = -1;
                        float min = 1;
                        for (int i = 0; i < frameSize; i++)
                        {
                            float sample = channels[c][startIndex + i];
                            if (sample > max)
                                max = sample;
                            if (sample < min)
                                min = sample;
                        }

                        int startY = (int)((-max + 1) * yScale + channelYOffset);
                        int endY = (int)((-min + 1) * yScale + channelYOffset);
                        for(int y = startY; y < endY; y++)
                        {
                            int pixelStartIndex = (y * Math.Abs(bitmapData.Stride)) + x * pixelLength;
                            bytes[pixelStartIndex] = color.B;
                            bytes[pixelStartIndex + 1] = color.G;
                            bytes[pixelStartIndex + 2] = color.R;
                            bytes[pixelStartIndex + 3] = color.A;
                            if (cancellationToken.IsCancellationRequested)
                            {
                                bitmap.UnlockBits(bitmapData);
                                return;
                            }
                        }
                    }
                }
                // Copy to bitmap data & Unlock data
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmapDataIntPtr, bytesLength);
                bitmap.UnlockBits(bitmapData);

                // Convert
                BitmapImage bitmapImage = BitmapToBitmapImage(bitmap);
                // Display
                Dispatcher.Invoke(new Action(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    WaveImage.Source = bitmapImage;
                    WaveBitmap = bitmap;
                }));
            }));
            task.Start();

            return task;
        }

        private BitmapImage BitmapToBitmapImage(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Samples = LoadChannels("test.mp3");
            Start = 0;
            Count = Samples[0].Length;
            RenderWaveformAsync(Samples, System.Drawing.Color.FromArgb(0xCC, 0x00, 0x80, 0xFF), Start, Count);
        }
    }
}
