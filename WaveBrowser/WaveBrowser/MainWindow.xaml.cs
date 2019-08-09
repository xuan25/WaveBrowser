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
        double Start, Count;
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
            if (Keyboard.IsKeyDown(Key.LeftShift))
            {
                double offset;
                if (e.Delta > 0)
                    offset = -(Count * 0.1);
                else
                    offset = (Count * 0.1);

                Start += offset;

                if (Start < 0)
                    Start = 0;
                else if (Start + Count > Samples[0].Length - 1)
                    Start = Samples[0].Length - Count - 1;

                double frameSize = Count / (WaveImage.RenderSize.Width + 1);
                double imageOffset = offset / frameSize;
                Bitmap bitmap = new Bitmap(WaveBitmap.Width, WaveBitmap.Height);
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(WaveBitmap, new System.Drawing.Rectangle(-(int)imageOffset, 0, WaveBitmap.Width, WaveBitmap.Height));
                WaveBitmap = bitmap;
                WaveImage.Source = BitmapToBitmapImage(bitmap);
            }
            else
            {
                System.Windows.Point point = e.GetPosition(WaveImage);
                double frameSizeBefore = Count / (WaveImage.RenderSize.Width + 1);
                double fixedX = point.X;

                double scale;
                if (e.Delta > 0)
                    scale = 1.5;
                else
                    scale = 1 / 1.5;

                Count /= scale;

                if (!(Count < 4))
                {
                    double frameSizeAfter = Count / (WaveImage.RenderSize.Width + 1);
                    double SampleOffset = (frameSizeBefore - frameSizeAfter) * fixedX;
                    Start += SampleOffset;
                }  

                if (Count > Samples[0].Length - 1)
                    Count = Samples[0].Length - 1;
                else if (Count < 4)
                    Count = 4;
                if (Start < 0)
                    Start = 0;
                else if (Start + Count > Samples[0].Length - 1)
                    Start = Samples[0].Length - Count - 1;

                double imageOffset = fixedX * (1 - scale);
                Bitmap bitmap = new Bitmap(WaveBitmap.Width, WaveBitmap.Height);
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.DrawImage(WaveBitmap, new System.Drawing.Rectangle((int)imageOffset, 0, (int)(WaveBitmap.Width * scale), WaveBitmap.Height));
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
        private Task RenderWaveformAsync(float[][] channels, System.Drawing.Color color, double start, double count)
        {
            // Cancellation
            if (RenderCancellationTokenSource != null)
                RenderCancellationTokenSource.Cancel();
            RenderCancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = RenderCancellationTokenSource.Token;

            // Calc propertise
            double width = WaveImage.RenderSize.Width;
            double height = WaveImage.RenderSize.Height;
            double channelHeight = height / channels.Length;
            double yScale = channelHeight / channels.Length;

            // Render task
            Task task = new Task(new Action(() =>
            {
                // Create Bitmap
                Bitmap bitmap = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                if(count > width * 4)
                {
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
                        for (int x = 0; x < width; x++)
                        {
                            double frameSize = count / (width + 1);
                            int startIndex = (int)(start + frameSize * x);

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
                            for (int y = startY; y < endY; y++)
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
                }
                else
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                    {
                        for (int c = 0; c < channels.Length; c++)
                        {
                            float channelYOffset = c * (float)channelHeight;
                            PointF[] points = new PointF[(int)Math.Ceiling(count + 1)];
                            double sampleInterval = width / (count - 1);
                            double offset = (start - Math.Ceiling(start)) * sampleInterval;
                            for (int i = 0; i < count + 1; i++)
                            {
                                points[i] = new PointF((float)(i * sampleInterval + offset), (float)((-channels[c][(int)Math.Floor(start + i)] + 1) * yScale + channelYOffset));
                            }
                            graphics.DrawCurve(new System.Drawing.Pen(color), points);
                        }
                    }
                }

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
