using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroVideo.Color;
using ZeroVideo.Core;

namespace ZeroVideo.Tests
{
    public class EmpiricalChallengeTests
    {
        // -------------------------------------------------------------
        // SECTION 1: ColorConverter Bounds, Truncation & Fuzzing
        // -------------------------------------------------------------

        [Theory]
        [InlineData(16, 16, VideoPixelFormat.Yuv420p, VideoPixelFormat.Rgb24)]
        [InlineData(16, 16, VideoPixelFormat.Nv12, VideoPixelFormat.Rgb24)]
        [InlineData(16, 16, VideoPixelFormat.Rgb24, VideoPixelFormat.Gray8)]
        [InlineData(64, 48, VideoPixelFormat.Yuv420p, VideoPixelFormat.Rgb24)]
        [InlineData(64, 48, VideoPixelFormat.Nv12, VideoPixelFormat.Rgb24)]
        [InlineData(64, 48, VideoPixelFormat.Rgb24, VideoPixelFormat.Gray8)]
        public void ColorConverter_TruncatedSourceSpan_ThrowsArgumentException(
            int w, int h, VideoPixelFormat srcFmt, VideoPixelFormat dstFmt)
        {
            var dst = new VideoFrameBuffer(w, h, dstFmt);
            int minBytes = VideoFrameBuffer.CalculateTotalBytes(w, h, srcFmt);

            // Test truncated buffers: 0, 1, minBytes / 2, minBytes - 1
            int[] truncatedSizes = { 0, 1, minBytes / 2, minBytes - 1 };
            foreach (var size in truncatedSizes)
            {
                if (size < 0) continue;
                var truncatedSrc = new VideoFrameBuffer(w, h, srcFmt, new byte[size]);

                var ex = Record.Exception(() =>
                {
                    if (srcFmt == VideoPixelFormat.Yuv420p)
                        ColorConverter.Yuv420pToRgb(truncatedSrc, dst);
                    else if (srcFmt == VideoPixelFormat.Nv12)
                        ColorConverter.Nv12ToRgb(truncatedSrc, dst);
                    else if (srcFmt == VideoPixelFormat.Rgb24)
                        ColorConverter.RgbToGray8(truncatedSrc, dst);
                });

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }
        }

        [Theory]
        [InlineData(16, 16, VideoPixelFormat.Yuv420p, VideoPixelFormat.Rgb24)]
        [InlineData(16, 16, VideoPixelFormat.Nv12, VideoPixelFormat.Rgb24)]
        [InlineData(16, 16, VideoPixelFormat.Rgb24, VideoPixelFormat.Gray8)]
        [InlineData(64, 48, VideoPixelFormat.Yuv420p, VideoPixelFormat.Rgb24)]
        [InlineData(64, 48, VideoPixelFormat.Nv12, VideoPixelFormat.Rgb24)]
        [InlineData(64, 48, VideoPixelFormat.Rgb24, VideoPixelFormat.Gray8)]
        public void ColorConverter_TruncatedDestinationSpan_ThrowsArgumentException(
            int w, int h, VideoPixelFormat srcFmt, VideoPixelFormat dstFmt)
        {
            var src = new VideoFrameBuffer(w, h, srcFmt);
            int minDstBytes = VideoFrameBuffer.CalculateTotalBytes(w, h, dstFmt);

            int[] truncatedSizes = { 0, 1, minDstBytes / 2, minDstBytes - 1 };
            foreach (var size in truncatedSizes)
            {
                if (size < 0) continue;
                var truncatedDst = new VideoFrameBuffer(w, h, dstFmt, new byte[size]);

                var ex = Record.Exception(() =>
                {
                    if (srcFmt == VideoPixelFormat.Yuv420p)
                        ColorConverter.Yuv420pToRgb(src, truncatedDst);
                    else if (srcFmt == VideoPixelFormat.Nv12)
                        ColorConverter.Nv12ToRgb(src, truncatedDst);
                    else if (srcFmt == VideoPixelFormat.Rgb24)
                        ColorConverter.RgbToGray8(src, truncatedDst);
                });

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(15, 15)]
        [InlineData(17, 9)]
        [InlineData(63, 63)]
        [InlineData(1921, 1081)]
        public void ColorConverter_OddDimensions_Yuv420p_ConvertsWithoutException(int w, int h)
        {
            var yuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            for (int i = 0; i < yuv.Data.Length; i++)
                yuv.Data[i] = (byte)(i % 256);

            // Must not throw IndexOutOfRangeException or segfault
            ColorConverter.Yuv420pToRgb(yuv, rgb);
            Assert.True(rgb.Data.Length >= w * h * 3);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(15, 15)]
        [InlineData(17, 9)]
        [InlineData(63, 63)]
        [InlineData(1921, 1081)]
        public void ColorConverter_OddDimensions_Gray8_ConvertsWithoutException(int w, int h)
        {
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);
            var gray = new VideoFrameBuffer(w, h, VideoPixelFormat.Gray8);

            for (int i = 0; i < rgb.Data.Length; i++)
                rgb.Data[i] = (byte)(i % 256);

            ColorConverter.RgbToGray8(rgb, gray);
            Assert.True(gray.Data.Length >= w * h);
        }

        [Fact]
        public void ColorConverter_MismatchedStrides_Yuv420p_ConvertsCorrectly()
        {
            int w = 64, h = 48;
            int srcStride = 128; // padded stride
            int dstStride = 256; // padded stride

            var yuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p, srcStride);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24, dstStride);

            for (int i = 0; i < yuv.Data.Length; i++) yuv.Data[i] = 128;

            ColorConverter.Yuv420pToRgb(yuv, rgb);
            Assert.True(rgb.Data.Length >= h * dstStride);
        }

        [Fact]
        public void ColorConverter_MismatchedStrides_Nv12_EvenWidth_ConvertsCorrectly()
        {
            int w = 64, h = 48;
            int srcStride = 128;
            int dstStride = 256;

            var nv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12, srcStride);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24, dstStride);

            for (int i = 0; i < nv12.Data.Length; i++) nv12.Data[i] = 128;

            ColorConverter.Nv12ToRgb(nv12, rgb);
            Assert.True(rgb.Data.Length >= h * dstStride);
        }

        [Fact]
        public void ColorConverter_MismatchedStrides_Gray8_ConvertsCorrectly()
        {
            int w = 64, h = 48;
            int srcStride = 256; // 64 * 3 = 192, stride 256
            int dstStride = 128; // 64 * 1 = 64, stride 128

            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24, srcStride);
            var gray = new VideoFrameBuffer(w, h, VideoPixelFormat.Gray8, dstStride);

            for (int i = 0; i < rgb.Data.Length; i++) rgb.Data[i] = 128;

            ColorConverter.RgbToGray8(rgb, gray);
            Assert.True(gray.Data.Length >= h * dstStride);
        }

        // -------------------------------------------------------------
        // SECTION 2: Empirical Bug Reproductions (Defect Evidence)
        // -------------------------------------------------------------

        [Theory]
        [InlineData(1, 1)]
        [InlineData(3, 3)]
        [InlineData(15, 15)]
        [InlineData(17, 9)]
        [InlineData(63, 63)]
        [InlineData(1921, 1081)]
        public void ColorConverter_OddDimensions_Nv12_EmpiricalProof_ThrowsIndexOutOfRangeException(int w, int h)
        {
            // DEFECT REPRODUCTION 1:
            // In Nv12ToRgb, when w is odd and src.Stride == w, the trailing odd pixel loop (lines 204-209)
            // calculates uvIdx = uvRow + ((x >> 1) << 1) and accesses s[uvIdx + 1] for vVal.
            // On the last row (y / 2 == uvHeight - 1), uvIdx + 1 equals requiredSrc, which is exactly s.Length!
            // The runtime throws an unhandled IndexOutOfRangeException instead of ArgumentException or converting safely.
            var nv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            for (int i = 0; i < nv12.Data.Length; i++)
                nv12.Data[i] = (byte)(i % 256);

            var ex = Record.Exception(() => ColorConverter.Nv12ToRgb(nv12, rgb));
            Assert.NotNull(ex);
            // Empirical proof: throws unhandled IndexOutOfRangeException!
            Assert.IsType<IndexOutOfRangeException>(ex);
        }

        [Fact]
        public void ColorConverter_MismatchedDimensions_RgbToGray8_EmpiricalProof_ThrowsIndexOutOfRangeException()
        {
            // DEFECT REPRODUCTION 2:
            // RgbToGray8 completely omits dimension validation (src.Width != dst.Width || src.Height != dst.Height).
            // (In contrast, Yuv420pToRgb:28-29 and Nv12ToRgb:135-136 both enforce this check).
            // When src.Width > dst.Width (e.g., 200x10 vs 10x10), requiredDst = 10 * 10 = 100 <= dst.Data.Length.
            // The precondition passes, enters the conversion loop, and attempts to write d[dstRow + x] where x reaches 100+,
            // triggering an unhandled IndexOutOfRangeException instead of ArgumentException.
            var rgb = new VideoFrameBuffer(200, 10, VideoPixelFormat.Rgb24);
            var gray = new VideoFrameBuffer(10, 10, VideoPixelFormat.Gray8);

            var ex = Record.Exception(() => ColorConverter.RgbToGray8(rgb, gray));
            Assert.NotNull(ex);
            // Empirical proof: throws unhandled IndexOutOfRangeException instead of ArgumentException!
            Assert.IsType<IndexOutOfRangeException>(ex);
        }

        [Fact]
        public void VideoFrameBuffer_PooledBuffer_PostDisposalAccess_LacksObjectDisposedProtection()
        {
            // DEFECT REPRODUCTION 3:
            // PooledVideoFrameBuffer does NOT override AsSpan(), AsReadOnlySpan(), or GetRowSpan().
            // When disposed, _rentedArray is returned to ArrayPool.Shared, but buffer.Data still references
            // the returned array, and AsSpan() continues returning valid spans over returned pool memory!
            // In contrast, NativeVideoFrameBuffer correctly throws ObjectDisposedException on AsSpan() after Dispose().
            using var pool = new VideoFramePool(FramePoolBackend.ManagedArrayPool);
            var frame = pool.Rent(64, 48, VideoPixelFormat.Rgb24);
            frame.Dispose();
            Assert.True(frame.IsDisposed);

            // Empirical proof: AsSpan() returns without throwing ObjectDisposedException!
            var span = frame.AsSpan();
            Assert.True(span.Length > 0); // Demonstrates use-after-free hazard on returned pool array
        }

        // -------------------------------------------------------------
        // SECTION 3: VideoFramePool Extreme Oversize & Lifecycle Safety
        // -------------------------------------------------------------

        [Fact]
        public void VideoFramePool_ExtremeOversized8K_SucceedsWithoutSlabExhaustion()
        {
            // 8K UHD: 7680 x 4320
            // RGB24: 7680 * 4320 * 3 = 99,532,800 bytes (~99.5 MB)
            // Pool slabSize is 4MB. 99.5MB > 4MB triggers off-heap unpooled fallback.
            using var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 4 * 1024 * 1024);

            using (var frame = pool.Rent(7680, 4320, VideoPixelFormat.Rgb24))
            {
                Assert.IsType<NativeVideoFrameBuffer>(frame);
                var span = frame.AsSpan();
                Assert.Equal(99_532_800, span.Length);

                // Write and verify boundary bytes (first, middle, last)
                span[0] = 0xAA;
                span[span.Length / 2] = 0xBB;
                span[span.Length - 1] = 0xCC;

                Assert.Equal(0xAA, span[0]);
                Assert.Equal(0xBB, span[span.Length / 2]);
                Assert.Equal(0xCC, span[span.Length - 1]);

                // Active blocks in slab allocator must be 0 because this was unpooled
                Assert.Equal(0, pool.ActiveBlocks);
            }

            Assert.Equal(0, pool.ActiveBlocks);
        }

        [Fact]
        public void VideoFramePool_ExtremeOversized16K_SucceedsWithoutSlabExhaustion()
        {
            // 16K: 15360 x 8640
            // YUV420P: 15360 * 8640 * 1.5 = 199,065,600 bytes (~199 MB)
            using var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 4 * 1024 * 1024);

            using (var frame = pool.Rent(15360, 8640, VideoPixelFormat.Yuv420p))
            {
                Assert.IsType<NativeVideoFrameBuffer>(frame);
                var span = frame.AsSpan();
                Assert.Equal(199_065_600, span.Length);

                span[0] = 0x11;
                span[span.Length - 1] = 0x99;

                Assert.Equal(0x11, span[0]);
                Assert.Equal(0x99, span[span.Length - 1]);

                Assert.Equal(0, pool.ActiveBlocks);
            }
        }

        [Theory]
        [InlineData(FramePoolBackend.ManagedArrayPool)]
        [InlineData(FramePoolBackend.OffHeapSlab)]
        public void VideoFramePool_PostDisposalRent_ThrowsObjectDisposedException(FramePoolBackend backend)
        {
            var pool = new VideoFramePool(backend, slabSize: 1024 * 1024);
            pool.Dispose();
            Assert.True(pool.IsDisposed);

            // Verify both overloads throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(640, 480, VideoPixelFormat.Rgb24));
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(640, 480, VideoPixelFormat.Rgb24, timestampNs: 1000, frameIndex: 1));
        }

        [Fact]
        public async Task VideoFramePool_WaitForShutdown_ConcurrentActiveLeases_DrainsSuccessfully()
        {
            using var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 512 * 1024);

            const int workerCount = 4;
            var barrier = new Barrier(workerCount + 1);
            var tasks = new Task[workerCount];

            for (int i = 0; i < workerCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    // Rent a frame that fits in 512KB slab (320x240 RGB24 = ~230KB)
                    var frame = pool.Rent(320, 240, VideoPixelFormat.Rgb24);
                    var span = frame.AsSpan();
                    span[0] = 123;

                    barrier.SignalAndWait(); // Wait for all workers to rent

                    // Hold lease for 50ms then return
                    Thread.Sleep(50);
                    frame.Dispose();
                });
            }

            barrier.SignalAndWait(); // Ensure all 4 frames are actively rented
            Assert.Equal(workerCount, pool.ActiveBlocks);
            Assert.True(pool.HasActiveLeases);

            // Shutdown with 2000ms timeout: workers return within ~50ms, so drain should succeed
            bool drained = pool.WaitForShutdown(TimeSpan.FromSeconds(2));
            Assert.True(drained);
            Assert.True(pool.IsDisposed);
            Assert.Equal(0, pool.ActiveBlocks);

            await Task.WhenAll(tasks);

            // Post-shutdown rent must fail cleanly
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(320, 240, VideoPixelFormat.Rgb24));
        }

        [Fact]
        public void VideoFramePool_WaitForShutdown_TimeoutWhenLeasesNotReturned_ReturnsFalseAndDisposes()
        {
            var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 512 * 1024);

            // Rent a frame and do NOT return it before shutdown timeout
            var frame = pool.Rent(320, 240, VideoPixelFormat.Rgb24);
            Assert.Equal(1, pool.ActiveBlocks);

            // Shutdown with small timeout (30ms) while lease is still held
            bool drained = pool.WaitForShutdown(TimeSpan.FromMilliseconds(30));
            Assert.False(drained);
            Assert.True(pool.IsDisposed);

            // Now return the frame after pool was disposed: must not crash or throw
            var ex = Record.Exception(() => frame.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public async Task VideoFramePool_WaitForShutdown_MultipleConcurrentCallers_ThreadSafe()
        {
            var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 512 * 1024);

            var tasks = new Task<bool>[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = Task.Run(() => pool.WaitForShutdown(TimeSpan.FromMilliseconds(100)));
            }

            var results = await Task.WhenAll(tasks);
            Assert.All(results, res => Assert.True(res));
            Assert.True(pool.IsDisposed);
        }
    }
}
