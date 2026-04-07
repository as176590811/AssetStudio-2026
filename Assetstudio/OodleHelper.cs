using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AssetStudio;

public static class OodleHelper
{
    private static string dllPath;

    static OodleHelper()
    {
        ExtractEmbeddedDll();
    }

    private static void ExtractEmbeddedDll()
    {
        string tempFolder = Path.GetTempPath();
        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        dllPath = Path.Combine(tempFolder, $"{assemblyName}_oo2core_9_win64.dll");

        if (File.Exists(dllPath))
        {
            try
            {
                File.Delete(dllPath);
            }
            catch { }
        }

        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("embedded.oo2core_9_win64.dll"))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("无法从资源中提取oo2core_9_win64.dll,请确保资源已正确嵌入");
            }

            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            File.WriteAllBytes(dllPath, buffer);
        }
    }

    [DllImport("oo2core_9_win64.dll", CallingConvention = CallingConvention.StdCall)]
    static extern int Ooz_Decompress(ref byte compressedBuffer, int compressedBufferSize, ref byte decompressedBuffer, int decompressedBufferSize, int fuzzSafe, int checkCRC, int verbosity, IntPtr rawBuffer, int rawBufferSize, IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, IntPtr decoderMemorySize, int threadPhase);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibrary(string dllToLoad);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeLibrary(IntPtr hModule);

    private static IntPtr LoadEmbeddedDll()
    {
        return LoadLibrary(dllPath);
    }

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        IntPtr dllHandle = IntPtr.Zero;
        try
        {
            dllHandle = LoadEmbeddedDll();
            if (dllHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"无法加载oo2core_9_win64.dll,临时文件路径:{dllPath}");
            }

            int numWrite = -1;
            try
            {
                numWrite = Ooz_Decompress(ref compressed[0], compressed.Length, ref decompressed[0], decompressed.Length, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3);
            }
            catch (Exception e)
            {
                throw new IOException($"Oodle解压缩失败,实际写入{numWrite}字节,但预期{decompressed.Length}字节", e);
            }

            return numWrite;
        }
        finally
        {
            if (dllHandle != IntPtr.Zero)
            {
                FreeLibrary(dllHandle);
            }
        }
    }

    public static void CleanupTempDll()
    {
        if (!string.IsNullOrEmpty(dllPath) && File.Exists(dllPath))
        {
            try
            {
                File.Delete(dllPath);
            }
            catch { }
        }
    }
}