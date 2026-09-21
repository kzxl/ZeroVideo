using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using ZeroVideo.Color;
using ZeroVideo.Core;
using ZeroVideo.Playback;
using ZeroVideo.Transport;

namespace ZeroVideo.Tests
{
    public class VideoTests
    {
        [Fact]
        public void VideoFrameBuffer_AllocationsAndCloning_WorkCorrectly()
        {
            // 1. RGB24 Frame
            var rgb = new VideoFrameBuffer(640, 480, VideoPixelFormat.Rgb24);
            Assert.Equal(640, rgb.Width);
            Assert.Equal(480, rgb.Height);
            Assert.Equal(640 * 3, rgb.Stride);
            Assert.Equal(640 * 480 * 3, rgb.Data.Length);

            // 2. YUV420P Frame
            var yuv = new VideoFrameBuffer(1920, 1080, VideoPixelFormat.Yuv420p);
            Assert.Equal(1920 * 1080 * 3 / 2, yuv.Data.Length);

            // 3. Clone and timestamp
            rgb.TimestampNs = 1_000_000_000; // 1 second
            rgb.FrameIndex = 42;
            rgb.Data[0] = 255; // Red pixel

            var clone = rgb.Clone();
            Assert.Equal(rgb.TimestampNs, clone.TimestampNs);
            Assert.Equal(rgb.FrameIndex, clone.FrameIndex);
            Assert.Equal(TimeSpan.FromSeconds(1), clone.PresentationTime);
            Assert.Equal(255, clone.Data[0]);

            // Mutate clone to ensure deep copy
            clone.Data[0] = 0;
            Assert.Equal(255, rgb.Data[0]);

            // 4. Luminance
            rgb.Data[0] = 255; rgb.Data[1] = 255; rgb.Data[2] = 255; // White pixel
            Assert.True(rgb.GetLuminance(0, 0) > 0.99f);
        }

        [Fact]
        public void ColorConverter_Yuv420pAndNv12ToRgb_ConvertsAccurately()
        {
            int w = 16, h = 16;
            var yuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            // Fill with pure white in YUV: Y=235, U=128, V=128
            int yPlaneSize = w * h;
            for (int i = 0; i < yPlaneSize; i++) yuv.Data[i] = 235;
            for (int i = yPlaneSize; i < yuv.Data.Length; i++) yuv.Data[i] = 128;

            ColorConverter.Yuv420pToRgb(yuv, rgb);

            // Pixel (0,0) in RGB should be approx 235
            Assert.Equal(235, rgb.Data[0]);
            Assert.Equal(235, rgb.Data[1]);
            Assert.Equal(235, rgb.Data[2]);

            // NV12 Test
            var nv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12);
            for (int i = 0; i < yPlaneSize; i++) nv12.Data[i] = 235;
            for (int i = yPlaneSize; i < nv12.Data.Length; i++) nv12.Data[i] = 128;

            var bgr = new VideoFrameBuffer(w, h, VideoPixelFormat.Bgr24);
            ColorConverter.Nv12ToRgb(nv12, bgr, isBgr: true);

            Assert.Equal(235, bgr.Data[0]); // Blue
            Assert.Equal(235, bgr.Data[1]); // Green
            Assert.Equal(235, bgr.Data[2]); // Red

            // RGB to Gray8
            var gray = new VideoFrameBuffer(w, h, VideoPixelFormat.Gray8);
            ColorConverter.RgbToGray8(rgb, gray);
            Assert.True(gray.Data[0] >= 234 && gray.Data[0] <= 236);
        }

        [Fact]
        public void RtpPacket_ParseAndSerialize_RoundTripsAccurately()
        {
            var packet = new RtpPacket
            {
                Version = 2,
                PayloadType = 96,
                SequenceNumber = 10542,
                Timestamp = 3840294,
                Ssrc = 0xDEADBEEF,
                Marker = true,
                Payload = new byte[] { 0x67, 0x42, 0x00, 0x1F, 0x95, 0xA8 }
            };

            byte[] rawBytes = packet.ToByteArray();
            Assert.NotNull(rawBytes);
            Assert.Equal(12 + packet.Payload.Length, rawBytes.Length);

            bool success = RtpPacket.TryParse(rawBytes, out var parsed);
            Assert.True(success);
            Assert.NotNull(parsed);

            Assert.Equal(packet.Version, parsed.Version);
            Assert.Equal(packet.PayloadType, parsed.PayloadType);
            Assert.Equal(packet.SequenceNumber, parsed.SequenceNumber);
            Assert.Equal(packet.Timestamp, parsed.Timestamp);
            Assert.Equal(packet.Ssrc, parsed.Ssrc);
            Assert.True(parsed.Marker);
            Assert.Equal(packet.Payload, parsed.Payload);
        }

        [Fact]
        public void H264NaluParser_ExtractsAnnexBNalus()
        {
            // Build Annex B stream: 4-byte start code + SPS (0x67), 3-byte start code + IDR (0x65)
            var stream = new List<byte>();

            // SPS
            stream.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x1E });
            // IDR
            stream.AddRange(new byte[] { 0x00, 0x00, 0x01, 0x65, 0x88, 0x84, 0x00, 0xFF });

            var nalus = H264NaluParser.ExtractNalus(stream.ToArray());

            Assert.Equal(2, nalus.Count);
            Assert.Equal(NaluType.Sps, nalus[0].Type);
            Assert.True(nalus[0].IsParameterSet);
            Assert.Equal(3, nalus[0].Payload.Length); // 0x42, 0x00, 0x1E

            Assert.Equal(NaluType.IdrSlice, nalus[1].Type);
            Assert.True(nalus[1].IsKeyFrame);
            Assert.Equal(4, nalus[1].Payload.Length);
        }

        [Fact]
        public void H264FuAReassembler_ReassemblesFragments()
        {
            var reassembler = new H264FuAReassembler();

            // Original NALU to transmit: IDR Slice (0x25 = 001 00101)
            // Payload: 10 bytes of data
            byte[] originalPayload = new byte[10];
            for (int i = 0; i < originalPayload.Length; i++) originalPayload[i] = (byte)(i + 1);

            // Packet 1: Start fragment (S=1, E=0, Type=5)
            // Indicator: F=0, NRI=1 (001 11100 = 0x3C -> FuA)
            byte fuIndicator = (byte)((1 << 5) | 28);
            byte fuHeaderStart = (byte)(0x80 | 5);
            byte[] frag1 = new byte[] { fuIndicator, fuHeaderStart, 1, 2, 3, 4, 5 };

            // Packet 2: End fragment (S=0, E=1, Type=5)
            byte fuHeaderEnd = (byte)(0x40 | 5);
            byte[] frag2 = new byte[] { fuIndicator, fuHeaderEnd, 6, 7, 8, 9, 10 };

            var nalu1 = reassembler.ProcessRtpPayload(frag1);
            Assert.Null(nalu1); // Awaiting end fragment
            Assert.True(reassembler.IsAssembling);

            var nalu2 = reassembler.ProcessRtpPayload(frag2);
            Assert.NotNull(nalu2); // Reassembly complete
            Assert.False(reassembler.IsAssembling);

            Assert.Equal(NaluType.IdrSlice, nalu2.Type);
            Assert.Equal(originalPayload, nalu2.Payload);
        }

        [Fact]
        public void RtspClient_FormatsRequestsAndParsesResponses()
        {
            // 1. Options Request
            string opt = RtspClient.CreateOptionsRequest("rtsp://192.168.1.100:554/live", 1);
            Assert.Contains("OPTIONS rtsp://192.168.1.100:554/live RTSP/1.0", opt);
            Assert.Contains("CSeq: 1", opt);

            // 2. Parse Response
            string resp = "RTSP/1.0 200 OK\r\n" +
                          "CSeq: 1\r\n" +
                          "Session: A8492041\r\n" +
                          "Content-Type: application/sdp\r\n\r\n" +
                          "v=0\r\nm=video 0 RTP/AVP 96\r\na=control:trackID=1\r\n";

            bool ok = RtspClient.TryParseResponse(resp, out int status, out var headers, out string body);
            Assert.True(ok);
            Assert.Equal(200, status);
            Assert.Equal("A8492041", headers["Session"]);
            Assert.Contains("m=video", body);

            // 3. Extract Video Track
            string trackUri = RtspClient.ExtractVideoTrack(body, "rtsp://192.168.1.100:554/live");
            Assert.Equal("rtsp://192.168.1.100:554/live/trackID=1", trackUri);
        }

        [Fact]
        public void VideoClock_And_VideoPlayer_StateAndStepping()
        {
            var player = new VideoPlayer();
            Assert.Equal(PlaybackState.Stopped, player.State);

            // Enqueue 3 frames
            var f1 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(0) };
            var f2 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(40) };
            var f3 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(80) };

            player.EnqueueFrame(f1);
            player.EnqueueFrame(f2);
            player.EnqueueFrame(f3);
            Assert.Equal(3, player.QueueCount);

            // Step frame by frame
            bool step1 = player.StepNextFrame();
            Assert.True(step1);
            Assert.Same(f1, player.CurrentFrame);
            Assert.Equal(2, player.QueueCount);

            // Play & Pause
            player.Play();
            Assert.Equal(PlaybackState.Playing, player.State);
            Assert.True(player.Clock.IsRunning);

            player.Pause();
            Assert.Equal(PlaybackState.Paused, player.State);
            Assert.False(player.Clock.IsRunning);

            player.Seek(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromSeconds(5), player.Clock.Position);

            player.Stop();
            Assert.Equal(PlaybackState.Stopped, player.State);
            Assert.Equal(0, player.QueueCount);
        }
    }
}
