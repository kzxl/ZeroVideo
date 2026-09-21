using System;
using System.Collections.Generic;
using ZeroVideo.Core;

namespace ZeroVideo.Playback
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    /// <summary>
    /// Thread-safe video playback controller with clock synchronization and frame-by-frame stepping.
    /// Ideal for both continuous camera stream display and forensic inspection review.
    /// </summary>
    public class VideoPlayer
    {
        private readonly Queue<VideoFrameBuffer> _frameQueue = new Queue<VideoFrameBuffer>();
        private readonly object _lock = new object();
        private PlaybackState _state = PlaybackState.Stopped;
        private VideoFrameBuffer? _currentFrame;

        public VideoClock Clock { get; } = new VideoClock();
        public PlaybackState State => _state;
        public VideoFrameBuffer? CurrentFrame => _currentFrame;
        public int QueueCount { get { lock (_lock) return _frameQueue.Count; } }

        public event EventHandler<VideoFrameBuffer>? FrameReady;
        public event EventHandler<PlaybackState>? StateChanged;

        public void EnqueueFrame(VideoFrameBuffer frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            lock (_lock)
            {
                _frameQueue.Enqueue(frame);
            }
        }

        public void Play()
        {
            lock (_lock)
            {
                if (_state == PlaybackState.Playing) return;

                if (_state == PlaybackState.Paused)
                {
                    Clock.Resume();
                }
                else
                {
                    Clock.Start();
                }

                _state = PlaybackState.Playing;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_state != PlaybackState.Playing) return;

                Clock.Pause();
                _state = PlaybackState.Paused;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                Clock.Stop();
                _frameQueue.Clear();
                _state = PlaybackState.Stopped;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Seek(TimeSpan position)
        {
            lock (_lock)
            {
                Clock.Seek(position);
            }
        }

        /// <summary>
        /// Advances playback by presenting the next queued frame immediately.
        /// </summary>
        public bool StepNextFrame()
        {
            VideoFrameBuffer? nextFrame = null;
            lock (_lock)
            {
                if (_frameQueue.Count > 0)
                {
                    nextFrame = _frameQueue.Dequeue();
                    _currentFrame = nextFrame;
                }
            }

            if (nextFrame != null)
            {
                FrameReady?.Invoke(this, nextFrame);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Synchronizes queued frames against the current master clock presentation time.
        /// Returns true if a new frame was presented.
        /// </summary>
        public bool Update()
        {
            if (_state != PlaybackState.Playing) return false;

            TimeSpan currentPts = Clock.Position;
            VideoFrameBuffer? frameToPresent = null;

            lock (_lock)
            {
                while (_frameQueue.Count > 0)
                {
                    var peek = _frameQueue.Peek();
                    if (peek.PresentationTime <= currentPts)
                    {
                        frameToPresent = _frameQueue.Dequeue();
                        _currentFrame = frameToPresent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (frameToPresent != null)
            {
                FrameReady?.Invoke(this, frameToPresent);
                return true;
            }

            return false;
        }
    }
}
