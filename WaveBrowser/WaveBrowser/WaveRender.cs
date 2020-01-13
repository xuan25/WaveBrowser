using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WaveBrowser
{
    class WaveRender
    {
        SolidColorBrush WaveformBrush = new SolidColorBrush(new Color() { A = 0xCC, R = 0x00, G = 0x80, B = 0xFF });
        public Canvas canvas;
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
        }

        public void UpdateView(double viewStart, double viewEnd)
        {
            ViewStart = viewStart;
            ViewEnd = viewEnd;
            Render(ViewStart, ViewEnd);
        }

        private void Render(double viewStart, double viewEnd)
        {
            double channelHeight = this.Height / this.ChanelCount;
            double yScale = channelHeight / this.ChanelCount;
            double viewCount = viewEnd - viewStart;
            if (viewCount > this.Width * 4)
            {
                canvas.Children.Clear();
                for (int c = 0; c < this.ChanelCount; c++)
                {
                    float[] chanelData = this.WaveData[c];
                    // pixel 2 pixel

                    Polyline wave = new Polyline();
                    wave.Stroke = WaveformBrush;
                    wave.StrokeThickness = 1;
                    wave.FillRule = FillRule.EvenOdd;
                    PointCollection points = new PointCollection();

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
                            points.Add(new Point(x, (float)((-max + 1) * yScale + channelYOffset)));
                            points.Add(new Point(x, (float)((-min + 1) * yScale + channelYOffset)));
                        }
                        else
                        {
                            points.Add(new Point(x, (float)((-min + 1) * yScale + channelYOffset)));
                            points.Add(new Point(x, (float)((-max + 1) * yScale + channelYOffset)));
                        }
                        flip = !flip;

                    }
                    wave.Points = points;
                    canvas.Children.Add(wave);
                }

            }
            else
            {
                canvas.Children.Clear();
                for (int c = 0; c < this.ChanelCount; c++)
                {
                    float[] chanelData = this.WaveData[c];
                    float channelYOffset = c * (float)channelHeight;

                    int renderStart = (int)this.ViewStart;
                    int renderCount = (int)viewCount + 1;

                    Polyline wave = new Polyline();
                    wave.Stroke = WaveformBrush;
                    wave.StrokeThickness = 1;
                    wave.FillRule = FillRule.EvenOdd;
                    PointCollection points = new PointCollection();

                    double sampleWidth = this.Width / (viewCount - 1);
                    double startPosition = (renderStart - this.ViewStart) * sampleWidth;
                    for (int i = 0; i < renderCount; i++)
                    {
                        points.Add(new Point((float)(i * sampleWidth + startPosition), (float)((-chanelData[renderStart + i] + 1) * yScale + channelYOffset)));
                    }
                    wave.Points = points;
                    canvas.Children.Add(wave);
                }
            }
        }
    }
}
