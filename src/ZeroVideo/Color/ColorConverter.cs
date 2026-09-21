using System;
using ZeroVideo.Core;

namespace ZeroVideo.Color
{
    /// <summary>
    /// Ultra-high-speed color space converter for industrial and consumer video streams.
    /// Implements pure C# integer-scaled ITU-R BT.601 color conversions without native dependencies.
    /// </summary>
    public static class ColorConverter
    {
        private static byte Clamp(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }

        /// <summary>
        /// Converts a planar YUV 4:2:0 frame (Yuv420p) to packed 24-bit RGB or BGR.
        /// </summary>
        public static void Yuv420pToRgb(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            int yPlaneSize = src.Stride * h;
            int uPlaneOffset = yPlaneSize;
            int vPlaneOffset = yPlaneSize + (yPlaneSize / 4);

            byte[] s = src.Data;
            byte[] d = dst.Data;

            int uvStride = src.Stride / 2;

            for (int y = 0; y < h; y++)
            {
                int yRow = y * src.Stride;
                int uvRow = (y / 2) * uvStride;
                int dRow = y * dst.Stride;

                for (int x = 0; x < w; x++)
                {
                    int yVal = s[yRow + x];
                    int uVal = s[uPlaneOffset + uvRow + (x / 2)] - 128;
                    int vVal = s[vPlaneOffset + uvRow + (x / 2)] - 128;

                    // Fixed-point BT.601:
                    // R = Y + 1.402 * V
                    // G = Y - 0.344136 * U - 0.714136 * V
                    // B = Y + 1.772 * U
                    int r = yVal + ((359 * vVal) >> 8);
                    int g = yVal - ((88 * uVal + 183 * vVal) >> 8);
                    int b = yVal + ((454 * uVal) >> 8);

                    int dstIdx = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx] = Clamp(b);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(r);
                    }
                    else
                    {
                        d[dstIdx] = Clamp(r);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(b);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a semi-planar NV12 frame to packed 24-bit RGB or BGR.
        /// </summary>
        public static void Nv12ToRgb(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            int uvOffset = src.Stride * h;
            byte[] s = src.Data;
            byte[] d = dst.Data;

            for (int y = 0; y < h; y++)
            {
                int yRow = y * src.Stride;
                int uvRow = uvOffset + (y / 2) * src.Stride;
                int dRow = y * dst.Stride;

                for (int x = 0; x < w; x++)
                {
                    int yVal = s[yRow + x];
                    int uvIdx = uvRow + ((x / 2) * 2);
                    int uVal = s[uvIdx] - 128;
                    int vVal = s[uvIdx + 1] - 128;

                    int r = yVal + ((359 * vVal) >> 8);
                    int g = yVal - ((88 * uVal + 183 * vVal) >> 8);
                    int b = yVal + ((454 * uVal) >> 8);

                    int dstIdx = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx] = Clamp(b);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(r);
                    }
                    else
                    {
                        d[dstIdx] = Clamp(r);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(b);
                    }
                }
            }
        }

        /// <summary>
        /// Converts RGB24 or BGR24 to 8-bit monochrome grayscale.
        /// </summary>
        public static void RgbToGray8(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            int w = src.Width;
            int h = src.Height;
            byte[] s = src.Data;
            byte[] d = dst.Data;

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * src.Stride;
                int dstRow = y * dst.Stride;

                for (int x = 0; x < w; x++)
                {
                    int sIdx = srcRow + (x * 3);
                    int r = isBgr ? s[sIdx + 2] : s[sIdx];
                    int g = s[sIdx + 1];
                    int b = isBgr ? s[sIdx] : s[sIdx + 2];

                    // Rec. 601 luma: 0.299 R + 0.587 G + 0.114 B
                    int luma = (r * 77 + g * 150 + b * 29) >> 8;
                    d[dstRow + x] = (byte)luma;
                }
            }
        }
    }
}
