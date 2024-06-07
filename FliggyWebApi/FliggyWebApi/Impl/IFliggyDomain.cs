using CommonCore.Dependency;

namespace FliggyWebApi.Impl
{
    public interface IFliggyDomain : IScopedDependency
    {
        Task<bool> SyncHotel();

        /// <summary>
        /// 统一处理Api回调信息
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        Task<string> Process(string body);
    }
}