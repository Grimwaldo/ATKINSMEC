using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Raw BGRA frames on stdin + a 16-bit PCM WAV  ->  an H.264/AAC MP4.
//
// Media Foundation's SinkWriter, which ships with Windows - no ffmpeg,
// nothing downloaded. The container is taken from the .mp4 extension.
//
//   mp4enc <out.mp4> <width> <height> <fps> <frames> <audio.wav>
//
// Every GUID and every vtable below was read out of the Windows SDK
// headers (mfapi.h, mfobjects.h, mfreadwrite.h, 10.0.26100.0), not
// remembered. C# COM interop has no interface inheritance, so each
// derived interface re-declares ALL of IMFAttributes' thirty methods in
// order before its own - one slot out and every later call lands on the
// wrong method.
internal static class Program
{
    // ---- GUIDs, from mfapi.h / mfreadwrite.h -------------------------

    static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    static readonly Guid MF_MT_AVG_BITRATE = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    static readonly Guid MF_MT_INTERLACE_MODE = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    static readonly Guid MF_MT_FRAME_SIZE = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
    static readonly Guid MF_MT_FRAME_RATE = new Guid("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    static readonly Guid MF_MT_DEFAULT_STRIDE = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new Guid("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
    static readonly Guid MF_MT_AAC_PAYLOAD_TYPE = new Guid("bfbabe79-7434-4d1c-94f0-72a3b9e17188");
    static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new Guid("a634a91c-822b-41b9-a494-4de4643612b0");

    static readonly Guid MFMediaType_Video = new Guid("73646976-0000-0010-8000-00AA00389B71");
    static readonly Guid MFMediaType_Audio = new Guid("73647561-0000-0010-8000-00AA00389B71");
    static readonly Guid MFVideoFormat_RGB32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
    static readonly Guid MFVideoFormat_H264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
    static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00AA00389B71");
    static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00AA00389B71");

    const int MF_VERSION = 0x00020070;
    const int MFVideoInterlace_Progressive = 2;

    // ---- entry points ---------------------------------------------------

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFStartup(int version, int flags);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateAttributes(out IMFAttributes attributes, int initialSize);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateMediaType(out IMFMediaType type);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateSample(out IMFSample sample);

    [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateMemoryBuffer(int maxLength, out IMFMediaBuffer buffer);

    [DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
    static extern void MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string url,
        IntPtr byteStream,
        IMFAttributes attributes,
        out IMFSinkWriter writer);

    // ---- the run --------------------------------------------------------

    private static int Main(string[] args)
    {
        if (args.Length >= 4 && args[0] == "check")
            return Check.Run(args);

        if (args.Length != 6)
        {
            Console.Error.WriteLine(
                "mp4enc <out.mp4> <width> <height> <fps> <frames> <audio.wav>");

            return 2;
        }

        string output = Path.GetFullPath(args[0]);
        int width = int.Parse(args[1]);
        int height = int.Parse(args[2]);
        int fps = int.Parse(args[3]);
        int frames = int.Parse(args[4]);

        Wav wav = Wav.Read(args[5]);

        if (File.Exists(output))
            File.Delete(output);

        MFStartup(MF_VERSION, 0);

        try
        {
            Encode(output, width, height, fps, frames, wav);
        }
        finally
        {
            MFShutdown();
        }

        Console.Error.WriteLine("wrote " + output);

        return 0;
    }

    private static void Encode(
        string output, int width, int height, int fps, int frames, Wav wav)
    {
        IMFAttributes attributes;

        MFCreateAttributes(out attributes, 1);
        attributes.SetUINT32(MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

        IMFSinkWriter writer;

        MFCreateSinkWriterFromURL(output, IntPtr.Zero, attributes, out writer);

        // ---- video: H.264 out, RGB32 in ----
        IMFMediaType videoOut;

        MFCreateMediaType(out videoOut);
        videoOut.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        videoOut.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_H264);
        videoOut.SetUINT32(MF_MT_AVG_BITRATE, 10_000_000);
        videoOut.SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
        videoOut.SetUINT64(MF_MT_FRAME_SIZE, Pack(width, height));
        videoOut.SetUINT64(MF_MT_FRAME_RATE, Pack(fps, 1));
        videoOut.SetUINT64(MF_MT_PIXEL_ASPECT_RATIO, Pack(1, 1));

        int videoStream;

        writer.AddStream(videoOut, out videoStream);

        IMFMediaType videoIn;

        MFCreateMediaType(out videoIn);
        videoIn.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video);
        videoIn.SetGUID(MF_MT_SUBTYPE, MFVideoFormat_RGB32);
        videoIn.SetUINT32(MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
        videoIn.SetUINT64(MF_MT_FRAME_SIZE, Pack(width, height));
        videoIn.SetUINT64(MF_MT_FRAME_RATE, Pack(fps, 1));
        videoIn.SetUINT64(MF_MT_PIXEL_ASPECT_RATIO, Pack(1, 1));

        // A POSITIVE stride says the rows run top to bottom. Left unset,
        // uncompressed RGB is taken as bottom-up and the whole video
        // comes out upside down.
        videoIn.SetUINT32(MF_MT_DEFAULT_STRIDE, width * 4);

        writer.SetInputMediaType(videoStream, videoIn, null);

        // ---- audio: AAC out, 16-bit PCM in ----
        IMFMediaType audioOut;

        MFCreateMediaType(out audioOut);
        audioOut.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        audioOut.SetGUID(MF_MT_SUBTYPE, MFAudioFormat_AAC);
        audioOut.SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        audioOut.SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, wav.Rate);
        audioOut.SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, wav.Channels);

        // The AAC encoder takes exactly 12000, 16000, 20000 or 24000
        audioOut.SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 24000);
        audioOut.SetUINT32(MF_MT_AAC_PAYLOAD_TYPE, 0);

        int audioStream;

        writer.AddStream(audioOut, out audioStream);

        IMFMediaType audioIn;

        MFCreateMediaType(out audioIn);
        audioIn.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
        audioIn.SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM);
        audioIn.SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
        audioIn.SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, wav.Rate);
        audioIn.SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, wav.Channels);
        audioIn.SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, wav.BlockAlign);
        audioIn.SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, wav.Rate * wav.BlockAlign);

        writer.SetInputMediaType(audioStream, audioIn, null);

        writer.BeginWriting();

        // ---- frames and sound, interleaved in time ----
        int frameBytes = width * height * 4;
        long frameTicks = 10_000_000L / fps;

        var frame = new byte[frameBytes];

        Stream input = Console.OpenStandardInput();

        // A tenth of a second of sound per sample
        int chunkFrames = wav.Rate / 10;
        long audioDone = 0;
        long audioTotal = wav.Data.Length / wav.BlockAlign;

        for (int i = 0; i < frames; i++)
        {
            ReadExactly(input, frame);

            WriteSample(writer, videoStream, frame, frameBytes,
                i * frameTicks, frameTicks);

            // Sound up to the end of this frame, so the two streams
            // arrive together rather than one buffering behind the other
            long until = (i + 1) * frameTicks;

            while (audioDone < audioTotal &&
                   audioDone * 10_000_000L / wav.Rate < until)
            {
                int take = (int)Math.Min(chunkFrames, audioTotal - audioDone);
                int bytes = take * wav.BlockAlign;

                var chunk = new byte[bytes];

                Buffer.BlockCopy(wav.Data, (int)(audioDone * wav.BlockAlign),
                    chunk, 0, bytes);

                long start = audioDone * 10_000_000L / wav.Rate;
                long length = (audioDone + take) * 10_000_000L / wav.Rate - start;

                WriteSample(writer, audioStream, chunk, bytes, start, length);

                audioDone += take;
            }

            if (i % 250 == 0)
                Console.Error.WriteLine("  frame " + i + " / " + frames);
        }

        writer.DoFinalize();

        Marshal.ReleaseComObject(writer);
    }

    private static void WriteSample(
        IMFSinkWriter writer, int stream, byte[] data, int length,
        long time, long duration)
    {
        IMFMediaBuffer buffer;

        MFCreateMemoryBuffer(length, out buffer);

        IntPtr pointer;
        int max;
        int current;

        buffer.Lock(out pointer, out max, out current);

        try
        {
            Marshal.Copy(data, 0, pointer, length);
        }
        finally
        {
            buffer.Unlock();
        }

        buffer.SetCurrentLength(length);

        IMFSample sample;

        MFCreateSample(out sample);
        sample.AddBuffer(buffer);
        sample.SetSampleTime(time);
        sample.SetSampleDuration(duration);

        writer.WriteSample(stream, sample);

        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buffer);
    }

    private static void ReadExactly(Stream input, byte[] into)
    {
        int got = 0;

        while (got < into.Length)
        {
            int n = input.Read(into, got, into.Length - got);

            if (n <= 0)
                throw new EndOfStreamException(
                    "stdin ended after " + got + " of " + into.Length +
                    " bytes of a frame");

            got += n;
        }
    }

    private static long Pack(int high, int low)
    {
        return ((long)high << 32) | (uint)low;
    }

    // ---- a WAV file -----------------------------------------------------

    private class Wav
    {
        public int Rate;
        public int Channels;
        public int BlockAlign;
        public byte[] Data;

        public static Wav Read(string path)
        {
            byte[] all = File.ReadAllBytes(path);

            if (Encoding.ASCII.GetString(all, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(all, 8, 4) != "WAVE")
                throw new InvalidDataException(path + " is not a WAV file");

            var wav = new Wav();
            int at = 12;

            while (at + 8 <= all.Length)
            {
                string id = Encoding.ASCII.GetString(all, at, 4);
                int size = BitConverter.ToInt32(all, at + 4);
                int body = at + 8;

                if (id == "fmt ")
                {
                    int format = BitConverter.ToInt16(all, body);
                    wav.Channels = BitConverter.ToInt16(all, body + 2);
                    wav.Rate = BitConverter.ToInt32(all, body + 4);
                    wav.BlockAlign = BitConverter.ToInt16(all, body + 12);
                    int bits = BitConverter.ToInt16(all, body + 14);

                    if (format != 1 || bits != 16)
                        throw new InvalidDataException(
                            "the WAV must be 16-bit PCM");
                }
                else if (id == "data")
                {
                    wav.Data = new byte[size];
                    Buffer.BlockCopy(all, body, wav.Data, 0, size);
                }

                at = body + size + (size & 1);
            }

            if (wav.Data == null || wav.Rate == 0)
                throw new InvalidDataException(path + " has no audio in it");

            return wav;
        }
    }
}

// ---- the interfaces, in vtable order from the SDK ------------------------
//
// IMFAttributes' thirty come first in every one of the derived ones.

[ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFAttributes
{
    void GetItem(in Guid key, IntPtr value);
    void GetItemType(in Guid key, out int type);
    void CompareItem(in Guid key, IntPtr value, out bool result);
    void Compare(IMFAttributes theirs, int type, out bool result);
    void GetUINT32(in Guid key, out int value);
    void GetUINT64(in Guid key, out long value);
    void GetDouble(in Guid key, out double value);
    void GetGUID(in Guid key, out Guid value);
    void GetStringLength(in Guid key, out int length);
    void GetString(in Guid key, IntPtr value, int size, out int length);
    void GetAllocatedString(in Guid key, out IntPtr value, out int length);
    void GetBlobSize(in Guid key, out int size);
    void GetBlob(in Guid key, IntPtr buffer, int size, out int written);
    void GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    void GetUnknown(in Guid key, in Guid riid, out IntPtr value);
    void SetItem(in Guid key, IntPtr value);
    void DeleteItem(in Guid key);
    void DeleteAllItems();
    void SetUINT32(in Guid key, int value);
    void SetUINT64(in Guid key, long value);
    void SetDouble(in Guid key, double value);
    void SetGUID(in Guid key, in Guid value);
    void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    void SetBlob(in Guid key, IntPtr buffer, int size);
    void SetUnknown(in Guid key, IntPtr value);
    void LockStore();
    void UnlockStore();
    void GetCount(out int count);
    void GetItemByIndex(int index, out Guid key, IntPtr value);
    void CopyAllItems(IMFAttributes destination);
}

[ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFMediaType
{
    void GetItem(in Guid key, IntPtr value);
    void GetItemType(in Guid key, out int type);
    void CompareItem(in Guid key, IntPtr value, out bool result);
    void Compare(IMFAttributes theirs, int type, out bool result);
    void GetUINT32(in Guid key, out int value);
    void GetUINT64(in Guid key, out long value);
    void GetDouble(in Guid key, out double value);
    void GetGUID(in Guid key, out Guid value);
    void GetStringLength(in Guid key, out int length);
    void GetString(in Guid key, IntPtr value, int size, out int length);
    void GetAllocatedString(in Guid key, out IntPtr value, out int length);
    void GetBlobSize(in Guid key, out int size);
    void GetBlob(in Guid key, IntPtr buffer, int size, out int written);
    void GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    void GetUnknown(in Guid key, in Guid riid, out IntPtr value);
    void SetItem(in Guid key, IntPtr value);
    void DeleteItem(in Guid key);
    void DeleteAllItems();
    void SetUINT32(in Guid key, int value);
    void SetUINT64(in Guid key, long value);
    void SetDouble(in Guid key, double value);
    void SetGUID(in Guid key, in Guid value);
    void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    void SetBlob(in Guid key, IntPtr buffer, int size);
    void SetUnknown(in Guid key, IntPtr value);
    void LockStore();
    void UnlockStore();
    void GetCount(out int count);
    void GetItemByIndex(int index, out Guid key, IntPtr value);
    void CopyAllItems(IMFAttributes destination);

    void GetMajorType(out Guid major);
    void IsCompressedFormat(out bool compressed);
    void IsEqual(IMFMediaType other, out int flags);
    void GetRepresentation(Guid representation, out IntPtr value);
    void FreeRepresentation(Guid representation, IntPtr value);
}

[ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFSample
{
    void GetItem(in Guid key, IntPtr value);
    void GetItemType(in Guid key, out int type);
    void CompareItem(in Guid key, IntPtr value, out bool result);
    void Compare(IMFAttributes theirs, int type, out bool result);
    void GetUINT32(in Guid key, out int value);
    void GetUINT64(in Guid key, out long value);
    void GetDouble(in Guid key, out double value);
    void GetGUID(in Guid key, out Guid value);
    void GetStringLength(in Guid key, out int length);
    void GetString(in Guid key, IntPtr value, int size, out int length);
    void GetAllocatedString(in Guid key, out IntPtr value, out int length);
    void GetBlobSize(in Guid key, out int size);
    void GetBlob(in Guid key, IntPtr buffer, int size, out int written);
    void GetAllocatedBlob(in Guid key, out IntPtr buffer, out int size);
    void GetUnknown(in Guid key, in Guid riid, out IntPtr value);
    void SetItem(in Guid key, IntPtr value);
    void DeleteItem(in Guid key);
    void DeleteAllItems();
    void SetUINT32(in Guid key, int value);
    void SetUINT64(in Guid key, long value);
    void SetDouble(in Guid key, double value);
    void SetGUID(in Guid key, in Guid value);
    void SetString(in Guid key, [MarshalAs(UnmanagedType.LPWStr)] string value);
    void SetBlob(in Guid key, IntPtr buffer, int size);
    void SetUnknown(in Guid key, IntPtr value);
    void LockStore();
    void UnlockStore();
    void GetCount(out int count);
    void GetItemByIndex(int index, out Guid key, IntPtr value);
    void CopyAllItems(IMFAttributes destination);

    void GetSampleFlags(out int flags);
    void SetSampleFlags(int flags);
    void GetSampleTime(out long time);
    void SetSampleTime(long time);
    void GetSampleDuration(out long duration);
    void SetSampleDuration(long duration);
    void GetBufferCount(out int count);
    void GetBufferByIndex(int index, out IMFMediaBuffer buffer);
    void ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
    void AddBuffer(IMFMediaBuffer buffer);
    void RemoveBufferByIndex(int index);
    void RemoveAllBuffers();
    void GetTotalLength(out int length);
    void CopyToBuffer(IMFMediaBuffer buffer);
}

[ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFMediaBuffer
{
    void Lock(out IntPtr buffer, out int maxLength, out int currentLength);
    void Unlock();
    void GetCurrentLength(out int length);
    void SetCurrentLength(int length);
    void GetMaxLength(out int length);
}

[ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMFSinkWriter
{
    void AddStream(IMFMediaType target, out int streamIndex);
    void SetInputMediaType(int streamIndex, IMFMediaType input, IMFAttributes encodingParameters);
    void BeginWriting();
    void WriteSample(int streamIndex, IMFSample sample);
    void SendStreamTick(int streamIndex, long timestamp);
    void PlaceMarker(int streamIndex, IntPtr context);
    void NotifyEndOfSegment(int streamIndex);
    void Flush(int streamIndex);

    // "Finalize" is taken by C#'s destructor; the slot is what matters
    void DoFinalize();

    void GetServiceForStream(int streamIndex, in Guid service, in Guid riid, out IntPtr value);
    void GetStatistics(int streamIndex, IntPtr statistics);
}
