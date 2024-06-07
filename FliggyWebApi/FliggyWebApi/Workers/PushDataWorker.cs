using FliggyWebApi.Impl;
using FliggyWebApi.Util;
using Serilog;
using System.Diagnostics;

namespace FliggyWebApi.Workers
{
    public class PushDataWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 获取token
        /// </summary>
        /// <param name="serviceProvider"></param>
        public PushDataWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateAsyncScope();
                var _domain = scope.ServiceProvider.GetRequiredService<IFliggyDomain>();
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    stopwatch.Start();
                    var result = await _domain.SyncHotel();

                    await Task.Delay(1 * 600 * 1000, stoppingToken);
                }
                catch (Exception ex)
                {
                    Log.Error($"EarlyWarningWorker运行出错{ex.Message}");
                    continue;
                }
            }
        }
    }
}