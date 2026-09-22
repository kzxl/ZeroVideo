using System;
using System.Buffers;

namespace ZeroVideo.Core
{
    /// <summary>
    /// Represents a video frame buffer whose underlying memory is rented from an <see cref="ArrayPool{T}"/>.
    /// Disposing this frame buffer returns the rented byte array back to the pool, preventing LOH allocations.
    /// </summary>
    public class PooledVideoFrameBuffer : VideoFrameBuffer
    {
        private readonly ArrayPool<byte> _pool;
        private byte[]? _rentedArray;
        private bool _isDisposed;

        public override bool IsDisposed => _isDisposed;

        internal PooledVideoFrameBuffer(
            int width,
            int height,
            VideoPixelFormat pixelFormat,
            byte[] rentedArray,
            ArrayPool<byte> pool,
            int stride = 0)
            : base(width, height, pixelFormat, rentedArray, stride)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _rentedArray = rentedArray ?? throw new ArgumentNullException(nameof(rentedArray));
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                if (_rentedArray != null)
                {
                    _pool.Return(_rentedArray);
                    _rentedArray = null;
                }
            }
        }
    }

    /// <summary>
    /// High-throughput, zero-LOH buffer pool for recycling video frame memory in continuous streaming pipelines.
    /// Eliminates memory fragmentation and Gen-2 Garbage Collection pauses during high-FPS video processing.
    /// </summary>
    public class VideoFramePool
    {
        private readonly ArrayPool<byte> _pool;

        /// <summary>
        /// Global shared video frame memory pool.
        /// </summary>
        public static VideoFramePool Shared { get; } = new VideoFramePool(ArrayPool<byte>.Shared);

        public VideoFramePool(ArrayPool<byte>? pool = null)
        {
            _pool = pool ?? ArrayPool<byte>.Shared;
        }

        /// <summary>
        /// Rents a pooled video frame buffer with the specified dimensions and pixel format.
        /// The caller MUST dispose the returned <see cref="PooledVideoFrameBuffer"/> when done.
        /// </summary>
        public PooledVideoFrameBuffer Rent(int width, int height, VideoPixelFormat pixelFormat, int stride = 0)
        {
            int totalBytes = VideoFrameBuffer.CalculateTotalBytes(width, height, pixelFormat, stride);
            byte[] rented = _pool.Rent(totalBytes);
            return new PooledVideoFrameBuffer(width, height, pixelFormat, rented, _pool, stride);
        }

        /// <summary>
        /// Rents a pooled video frame buffer with timestamp and frame index initialized.
        /// </summary>
        public PooledVideoFrameBuffer Rent(
            int width,
            int height,
            VideoPixelFormat pixelFormat,
            long timestampNs,
            long frameIndex,
            int stride = 0)
        {
            var buffer = Rent(width, height, pixelFormat, stride);
            buffer.TimestampNs = timestampNs;
            buffer.FrameIndex = frameIndex;
            return buffer;
        }
    }
}
