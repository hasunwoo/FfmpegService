using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;

namespace FfmpegService
{

    internal class Constants
    {
        //ffmpeg 경로
        //ffmpeg.exe 경로로 설정해줘야함
        public const string FFMPEG_PATH = @"C:\ffmpeg경로\ffmpeg.exe";
        //로그경로 설정해줘야함.
        public static string HEALTH_CHECK_LOG_PATH = @"C:\로그경로\health.log";
    }

    internal class MyNewService : ServiceBase
    {
        //프로세스 관리자
        private FfmpegProcessManager _manager;

        public MyNewService(List<FfmpegProcessOptions> instanceOptions, TimeSpan autoRestartTime, string healthCheckFile) {
            _manager = new FfmpegProcessManager(instanceOptions, healthCheckFile, autoRestartTime, TimeSpan.FromSeconds(1));
        }

        public void Start()
        {
            _manager.Start();
        }

        //프로세스를 재시작

        protected override void OnStart(string[] args)
        {
            _manager.Start();
        }

        protected override void OnStop()
        {
            _manager.Stop();
        }
    }

    internal class FfmpegProcessInfo
    {
        internal FfmpegProcess process;
        internal DateTime? created; 

        internal FfmpegProcessInfo(FfmpegProcess process, DateTime? created)
        {
            this.process = process;
            this.created = created;
        }
    }

    internal class FfmpegProcessManager
    {
        //시작할 ffmpeg instance 정보와 프로세스 정보
        private Dictionary<FfmpegProcessOptions, FfmpegProcessInfo> _processMap;
        private object _processMapLock = new object();

        //자동 재시작 간격
        private TimeSpan _autoRestartTime;

        //프로세스 상태 확인 간격
        private TimeSpan _healthCheckTime;

        //프로세스 상태 로그 파일
        private string _healthCheckFile;

        //타이머
        private Timer _timer;
        private Timer _healthCheckTimer;

        //실행여부
        private bool _isRunning;
        private object _timerLock = new object();

        public FfmpegProcessManager(List<FfmpegProcessOptions> instanceOptions, string healthCheckFile, TimeSpan autoRestartTime, TimeSpan healthCheckTime) {
            _processMap = instanceOptions.ToDictionary(key => key, key => new FfmpegProcessInfo(null, null));
            _healthCheckFile = healthCheckFile;
            _autoRestartTime = autoRestartTime;
            _healthCheckTime = healthCheckTime;
            _isRunning = false;
        }

        public bool IsRunning()
        {
            lock(_timerLock)
            {
                return _isRunning;
            }
        }

        public void Start()
        {
            lock(_timerLock)
            {
                if(!_isRunning)
                {
                    _timer = new Timer(TimerCallback, this, TimeSpan.Zero, _autoRestartTime);
                    _healthCheckTimer = new Timer(HealthCheckTimerCallback, this, TimeSpan.Zero, _healthCheckTime);
                    _isRunning = true;
                }
            }
        }

        public void Stop()
        {
            lock(_timerLock)
            {
                if(_isRunning)
                {
                    _healthCheckTimer.Dispose();
                    _healthCheckTimer = null;

                    _timer.Dispose();
                    _timer = null;

                    StopProcesses();
                    _isRunning = false;
                }
            }
        }

        //프로세스들 시작
        private void StartProcesses()
        {
            lock(_processMapLock)
            {
                foreach (var key in _processMap.Keys.ToList())
                {
                    var value = _processMap[key];
                    //이미 들아가는지 확인
                    if(value.process == null || !value.process.IsRunning())
                    {
                        Console.WriteLine($"StartProcesses(): {key.ToParameterString()}");
                        var newProcess = new FfmpegProcess(key);
                        newProcess.Start();
                        _processMap[key].process = newProcess;
                        _processMap[key].created= DateTime.Now;
                    }
                }
            }
        }

        //프로세스를 종료
        private void StopProcesses()
        {
            lock(_processMapLock)
            {
                foreach (var key in _processMap.Keys.ToList())
                {
                    var value = _processMap[key];
                    //이미 들아가는지 확인
                    if(value.process != null && value.process.IsRunning())
                    {
                        value.process.Stop();
                    }
                    _processMap[key].process = null;
                }
            }
        }

        private void HealthCheck()
        {
            lock(_processMapLock)
            {
                using(StreamWriter writer = new StreamWriter(_healthCheckFile))
                {
                    writer.WriteLine(DateTime.Now);
                    foreach (KeyValuePair<FfmpegProcessOptions, FfmpegProcessInfo> pair in _processMap)
                    {
                        var key = pair.Key;
                        var value = pair.Value;
                        if(value.process != null && value.process.IsRunning())
                        {
                            if(DateTime.Now - value.created > value.process.Options().Timeout() + TimeSpan.FromSeconds(2))
                            {
                                writer.WriteLine(key.ToParameterString());
                            }
                        } 
                    }
                }
            }
        }

        private static void TimerCallback(object _state)
        {
            var state = (FfmpegProcessManager)_state;
            state.StartProcesses();
        }

        private static void HealthCheckTimerCallback(object _state)
        {
            var state = (FfmpegProcessManager)_state;
            state.HealthCheck();
        }
    }

    internal class FfmpegProcessOptions
    {
        private List<string> _options;
        private TimeSpan _timeout;

        public FfmpegProcessOptions(List<string> options)
        {
            _options = options;
            _timeout = TimeSpan.Zero;
        }

        public FfmpegProcessOptions(string inputAddress, string outputAddress, TimeSpan timeout)
        {
            //기본 옵션
            _options = new List<string>
            {
                "-fflags", "nobuffer",
                "-i", $"\"{inputAddress}?timeout={timeout.TotalMilliseconds * 1000}\"",
                "-c:v", "copy",
                "-f", "mpegts",
                "-copytb", "0",
                "-tune", "zerolatency",
                "-probesize", "1M",
                $"\"{outputAddress}?pkt_size=1316\"",
            };
            _timeout = timeout;
        }

        public TimeSpan Timeout()
        {
            return _timeout;
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

        public FfmpegProcessOptions Options()
        {
            return _options;
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