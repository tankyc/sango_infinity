using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Data
{
    public class BmpLoader
    {
        static Vector3Int[] indexKey = new Vector3Int[]
         {
        new Vector3Int(129,190,225),
        new Vector3Int(170,22,255),
        new Vector3Int(127,115,78),
        new Vector3Int(164,149,108),
        new Vector3Int(186,176,130),
        new Vector3Int(39,146,130),

        new Vector3Int(142,142,142),
        new Vector3Int(147,142,129),
        new Vector3Int(171,171,157),
        new Vector3Int(36,95,123),
        new Vector3Int(73,122,140),
        new Vector3Int(215,95,43),

        new Vector3Int(123,36,114),
        new Vector3Int(212,204,149),
        new Vector3Int(221,147,3),
        new Vector3Int(59,185,65),
        new Vector3Int(161,98,88),
        new Vector3Int(255,128,128),

        new Vector3Int(191,148,123),
        new Vector3Int(212,208,203),
        new Vector3Int(255,179,167),
        new Vector3Int(166,100,152),
        new Vector3Int(91,90,85),
        new Vector3Int(95,157,66),

        new Vector3Int(100,140,160),
        new Vector3Int(249,73,62),
        new Vector3Int(146,74,132),
        new Vector3Int(175,134,64),
        new Vector3Int(228,188,84),
        new Vector3Int(179,218,143),

        new Vector3Int(35,120,18),
        new Vector3Int(195,173,118),
        new Vector3Int(133,174,107),
        new Vector3Int(254,255,190),
        new Vector3Int(254,255,230),
        new Vector3Int(0,0,0),
         };

        public struct BmpInfo
        {
            public static int BmpHeaderSize = 54;
            public int width;
            public int height;
            public short bitCount;
            public int fixedWidth;
            public int byteSkip;
            public long totalSize;
            private Stream fileStream;

            public BmpInfo(string path)
            {
                try
                {
                    Stream stream = System.IO.File.OpenRead(path);  // 打开位图文件
                    fileStream = stream;

                    byte[] buff = new byte[4];
                    stream.Position = 18;
                    stream.Read(buff, 0, 4);
                    width = BitConverter.ToInt32(buff, 0);
                    stream.Read(buff, 0, 4);
                    height = BitConverter.ToInt32(buff, 0);
                    stream.Position = 28;
                    stream.Read(buff, 0, 2);
                    bitCount = BitConverter.ToInt16(buff, 0);
                    fixedWidth = ((width * bitCount + bitCount - 1) >> 5) << 2;
                    byteSkip = 4 - ((width * bitCount) >> 3) & 3;
                    totalSize = (long)width * (long)height;
                }
                catch
                {
                    width = 0;
                    height = 0;
                    bitCount = 0;
                    fixedWidth = 0;
                    totalSize = 0;
                    byteSkip = 0;
                    fileStream = null;
                }
            }

            public bool Valid()
            {
                return fileStream != null;
            }

            public void BeginRead()
            {
                fileStream.Position = BmpInfo.BmpHeaderSize;  // 跳过文件头和信息头
            }

            public Color ReadColor()
            {
                int b = fileStream.ReadByte();
                int g = fileStream.ReadByte();
                int r = fileStream.ReadByte();
                Color c = new Color(r, g, b);
                if (bitCount > 24)
                    c.a = fileStream.ReadByte();
                return c;
            }

            public void ReadColor(out int r, out int g, out int b, out int a)
            {
                b = fileStream.ReadByte();
                g = fileStream.ReadByte();
                r = fileStream.ReadByte();
                if (bitCount > 24)
                    a = fileStream.ReadByte();
                else
                    a = 0;
            }

            public void ReadB(out int b)
            {
                b = fileStream.ReadByte();
                fileStream.Position += 2;
                if (bitCount > 24)
                    fileStream.Position += 1;
            }

            public void SkipUnusedData()
            {
                fileStream.Position += byteSkip;
            }

            public void Close()
            {
                if (fileStream != null)
                    fileStream.Close();
            }
        }


        public static int[] LoadTextureIndex(string path)
        {
            Stream stream = System.IO.File.OpenRead(path);  // 打开位图文件
            BmpInfo bmpInfo = new BmpInfo(path);
            stream.Position = BmpInfo.BmpHeaderSize;  // 跳过文件头和信息头
            int[] indexBytes = new int[bmpInfo.totalSize];
            for (int h = 0; h < bmpInfo.height; h++)
            {
                for (int w = 0; w < bmpInfo.width; w++)
                {
                    int b = stream.ReadByte();
                    int g = stream.ReadByte();
                    int r = stream.ReadByte();
                    if (bmpInfo.bitCount > 24)
                        stream.ReadByte();

                    Vector3Int color = new Vector3Int(r, g, b);
                    int index = Array.IndexOf(indexKey, color);
                    if (index < 0)
                    {
                        Debug.LogError(color.ToString());
                        return null;
                    }
                    indexBytes[h * bmpInfo.width + w] = (byte)index;
                }
                stream.Position += bmpInfo.byteSkip;
            }
            bmpInfo.Close();
            stream.Close();
            return indexBytes;
        }

        public static int[] LoadHeight(string path)
        {
            Stream stream = System.IO.File.OpenRead(path);  // 打开位图文件
            BmpInfo bmpInfo = new BmpInfo(path);
            stream.Position = BmpInfo.BmpHeaderSize;  // 跳过文件头和信息头
            int[] indexBytes = new int[bmpInfo.totalSize];
            for (int h = 0; h < bmpInfo.height; h++)
            {
                for (int w = 0; w < bmpInfo.width; w++)
                {
                    indexBytes[h * bmpInfo.width + w] = stream.ReadByte();
                    stream.Position += 2;
                    if (bmpInfo.bitCount > 24)
                        stream.Position += 1;
                }
                stream.Position += bmpInfo.byteSkip;
            }
            bmpInfo.Close();
            stream.Close();
            return indexBytes;
        }

        public static int[] LoadWaterHeight(string path)
        {
            return LoadHeight(path);
        }
    }
}

