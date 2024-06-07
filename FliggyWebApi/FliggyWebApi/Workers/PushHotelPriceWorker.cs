using FliggyWebApi.Impl;
using Serilog;
using System.Diagnostics;

namespace FliggyWebApi.Workers
{
    public class PushHotelPriceWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 获取token
        /// </summary>
        /// <param name="serviceProvider"></param>
        public PushHotelPriceWorker(IServiceProvider serviceProvider)
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
                    //var result = await _domain.SyncPlanAndStock();

                    await Task.Delay(1 * 60 * 1000, stoppingToken);
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