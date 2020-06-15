using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WaveBrowser
{
    class WaveRender
    {
        System.Drawing.Color WaveformColor = System.Drawing.Color.FromArgb(0xCC, 0x00, 0x80, 0xFF);
        public WriteableBitmap WaveformBitmap;
        public int Width, Height;
        public int ChanelCount, ChanelLength;
        public float[][] WaveData;
        public Window Parent;
        public double ViewStart, ViewEnd;

        public WaveRender(Window parent, int width, int height, float[][] waveData)
        {
            Parent = parent;
            Width = width;
            Height = height;

            WaveData = waveData;
            ChanelCount = waveData.Length;
            ChanelLength = waveData[0].Length;

            ViewStart = 0;
            ViewEnd = ChanelLength - 1;

            WaveformBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
        }

        public void UpdateView(double viewStart, double viewEnd)
        {
            ViewStart = viewStart;
            ViewEnd = viewEnd;
            Render(ViewStart, ViewEnd);
        }

        private void Render(double viewStart, double viewEnd)
        {
            WaveformBitmap.Lock();
            Bitmap frameBitmap = new Bitmap(WaveformBitmap.PixelWidth, WaveformBitmap.PixelHeight, WaveformBitmap.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, WaveformBitmap.BackBuffer);

            System.Drawing.Pen pen = new System.Drawing.Pen(WaveformColor);
            double channelHeight = this.Height / this.ChanelCount;
            double yScale = channelHeight / this.ChanelCount;
            double viewCount = viewEnd - viewStart;
            if (viewCount > this.Width * 6)
            {
                using (Graphics graphics = Graphics.FromImage(frameBitmap))
                {
                    graphics.Clear(System.Drawing.Color.White);

                    List<Task> tasks = new List<Task>();
                    int taskNum = 16;
                    for (int t = 0; t < taskNum; t++)
                    {
                        int ti = t;
                        Task task = Task.Factory.StartNew(() =>
                        {
                            for (int c = 0; c < this.ChanelCount; c++)
                            {
                                float[] chanelData = this.WaveData[c];
                                double channelYOffset = c * channelHeight;
                                int windowSize = (int)(viewCount / this.Width);

                                for (int x = this.Width * ti / taskNum; x < this.Width * (ti + 1) / taskNum; x++)
                                {
                                    int windowStart = (int)(viewStart + windowSize * x);

                                    float max = -1;
                                    float min = 1;
                                    for (int i = 0; i < windowSize; i++)
                                    {
                                        float sample = chanelData[windowStart + i];
                                        if (sample > max)
                                            max = sample;
                                        if (sample < min)
                                            min = sample;
                                    }
                                    lock (graphics)
                                    {
                                        graphics.DrawLine(pen, x, (float)((-max + 1) * yScale + channelYOffset), x, (float)((-min + 1) * yScale + channelYOffset));
                                    }
                                }
                            }
                        });
                        tasks.Add(task);
                    }
                    for (int t = 0; t < taskNum; t++)
                    {
                        tasks[t].Wait();
                    }
                    graphics.Flush();
                }

            }
            else
            {
                using (Graphics graphics = Graphics.FromImage(frameBitmap))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    for (int c = 0; c < this.ChanelCount; c++)
                    {
                        float[] chanelData = this.WaveData[c];
                        float channelYOffset = c * (float)channelHeight;

                        int renderStart = (int)this.ViewStart;
                        int renderCount = (int)viewCount + 1;
                        PointF[] points = new PointF[renderCount];

                        double sampleWidth = this.Width / (viewCount - 1);
                        double startPosition = (renderStart - this.ViewStart) * sampleWidth;
                        for (int i = 0; i < renderCount; i++)
                        {
                            points[i] = new PointF((float)(i * sampleWidth + startPosition), (float)((-chanelData[renderStart + i] + 1) * yScale + channelYOffset));
                        }
                        graphics.DrawCurve(pen, points);
                    }
                    graphics.Flush();
                }
            }

            try
            {
                WaveformBitmap.AddDirtyRect(new Int32Rect(0, 0, this.Width, this.Height));
                WaveformBitmap.Unlock();
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
