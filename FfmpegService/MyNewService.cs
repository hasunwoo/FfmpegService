using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace FfmpegService
{

    internal class Constants
    {
        //ffmpeg 경로
        //ffmpeg.exe 경로로 설정해줘야함
        public const string FFMPEG_PATH = @"C:\ffmpeg경로\ffmpeg.exe";
    }

    internal class MyNewService : ServiceBase
    {
        //시작할 ffmpeg instance 정보
        private List<FfmpegProcessOptions> _instanceOptions;

        //돌아가고있는 프로세스들
        private List<FfmpegProcess> _runningProcesses;

        public MyNewService(List<FfmpegProcessOptions> instanceOptions) {
            _instanceOptions = instanceOptions;
            _runningProcesses = new List<FfmpegProcess>();
        }

        //프로세스들 시작
        private void startProcesses()
        {
            lock(_runningProcesses)
            {
                foreach(var opt in _instanceOptions)
                {
                    FfmpegProcess process = new FfmpegProcess(opt);
                    process.Start();
                    _runningProcesses.Add(process);
                }
            }
        }

        //프로세스를 종료
        private void stopProcesses()
        {
            lock(_runningProcesses)
            {
                foreach(var process in _runningProcesses)
                {
                    if(process.IsRunning())
                    {
                        process.Stop();
                    }
                }
                _runningProcesses.Clear();
            }
        }

        protected override void OnStart(string[] args)
        {
            startProcesses();
        }

        protected override void OnStop()
        {
            stopProcesses();
        }
    }

    internal class FfmpegProcessOptions
    {
        private List<string> _options;

        public FfmpegProcessOptions(List<string> options)
        {
            _options = options;
        }

        public FfmpegProcessOptions(string inputAddress, string outputAddress)
        {
            //기본 옵션
            _options = new List<string>
            {
                "-fflags", "nobuffer",
                "-i", $"\"{inputAddress}\"",
                "-c:v", "copy",
                "-f", "mpegts",
                "-copytb", "0",
                "-tune", "zerolatency",
                "-probesize", "1M",
                $"\"{outputAddress}?pkt_size=1316\"",
            };
        }

        public string ToParameterString()
        {
            return _options.Aggregate((a, b) => a + " " + b);
        }
    }

    internal class FfmpegProcess
    {
        private readonly FfmpegProcessOptions _options;
        private Process _process;

        public FfmpegProcess(FfmpegProcessOptions options)
        {
            _options = options;
            _process = null;
        }

        public void Start()
        {
            if(_process != null)
            {
                throw new InvalidOperationException("Start(): Process is already running.");
            }
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Constants.FFMPEG_PATH,
                Arguments = _options.ToParameterString(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _process = new Process();
            _process.StartInfo = startInfo;
            var result = _process.Start();

             _process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Console.WriteLine(args.Data);
                }
            };

             _process.ErrorDataReceived+= (sender, args) =>
            {
                if (args.Data != null)
                {
                    Console.WriteLine(args.Data);
                }
            };

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public bool IsRunning()
        {
            return _process != null && !_process.HasExited;
        }

        public void WaitForExit()
        {
            if(!IsRunning())
            {
                throw new InvalidOperationException("WaitForExit(): Process is not running.");
            }
            _process.WaitForExit();
        }

        public void Stop()
        {
            if(!IsRunning())
            {
                throw new InvalidOperationException("Stop(): Process is already dead.");
            }
            _process.Kill();
            _process.WaitForExit();
            _process = null;
    }
    }
}