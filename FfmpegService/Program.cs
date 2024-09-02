using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace FfmpegService
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        static void Main()
        {
             ServiceBase[] ServicesToRun;
             ServicesToRun = new ServiceBase[]
             {
                 new MyNewService(new List<FfmpegProcessOptions>
                 {
                     //필요한 개수만큼 사용가능
                     new FfmpegProcessOptions("udp://127.0.0.1:12211", "udp://127.0.0.1:32111", TimeSpan.FromSeconds(5)),
                     new FfmpegProcessOptions("udp://127.0.0.1:12212", "udp://127.0.0.1:32112", TimeSpan.FromSeconds(5)),
                 }, TimeSpan.FromSeconds(5), Constants.HEALTH_CHECK_LOG_PATH)
             };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
