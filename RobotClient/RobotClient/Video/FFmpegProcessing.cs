using SIPSorcery.Net;
using System.Net;
using System.Diagnostics;

namespace RobotClient.Video
{
    internal class FFmpegProcessing
    {
        public SDPAudioVideoMediaFormat videoFormat;
        public RTPSession listener;

        public CancellationTokenSource exitCts;

        private const uint SSRC_REMOTE_VIDEO = 38106908;

        // Команда для реальной камеры на Linux (по умолчанию /dev/video0)
        private const string FFMPEG_CAMERA_COMMAND = "ffmpeg -f v4l2 -i /dev/video0 -video_size 640x480 -framerate 30 {0} -pix_fmt yuv420p -strict experimental -g 1 -ssrc {2} -f rtp rtp://127.0.0.1:{1} -sdp_file {3}";
        // Команда для тестового источника (если камера недоступна)
        private const string FFMPEG_TEST_COMMAND = "ffmpeg -re -f lavfi -i testsrc=size=640x480:rate=30 {0} -pix_fmt yuv420p -strict experimental -g 1 -ssrc {2} -f rtp rtp://127.0.0.1:{1} -sdp_file {3}";
        private const string FFMPEG_SDP_FILE = "ffmpeg.sdp";
        private const int FFMPEG_DEFAULT_RTP_PORT = 5020;

        /// <summary>
        /// The codec to pass to ffmpeg via the command line. WebRTC supported options are:
        /// - vp8
        /// - vp9
        /// - h264
        /// - libx265
        /// Note if you change this option you will need to delete the ffmpeg.sdp file.
        /// </summary>
        private const string FFMPEG_VP8_CODEC = "-vcodec vp8";
        private const string FFMPEG_VP9_CODEC = "-vcodec vp9";
        private const string FFMPEG_H264_CODEC = "-vcodec h264";
        private const string FFMPEG_H265_CODEC = "-c:v libx265";
        private const string FFMPEG_DEFAULT_CODEC = FFMPEG_H264_CODEC;       

        public async Task Initialize(string[] args)
        {
            string videoCodec = FFMPEG_DEFAULT_CODEC;
            string cameraDevice = "/dev/video0";
            bool useTestSource = false;

            // Парсим аргументы
            if (args?.Length > 0)
            {
                foreach (var arg in args)
                {
                    switch (arg.ToLower())
                    {
                        case "-vcodec":
                        case FFMPEG_VP8_CODEC:
                        case FFMPEG_VP9_CODEC:
                        case FFMPEG_H264_CODEC:
                        case FFMPEG_H265_CODEC:
                            videoCodec = arg.ToLower();
                            break;
                        case "-test":
                            useTestSource = true;
                            Console.WriteLine("🧪 Используется тестовый источник видео");
                            break;
                        default:
                            if (arg.StartsWith("/dev/video"))
                            {
                                cameraDevice = arg;
                                Console.WriteLine($"📷 Выбрано устройство камеры: {cameraDevice}");
                            }
                            break;
                    }
                }
            }

            exitCts = new CancellationTokenSource();

            // Выбираем команду FFmpeg
            string ffmpegCommand;
            if (useTestSource)
            {
                ffmpegCommand = String.Format(FFMPEG_TEST_COMMAND, videoCodec, FFMPEG_DEFAULT_RTP_PORT, SSRC_REMOTE_VIDEO, FFMPEG_SDP_FILE);
                Console.WriteLine("🧪 Используется тестовый источник FFmpeg");
            }
            else
            {
                // Заменяем /dev/video0 на выбранное устройство
                string cameraCommand = FFMPEG_CAMERA_COMMAND.Replace("/dev/video0", cameraDevice);
                ffmpegCommand = String.Format(cameraCommand, videoCodec, FFMPEG_DEFAULT_RTP_PORT, SSRC_REMOTE_VIDEO, FFMPEG_SDP_FILE);
                Console.WriteLine($"📷 Используется камера: {cameraDevice}");
            }

            if (File.Exists(FFMPEG_SDP_FILE))
            {
                string codecName = GetCodecName();
                if (!videoCodec.Contains(codecName))
                {
                    Console.WriteLine($"Removing existing ffmpeg SDP file {FFMPEG_SDP_FILE} due to codec mismatch.");
                    File.Delete(FFMPEG_SDP_FILE);
                }
            }

            Console.WriteLine("🎬 Запуск FFmpeg с командой:");
            Console.WriteLine(ffmpegCommand);
            Console.WriteLine();
            Console.WriteLine("📋 Доступные параметры:");
            Console.WriteLine("  -test                  - использовать тестовый источник");
            Console.WriteLine("  /dev/videoX           - выбрать камеру (например /dev/video0)");
            Console.WriteLine("  -vcodec h264|vp8|vp9  - выбрать кодек");

            if (!File.Exists(FFMPEG_SDP_FILE))
            {
                Console.WriteLine();
                Console.WriteLine($"⏳ Ожидание создания {FFMPEG_SDP_FILE} файла...");
            }

            // Запускаем FFmpeg процесс
            _ = Task.Run(async () => await StartFfmpegProcess(ffmpegCommand, exitCts.Token));
            
            // Ждем инициализации listener
            await StartFfmpegListener(FFMPEG_SDP_FILE, exitCts.Token);

            Console.WriteLine($"✅ FFmpeg listener создан на порту {FFMPEG_DEFAULT_RTP_PORT} с видео форматом {videoFormat.Name()}.");
        }

        private async Task StartFfmpegProcess(string ffmpegCommand, CancellationToken cancel)
        {
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "bash";
                    process.StartInfo.Arguments = $"-c \"{ffmpegCommand}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.WriteLine($"FFmpeg OUT: {e.Data}");
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Console.WriteLine($"FFmpeg ERR: {e.Data}");
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    Console.WriteLine($"🎬 FFmpeg процесс запущен (PID: {process.Id})");

                    // Ждем отмены или завершения процесса
                    while (!process.HasExited && !cancel.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }

                    if (cancel.IsCancellationRequested && !process.HasExited)
                    {
                        Console.WriteLine("🛑 Завершаем FFmpeg процесс...");
                        process.Kill();
                        await Task.Delay(1000); // Даем время на завершение
                    }

                    Console.WriteLine($"🛑 FFmpeg процесс завершен с кодом: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска FFmpeg процесса: {ex.Message}");
                throw;
            }
        }

        private async Task StartFfmpegListener(string sdpPath, CancellationToken cancel)
        {
            while (!File.Exists(FFMPEG_SDP_FILE) && !cancel.IsCancellationRequested)
            {
                await Task.Delay(500);
            }

            if (!cancel.IsCancellationRequested)
            {
                var sdp = SDP.ParseSDPDescription(File.ReadAllText(FFMPEG_SDP_FILE));

                // The SDP is only expected to contain a single video media announcement.
                var videoAnn = sdp.Media.Single(x => x.Media == SDPMediaTypesEnum.video);
                videoFormat = videoAnn.MediaFormats.Values.First();

                listener = new RTPSession(false, false, false, IPAddress.Loopback, FFMPEG_DEFAULT_RTP_PORT);
                listener.AcceptRtpFromAny = true;

                MediaStreamTrack videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, new List<SDPAudioVideoMediaFormat> { videoFormat }, MediaStreamStatusEnum.RecvOnly);
                videoTrack.Ssrc = SSRC_REMOTE_VIDEO; //   /!\ Need to set the correct SSRC in order to accept RTP stream
                listener.addTrack(videoTrack);

                listener.SetRemoteDescription(SIPSorcery.SIP.App.SdpType.answer, sdp);

                // Set a dummy destination end point or the RTP session will end up sending RTCP reports
                // to itself.
                var dummyIPEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
                listener.SetDestination(SDPMediaTypesEnum.video, dummyIPEndPoint, dummyIPEndPoint);

                await listener.Start();               
            }           
        }

        private static string GetCodecName()
        {
            var sdp = SDP.ParseSDPDescription(File.ReadAllText(FFMPEG_SDP_FILE));
            var videoAnn = sdp.Media.Single(x => x.Media == SDPMediaTypesEnum.video);
            var codec = videoAnn.MediaFormats.Values.First().Name().ToLower();
            if (codec == "h265")
            {
                return "libx265";
            }
            else
            {
                return codec;
            }
        }
    }
}
