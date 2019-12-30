using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WaveBrowser
{
    class PerformanceTimer
    {
        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(
          out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(
          out long lpFrequency);

        private long startTime, stopTime;
        private long freq;

        public PerformanceTimer()
        {
            startTime = 0;
            stopTime = 0;

            if (QueryPerformanceFrequency(out freq) == false)
            {
                throw new Exception("Timer not supported.");
            }
        }

        public void Start()
        {
            Thread.Sleep(0);
            QueryPerformanceCounter(out startTime);
        }

        public void Stop()
        {
            QueryPerformanceCounter(out stopTime);
        }

        public double Duration
        {
            get
            {
                return (double)(stopTime - startTime) / (double)freq;
            }
        }
    }
}
