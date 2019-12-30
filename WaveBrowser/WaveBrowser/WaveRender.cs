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

            //Parent.Dispatcher.Invoke(new Action(() =>
            //{
            WaveformBitmap = new WriteableBitmap(width, height, 72, 72, PixelFormats.Pbgra32, null);
            //}));
        }

        public void UpdateView(double viewStart, double viewEnd)
        {
            ViewStart = viewStart;
            ViewEnd = viewEnd;
            Render(ViewStart, ViewEnd);
        }

        private void Render(double viewStart, double viewEnd)
        {
            Bitmap frameBitmap = null;

            //Parent.Dispatcher.Invoke(new Action(() =>
            //{
            WaveformBitmap.Lock();
            frameBitmap = new Bitmap(WaveformBitmap.PixelWidth, WaveformBitmap.PixelHeight, WaveformBitmap.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, WaveformBitmap.BackBuffer);
            //}));

            double channelHeight = this.Height / this.ChanelCount;
            double yScale = channelHeight / this.ChanelCount;
            double viewCount = viewEnd - viewStart;
            if (viewCount > this.Width * 4)
            {
                using (Graphics graphics = Graphics.FromImage(frameBitmap))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    for (int c = 0; c < this.ChanelCount; c++)
                    {
                        float[] chanelData = this.WaveData[c];
                        // pixel 2 pixel
                        PointF[] pointFs = new PointF[this.Width * 2];
                        double channelYOffset = c * channelHeight;
                        bool flip = false;
                        int windowSize = (int)(viewCount / this.Width);

                        for (int x = 0; x < this.Width; x++)
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

                            if (flip)
                            {
                                pointFs[x * 2] = new PointF(x, (float)((-max + 1) * yScale + channelYOffset));
                                pointFs[x * 2 + 1] = new PointF(x, (float)((-min + 1) * yScale + channelYOffset));
                            }
                            else
                            {
                                pointFs[x * 2 + 1] = new PointF(x, (float)((-max + 1) * yScale + channelYOffset));
                                pointFs[x * 2] = new PointF(x, (float)((-min + 1) * yScale + channelYOffset));
                            }
                            flip = !flip;

                        }
                        graphics.DrawLines(new System.Drawing.Pen(WaveformColor), pointFs);
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
                            points[i] = new PointF((float)(i * sampleWidth + startPosition), (float)((-chanelData[renderStart+i] + 1) * yScale + channelYOffset));
                        }
                        graphics.DrawCurve(new System.Drawing.Pen(WaveformColor), points);
                    }
                    graphics.Flush();
                }
            }

            try
            {
                //Parent.Dispatcher.Invoke(new Action(() =>
                //{
                WaveformBitmap.AddDirtyRect(new Int32Rect(0, 0, this.Width, this.Height));
                WaveformBitmap.Unlock();
                //}));
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
