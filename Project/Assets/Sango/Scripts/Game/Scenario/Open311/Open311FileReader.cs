using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 原版（三国志11）剧本 / 存档文件的读取器。
    ///
    /// 【出处】D:\open311\s11\s11_file.cpp（FileReader / FileIO::Header）
    ///
    /// 文件结构：
    ///   0x00  Header（0x5a 字节）
    ///   0x5a  正文：按固定顺序逐段的二进制记录（Building → Person → Item → …）
    ///
    /// Header 关键字段：
    ///   bom        0xfeff 表示大端，否则小端
    ///   type       4 = Scenario、0x16 = ScenPK、6 = Save、0x1a = SavePK …
    ///   path       "KOEI%SAN11"
    ///   compressed != 0 表示正文是 zlib 压缩块（以 0x1a43574d 为签名）
    ///   seed       != 0 表示正文逐值异或加密
    ///
    /// 【解密方式】每读一个标量（bool/uint8/uint16/uint32/字符）都 `^= rng.Next()`。
    /// 顺序必须与写入完全一致，错一位后续全乱，所以所有 read 方法都要成对使用。
    /// </summary>
    public class Open311FileReader
    {
        // ---------- 文件类型（s11_enum.h:29-49） ----------
        public const int FileType_Scen = 2;
        public const int FileType_Scenario = 4;
        public const int FileType_SysSave = 5;
        public const int FileType_Save = 6;
        public const int FileType_TS = 0x0c;
        public const int FileType_ScenPK = 0x16;
        public const int FileType_ScenarioPK = 0x18;
        public const int FileType_SysSavePK = 0x19;
        public const int FileType_SavePK = 0x1a;
        public const int FileType_TSPK = 0x1b;

        const int HeaderSize = 0x5a;
        const uint CompressSignature = 0x1a43574d;
        const string Magic = "KOEI%SAN11";

        public int Type { get; private set; }
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public bool Encrypted { get; private set; }
        public bool Compressed { get; private set; }
        public bool BigEndian { get; private set; }

        /// <summary>当前读取位置（相对当前缓冲区）</summary>
        public int Position => mPos;

        /// <summary>缓冲区长度</summary>
        public int Length => mBuffer != null ? mBuffer.Length : 0;

        byte[] mBuffer;
        int mPos;
        readonly Open311FileRng mRng = new Open311FileRng();

        /// <summary>打开一个剧本 / 存档文件（读入全部字节并完成解压）</summary>
        /// <returns>false = 不是合法的原版文件</returns>
        public bool Open(string path)
        {
            if (!File.Exists(path)) return false;
            return Open(File.ReadAllBytes(path));
        }

        public bool Open(byte[] raw)
        {
            if (raw == null || raw.Length < HeaderSize) return false;
            if (!ReadHeader(raw)) return false;

            mBuffer = raw;
            mPos = HeaderSize;

            if (Compressed)
                Decompress();

            return true;
        }

        bool ReadHeader(byte[] raw)
        {
            // Header 本身不加密，按原始字节解析
            uint bom = BitConverter.ToUInt32(raw, 0);
            BigEndian = bom == 0xfeff;

            Type = ReadHeaderInt32(raw, 4);
            if (Type == 0 || Type == 1) return false;

            string magic = Encoding.ASCII.GetString(raw, 8, 0x10).TrimEnd('\0');
            if (magic != Magic) return false;

            Major = ReadHeaderInt32(raw, 0x18);
            Minor = ReadHeaderInt32(raw, 0x1c);
            uint compressed = BitConverter.ToUInt32(raw, 0x20);
            uint seed = BitConverter.ToUInt32(raw, 0x24);

            Compressed = compressed != 0;
            Encrypted = seed != 0;
            if (Encrypted)
                mRng.Init(seed);

            return true;
        }

        int ReadHeaderInt32(byte[] raw, int offset)
        {
            uint v = BitConverter.ToUInt32(raw, offset);
            return unchecked((int)v);
        }

        /// <summary>
        /// zlib 解压（s11_file.cpp:183-226）。
        /// 签名不匹配时视为"未压缩"，回退 4 字节继续顺序读。
        /// </summary>
        void Decompress()
        {
            uint signature = ReadUInt32();
            if (signature != CompressSignature)
            {
                Seek(-4);
                Compressed = false;
                return;
            }

            int decompressedSize = unchecked((int)ReadUInt32());
            int compressedSize = unchecked((int)ReadUInt32());
            if (decompressedSize <= 0 || compressedSize <= 0) return;

            byte[] src = ReadRawBytes(compressedSize);
            byte[] dst = new byte[decompressedSize];

            // 原版用 zlib（inflateInit），数据带 2 字节 zlib 头，DeflateStream 需要跳过
            using (MemoryStream ms = new MemoryStream(src, 2, src.Length - 2))
            using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                int offset = 0;
                while (offset < dst.Length)
                {
                    int n = ds.Read(dst, offset, dst.Length - offset);
                    if (n <= 0) break;
                    offset += n;
                }
            }

            mBuffer = dst;
            mPos = 0;
        }

        // ==================== 位置 / 原始读 ====================

        public void Seek(int offset) { mPos += offset; }

        public void SeekTo(int position) { mPos = position; }

        public byte[] ReadRawBytes(int count)
        {
            if (count <= 0 || mPos + count > mBuffer.Length) return new byte[0];
            byte[] result = new byte[count];
            Buffer.BlockCopy(mBuffer, mPos, result, 0, count);
            mPos += count;
            return result;
        }

        /// <summary>跳过 n 个字节（不解密，仅推进指针）</summary>
        public void Skip(int n) { mPos += n; }

        // ==================== 解密读 ====================

        byte ReadRawByte()
        {
            if (mPos >= mBuffer.Length) return 0;
            return mBuffer[mPos++];
        }

        public bool ReadBool()
        {
            byte v = ReadRawByte();
            if (Encrypted) v ^= (byte)mRng.Next();
            return v != 0;
        }

        public sbyte ReadInt8()
        {
            byte v = ReadRawByte();
            if (Encrypted) v ^= (byte)mRng.Next();
            return unchecked((sbyte)v);
        }

        public byte ReadUInt8()
        {
            byte v = ReadRawByte();
            if (Encrypted) v ^= (byte)mRng.Next();
            return v;
        }

        public short ReadInt16()
        {
            uint v = BitConverter.ToUInt16(mBuffer, mPos);
            mPos += 2;
            if (Encrypted) v ^= mRng.Next();
            return unchecked((short)v);
        }

        public ushort ReadUInt16()
        {
            uint v = BitConverter.ToUInt16(mBuffer, mPos);
            mPos += 2;
            if (Encrypted) v ^= mRng.Next();
            return unchecked((ushort)v);
        }

        public int ReadInt32()
        {
            uint v = BitConverter.ToUInt32(mBuffer, mPos);
            mPos += 4;
            if (Encrypted) v ^= mRng.Next();
            return unchecked((int)v);
        }

        public uint ReadUInt32()
        {
            uint v = BitConverter.ToUInt32(mBuffer, mPos);
            mPos += 4;
            if (Encrypted) v ^= mRng.Next();
            return v;
        }

        public float ReadFloat()
        {
            uint v = BitConverter.ToUInt32(mBuffer, mPos);
            mPos += 4;
            if (Encrypted) v ^= mRng.Next();
            return BitConverter.ToSingle(BitConverter.GetBytes(v), 0);
        }

        /// <summary>读定长字符串（逐字符解密，与原版 read_string(char*, n) 一致）</summary>
        public string ReadString(int byteLength, Encoding encoding = null)
        {
            if (byteLength <= 0) return string.Empty;

            byte[] buf = new byte[byteLength];
            for (int i = 0; i < byteLength; i++)
                buf[i] = ReadUInt8();

            if (encoding == null)
                encoding = Encoding.UTF8;

            string s = encoding.GetString(buf);
            int zero = s.IndexOf('\0');
            if (zero >= 0) s = s.Substring(0, zero);
            return s;
        }
    }
}
