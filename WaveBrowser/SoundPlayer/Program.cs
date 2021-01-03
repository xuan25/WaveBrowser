using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SoundPlayer
{
    class Program
    {
        [DllImport("xaudio2_9.dll", EntryPoint = "XAudio2Create")]
        static extern int XAudio2Create(IntPtr ppXAudio2, int Flags, int XAudio2Processor);

        static void Main(string[] args)
        {
            IntPtr ppXAudio2 = IntPtr.Zero;
            XAudio2Create(ppXAudio2, 0, 0);

        }
    }
}
