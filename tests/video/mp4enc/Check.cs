using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// Reads an MP4 back with Media Foundation's own SourceReader and says
// what is actually in it - so "the encoder exited 0" is never mistaken
// for "the video plays".
//
//   mp4enc check <file.mp4> <out-prefix> <seconds> [seconds...]
//
// Decodes EVERY video frame (a frame that will not decode is a frame a
// player will choke on), counts them, confirms an audio stream exists and
// what it is, and saves the frame nearest each requested time as a BMP -
// which is what catches an upside-down or frozen picture, because it is
// a real decoded frame and not what the encoder was handed.
internal static class Check
{
    static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
    static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    static readonly Guid MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING = new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");

    static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
    static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
    static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00AA00389B71");

    const int FirstVideo = unchecked((int)0xfffffffc);
    const int FirstAudio = unchecked((int)0xfffffffd);
    const int EndOfStream = 0x2;

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateSourceReaderFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string url,
        IMFAttributes attributes,
        out IMFSourceReader reader);

    public static int Run(string[] args)
    {
        string file = Path.GetFullPath(args[1]);
        string prefix = args[2];

        var wanted = new List<double>();

        for (int i = 3; i < args.Length; i++)
            wanted.Add(double.Parse(args[i],
                System.Globalization.CultureInfo.InvariantCulture));

        MFStartup(0x00020070, 0);

        try
        {
            return Inspect(file, prefix, wanted);
        }
        finally
        {
            MFShutdown();
        }
    }

    private static int Inspect(string file, string prefix, List<double> wanted)
    {
        IMFAttributes attributes;

        MFCreateAttributes(out attributes, 1);

        // Lets the reader turn H.264 into RGB32 for us
        attributes.SetUINT32(MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, 1);

        IMFSourceReader reader;

        MFCreateSourceReaderFromURL(file, attributes, out reader);

        // ---- the audio stream: is there one, and what is it ----
        bool audioOk = false;

        try
        {
            IMFMediaType native;

            reader.GetNativeMediaType(FirstAudio, 0, out native);

            Guid subtype;
            int rate;
            int channels;

            native.GetGUID(MF_MT_SUBTYPE, out subtype);
            native.GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, out rate);
            native.GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, out channels);

            audioOk = subtype == MFAudioFormat_AAC;

            Console.WriteLine("audio: " +
                (subtype == MFAudioFormat_AAC ? "AAC" : subtype.ToString()) +
                ", " + rate + " Hz, " + channels + " channel(s)");
        }
        catch (COMException ex)
        {
            Console.WriteLine("audio: NONE (" + ex.Message.Trim() + ")");
        }

        // ---- the video stream, decoded to RGB32 ----
        IMFMediaType wantType;

        MFCreateMediaType(out wantType);
        wantType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        wantType.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);

        reader.SetCurrentMediaType(FirstVideo, IntPtr.Zero, wantType);

        IMFMediaType current;

        reader.GetCurrentMediaType(FirstVideo, out current);

        long size;

        current.GetUINT64(MF_MT_FRAME_SIZE, out size);

        int width = (int)(size >> 32);
        int height = (int)(size & 0xffffffff);

        int stride = width * 4;

        try
        {
            int s;

            current.GetUINT32(MF_MT_DEFAULT_STRIDE, out s);
            stride = s;
        }
        catch (COMException)
        {
        }

        Console.WriteLine("video: " + width + "x" + height +
                          ", stride " + stride +
                          (stride < 0 ? " (bottom-up)" : " (top-down)"));

        // Only video from here on
        reader.SetStreamSelection(FirstAudio, false);

        int frames = 0;
        long last = 0;

        var saved = new bool[wanted.Count];

        while (true)
        {
            int actual;
            int flags;
            long time;
            IMFSample sample;

            reader.ReadSample(FirstVideo, 0, out actual, out flags,
                out time, out sample);

            if ((flags & EndOfStream) != 0)
                break;

            if (sample == null)
                continue;

            frames++;
            last = time;

            for (int w = 0; w < wanted.Count; w++)
            {
                if (saved[w] || time < wanted[w] * 10_000_000L)
                    continue;

                saved[w] = true;

                IMFMediaBuffer buffer;

                sample.ConvertToContiguousBuffer(out buffer);

                IntPtr pointer;
                int max;
                int length;

                buffer.Lock(out pointer, out max, out length);

                try
                {
                    var pixels = new byte[length];

                    Marshal.Copy(pointer, pixels, 0, length);

                    string bmp = prefix + "_" +
                        wanted[w].ToString("0.0",
                            System.Globalization.CultureInfo.InvariantCulture) +
                        "s.bmp";

                    Bmp(bmp, pixels, width, height, stride);

                    Console.WriteLine("frame at " + (time / 1e7).ToString("0.00") +
                                      " s -> " + Path.GetFileName(bmp));
                }
                finally
                {
                    buffer.Unlock();
                }

                Marshal.ReleaseComObject(buffer);
            }

            Marshal.ReleaseComObject(sample);
        }

        Console.WriteLine("frames decoded: " + frames +
                          ", last at " + (last / 1e7).ToString("0.000") + " s");

        Console.WriteLine(audioOk && frames > 0 ? "OK" : "PROBLEM");

        return audioOk && frames > 0 ? 0 : 1;
    }

    // A 32-bit BMP, written TOP-DOWN (negative height) whatever way the
    // decoder handed the rows over, so what is on disk is what is seen
    private static void Bmp(
        string path, byte[] pixels, int width, int height, int stride)
    {
        int rowBytes = width * 4;
        int imageBytes = rowBytes * height;

        using (var f = new BinaryWriter(File.Create(path)))
        {
            f.Write((byte)'B');
            f.Write((byte)'M');
            f.Write(14 + 40 + imageBytes);
            f.Write(0);
            f.Write(14 + 40);

            f.Write(40);
            f.Write(width);
            f.Write(-height);
            f.Write((short)1);
            f.Write((short)32);
            f.Write(0);
            f.Write(imageBytes);
            f.Write(2835);
            f.Write(2835);
            f.Write(0);
            f.Write(0);

            int abs = Math.Abs(stride);

            for (int row = 0; row < height; row++)
            {
                // A negative stride means row 0 of the image is the LAST
                // row in memory
                int source = stride >= 0
                    ? row * abs
                    : (height - 1 - row) * abs;

                f.Write(pixels, source, rowBytes);
            }
        }
    }
}

[ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFSourceReader
{
    void GetStreamSelection(int stream, [MarshalAs(UnmanagedType.Bool)] out bool selected);
    void SetStreamSelection(int stream, [MarshalAs(UnmanagedType.Bool)] bool selected);
    void GetNativeMediaType(int stream, int typeIndex, out IMFMediaType type);
    void GetCurrentMediaType(int stream, out IMFMediaType type);
    void SetCurrentMediaType(int stream, IntPtr reserved, IMFMediaType type);
    void SetCurrentPosition(in Guid format, IntPtr position);
    void ReadSample(int stream, int control, out int actualStream, out int flags,
                    out long timestamp, out IMFSample sample);
    void Flush(int stream);
    void GetServiceForStream(int stream, in Guid service, in Guid riid, out IntPtr value);
    void GetPresentationAttribute(int stream, in Guid key, IntPtr value);
}
