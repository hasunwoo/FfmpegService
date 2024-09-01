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
                    new FfmpegProcessOptions("udp://입력아이피:입력포트", "udp://출력아이피:출력포트"),
                    new FfmpegProcessOptions("udp://입력아이피:입력포트", "udp://출력아이피:출력포트"),
                })
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
