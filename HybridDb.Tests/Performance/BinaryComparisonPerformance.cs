using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace HybridDb.Tests.Performance
{
    public class BinaryComparisonPerformance
    {
        [Fact]
        public void CompareAllBytes()
        {
            var a = File.ReadAllBytes("Performance\\large.json");
            var b = new byte[a.Length];
            Array.Copy(a, b, a.Length);

            b[b.Length/2] = 0;

            Console.WriteLine(a.Length);

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < a.Length-8000; i+=8000)
            {
                CompareBuffers(a, i, b, i, 8000);
            }
            Console.WriteLine(watch.ElapsedMilliseconds);
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int memcmp(byte[] b1, byte[] b2, long count);
        static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }

        [DllImport("msvcrt.dll")]
        unsafe static extern int memcmp(byte* b1, byte* b2, int count);

        unsafe static int CompareBuffers(byte[] buffer1, int offset1, byte[] buffer2, int offset2, int count)
        {
            fixed (byte* b1 = buffer1, b2 = buffer2)
            {
                return memcmp(b1 + offset1, b2 + offset2, count);
            }
        }
    }

    public unsafe static class Hallo
    {
    }
}