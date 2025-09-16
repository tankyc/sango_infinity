//引用依赖的命名空间
using UnityEngine;
#if UNITY_STANDALONE_WIN 
using System.Drawing;
using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
#endif

//出自:https://blog.csdn.net/qq_42437783/article/details/125259145

//获取屏幕像素点的类
public class GetScreenPixel
{
#if UNITY_STANDALONE_WIN 
    private static Bitmap bitmapSrc;//屏幕快照的位图数据
    private static int multiple;//屏幕快照比例系数，可用于放大缩小
    private static Texture2D tex;

    public struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    /// <summary>   
    /// 获取鼠标的坐标   
    /// </summary>   
    /// <param name="lpPoint">传址参数，坐标point类型</param>   
    /// <returns>获取成功返回真</returns>   
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetCursorPos(out POINT pt);

#endif

    /// <summary>
    /// 截取鼠标点的屏幕快照，将其转为Unity的Texture2D纹理图像
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    public static Texture2D GetTexture(int width, int height)
    {
#if UNITY_STANDALONE_WIN 
        Size size = new Size(width, height);//截取的大小
        bitmapSrc = new Bitmap(width, height);//获取的位图大小
        multiple = 1;
        BitmapReset(bitmapSrc);//重置图片，解决超出屏幕部分图像残留BUG
        System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmapSrc);//根据位图数据创建新图
        POINT mPoint;
        GetCursorPos(out mPoint);
        g.CopyFromScreen(new Point(mPoint.X - width / (2 * multiple), mPoint.Y - height / (2 * multiple)), new Point(0, 0), size);//从屏幕上传输指定区域大小的图像数据到Graphics中绘制出来
        //g.CopyFromScreen(new Point(System.Windows.Forms.Cursor.Position.X - width / (2 * multiple), System.Windows.Forms.Cursor.Position.Y - height / (2 * multiple)), new Point(0, 0), size);//从屏幕上传输指定区域大小的图像数据到Graphics中绘制出来
        IntPtr dc1 = g.GetHdc();
        g.ReleaseHdc(dc1);//释放当前句柄
        if (tex == null) {
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.filterMode = FilterMode.Point;
        }
        tex.LoadImage(BitmapToByte(bitmapSrc));//加载图像字节数组到纹理。 
        return tex;
#else
        return null;
#endif

    }
#if UNITY_STANDALONE_WIN 
    /// <summary>
    /// 将bitmap位图流转为字节流数组
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    public static byte[] BitmapToByte(System.Drawing.Bitmap bitmap)
    {

        // 1.先将BitMap转成内存流
        System.IO.MemoryStream ms = new System.IO.MemoryStream();
        //bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);//Unity加载时不支持bmp格式数据
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);//将位图数据保存为png类型的数据
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        // 2.再将内存流转成byte[]并返回
        byte[] bytes = new byte[ms.Length];
        ms.Read(bytes, 0, bytes.Length);
        ms.Dispose();
        return bytes;
    }

    /// <summary>
    /// 重置位图
    /// </summary>
    /// <param name="bitmap"></param>
    private static void BitmapReset(Bitmap bitmap)
    {
        System.Drawing.Imaging.BitmapData bitmapdata = bitmap.LockBits(new Rectangle(new Point(0, 0), bitmap.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        unsafe {
            byte* dataPointer = (byte*)(bitmapdata.Scan0.ToPointer());//数据矩阵在内存中的地址指针
            for (int y = 0; y < bitmapdata.Height; y++) {
                for (int x = 0; x < bitmapdata.Width; x++) {
                    dataPointer[0] = 0;
                    dataPointer[1] = 0;
                    dataPointer[2] = 0;
                    dataPointer[3] = 0;
                    dataPointer += 4;
                }
            }
        }
        bitmap.UnlockBits(bitmapdata);
    }
#endif
}
