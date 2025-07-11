﻿using System.Text;

namespace WheelWizard.Helpers;

public static class BigEndianBinaryHelper
{
    //Helper functions to convert a buffer to an uint using big endian
    public static uint BufferToUint32(byte[] data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    public static uint BufferToUint16(byte[] data, int offset)
    {
        return (uint)((data[offset] << 8) | data[offset + 1]);
    }

    //big endian get the string
    public static string GetUtf16String(byte[] data, int offset, int maxLength)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < maxLength * 2; i += 2)
        {
            var b1 = data[offset + i];
            var b2 = data[offset + i + 1];
            if (b1 == 0 && b2 == 0)
                break;
            bytes.Add(b1);
            bytes.Add(b2);
        }
        return Encoding.BigEndianUnicode.GetString(bytes.ToArray());
    }

    public static void WriteUInt32BigEndian(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)((value >> 16) & 0xFF);
        data[offset + 2] = (byte)((value >> 8) & 0xFF);
        data[offset + 3] = (byte)(value & 0xFF);
    }

    public static float BufferToFloat(byte[] data, int offset)
    {
        // for a big-endian float before converting.
        var bytes = new byte[4];
        Array.Copy(data, offset, bytes, 0, 4);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static void WriteUInt16BigEndian(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Reads a 32-bit little-endian uint from the given byte array at the specified offset.
    /// </summary>
    public static uint ReadLittleEndianUInt32(byte[] data, int offset)
    {
        // Wii often uses little-endian for Mii blocks internally
        return (uint)((data[offset + 0] << 0) | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
    }
}
