using Serilog;

namespace FliggyWebApi.Dto
{
    public class InitConfig
    {
        private static readonly object _locker = new Object();
        private static InitConfig _instance = null;
        private static TaoBaoToken token;

        /// <summary>
        /// 单例
        /// </summary>
        public static InitConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_locker)
                    {
                        if (_instance == null)
                        {
                            _instance = new InitConfig();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取最新的passtime
        /// </summary>
        /// <returns></returns>
        public static TaoBaoToken Get_Token()
        {
            return token;
        }

        public static void SetTokenList(TaoBaoToken newtoken)
        {
            Log.Information($"rootbot:替换token【{newtoken.refresh_token_valid_time}】");
            token = newtoken;
        }
    }
}