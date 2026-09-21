# ZeroVideo: Enterprise Pure C# Video Streaming, RTSP Transport & Multimedia Engine

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.0.0-blue.svg)](https://www.nuget.org/packages/ZeroVideo)

**ZeroVideo** is a sovereign, high-performance, pure C# video streaming, camera network transport, and multimedia playback engine for .NET. Completely free from heavyweight native C++ bindings (no ffmpeg or VLC dependencies required), it provides low-latency RTSP/RTP/H.264 stream ingestion, strided multi-format video frame buffers, fast BT.601 color conversions, and clock-synchronized video playback.

---

## Key Features

- **Strided Video Frame Buffer (`VideoFrameBuffer`)**: Low-latency pixel memory wrapping supporting Gray8, RGB24, BGR24, RGBA32, BGRA32, NV12, and YUV420P with nanosecond presentation timestamps (PTS).
- **RTSP 1.0 Client Transport (`RtspClient`)**: Pure C# session negotiation (OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, TEARDOWN) with SDP video track parsing for IP security and industrial GigE cameras.
- **RFC 3550 RTP Stream Demuxing (`RtpPacket`)**: Fast binary packet header parsing and serialization with SSRC and sequence number tracking.
- **H.264 AVC NAL Unit Scanner (`H264NaluParser`)**: Annex B start code detection (`0x000001` / `0x00000001`) and parameter set (SPS/PPS) extraction.
- **RFC 6184 FU-A Reassembly (`H264FuAReassembler`)**: Seamless reassembly of fragmented NAL units across RTP packet boundaries.
- **Fixed-Point BT.601 Color Conversion (`ColorConverter`)**: Integer-scaled SIMD-friendly conversion from YUV420P and NV12 to packed RGB24 and BGR24.
- **Master Playback Clock & Player (`VideoClock`, `VideoPlayer`)**: Drift-free PTS synchronization, variable playback rate (0.5x, 1x, 2x), seeking, and forensic frame-by-frame inspection stepping.

---

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
