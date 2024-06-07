using CommonCore.EntityFramework.Common;
using CommonCore.Mapper;
using FliggyWebApi.Config;
using FliggyWebApi.Db;
using FliggyWebApi.Entity;
using FliggyWebApi.Impl;
using FliggyWebApi.Util;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System;
using WL.Utilities;
using Xiwan.Sdk;
using Xiwan.Shared;
using HttpHelper = FliggyWebApi.Util.HttpHelper;
using Topsdk;
using Log = Serilog.Log;
using System.Net.Sockets;
using Topsdk.Top;
using Topsdk.Top.Ability347;
using Topsdk.Top.Ability347.Request;
using Topsdk.Top.Ability304.Request;
using Topsdk.Top.Ability304;
using FliggyWebApi.Dto;
using Newtonsoft.Json.Linq;
using Qunar.Airtickets.Supplier.Concat.Dtos.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.VisualBasic.FileIO;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;
using System.Collections.Generic;
using System.Xml.Linq;
using Topsdk.Top.Ability347.Domain;
using System.Numerics;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using WL.ABC;

namespace FliggyWebApi.Service
{
    public class FliggyDomain : IFliggyDomain
    {
        private readonly IBaseRepository<FliggyDbContext> _repository;
        private readonly IConfiguration _configuration;
        private readonly AppSetting _appsetting;
        private readonly IMapper _mapper;
        private readonly IServiceProvider _serviceProvider;
        private readonly XiwanApiSetting _setting;

        private static List<SyncHotel> taskHotel;
        private static List<string> errorHotel;
        private static object errorHotelLocker = new object();
        private static DateTime lastInserTime;
        private static DateTime lastErrorTime;
        private ITopApiClient client;
        private Ability347 apiPackage;

        public FliggyDomain(IConfiguration configuration, IOptions<XiwanApiSetting> options, IOptions<AppSetting> useroptions,
            IBaseRepository<FliggyDbContext> repository, IMapper mapper, IServiceProvider serviceProvider)
        {
            _setting = options.Value;
            _configuration = configuration;
            _repository = repository;
            _mapper = mapper;
            _serviceProvider = serviceProvider;
            _appsetting = useroptions.Value;
            client = new DefaultTopApiClient(_appsetting.PushSetting.appkey, _appsetting.PushSetting.secret, _appsetting.PushSetting.pushurl, 10000, 20000);
            apiPackage = new Ability347(client);
        }

        #region 推送酒店

        public async Task GetUserSession()
        {
            var token = InitConfig.Get_Token();
            if (token == null)
            {
                // 1. 发起认证请求，获取授权码
                var authorizationEndpoint = "https://oauth.taobao.com/authorize";
                var clientId = _appsetting.PushSetting.appkey;
                var redirectUri = _appsetting.PushSetting.redirectUri;
                // 构造授权请求URL
                string authorizationUrl = $"https://oauth.taobao.com/authorize?response_type=code&client_id={clientId}&redirect_uri={redirectUri}";

                // todo 需要用户授权
                var code = "";
                //根据code获取授权
                await GetToken(code);
            }
        }

        private async Task GetToken(string code)
        {
            // create Client
            ITopApiClient client = new DefaultTopApiClient(_appsetting.PushSetting.appkey, _appsetting.PushSetting.secret, _appsetting.PushSetting.pushurl, 10000, 20000);
            Ability304 apiPackage = new Ability304(client);

            // create request
            var request = new TaobaoTopAuthTokenCreateRequest();
            request.Code = code;
            request.Uuid = ""; // "abc";

            var response = apiPackage.TaobaoTopAuthTokenCreate(request);
            if (response.isSuccess())
            {
                var tokenModel = JsonConvert.DeserializeObject<TaoBaoToken>(response.TokenResult);
                InitConfig.SetTokenList(tokenModel);
            }
            else
            {
                Log.Error($"获取token失败{response.SubCode}");
            }
        }

        private async Task RefreshToken()
        {
            var token = InitConfig.Get_Token();
            if (token == null)
            {
                Log.Error($"刷新token时候找不到token");
            }
            // create Client
            ITopApiClient client = new DefaultTopApiClient(_appsetting.PushSetting.appkey, _appsetting.PushSetting.secret, _appsetting.PushSetting.pushurl, 10000, 20000);
            Ability304 apiPackage = new Ability304(client);

            // create request
            var request = new TaobaoTopAuthTokenRefreshRequest();
            request.RefreshToken = token.refresh_token;

            var response = apiPackage.TaobaoTopAuthTokenRefresh(request);
            if (response.isSuccess())
            {
                var tokenModel = JsonConvert.DeserializeObject<TaoBaoToken>(response.TokenResult);
                InitConfig.SetTokenList(tokenModel);
            }
            else
            {
                Log.Error($"刷新token失败{response.SubCode}");
            }
        }

        private async Task<bool> SaveHotelBaseDataFromAPI()
        {
            bool result = false;
            //有20多万条数据

            var data = await _repository.GetRepository<SyncHotel>().Query()
                .OrderBy(n => n.utime)
                //.Take(10000)
                //.Where(n => n.HotelId == "00101050")
                .ToListAsync();
            if (data.Count() > 0)
            {
                //需要删除的酒店
                var delList = data.Where(n => n.isdeleted == 1 && n.status != 1).ToList();
                var syncList = data.Where(n => n.isdeleted != 1).ToList();

                if (delList.Count > 0)
                {
                    //todo 删除
                }

                lastInserTime = DateTime.Now;
                lastErrorTime = DateTime.Now;
                taskHotel = new List<SyncHotel>();
                errorHotel = new List<string>();
                Log.Information("开始获取酒店数据！");
                int count = 0;
                var maxConcurrency = 20; // 最大并发数
                int totalRequests = data.Count; // 总请求数
                int batchSize = 1000; // 每批次请求数
                var semaphore = new SemaphoreSlim(maxConcurrency);
                //全量覆盖本地数据

                for (int i = 0; i < totalRequests; i += batchSize)
                {
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    int currentBatchSize = Math.Min(batchSize, totalRequests - i);
                    Task[] tasks = new Task[currentBatchSize];

                    for (int j = 0; j < currentBatchSize; j++)
                    {
                        int requestNumber = i + j;
                        tasks[j] = Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                await GetHotelById(data[requestNumber].HotelId);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });
                    }
                    await Task.WhenAll(tasks);
                    await SaveXHotel();
                    watch.Stop();
                    Log.Information($"task耗时【{currentBatchSize}】条数据耗时{watch.ElapsedMilliseconds}ms");
                }
                result = true;
            }
            return result;
        }

        private async Task SaveXHotel()
        {
            var ids = taskHotel.Select(n => n.HotelId);
            var dbList = _repository.GetRepository<SyncHotel>().Query().Where(n => ids.Contains(n.HotelId)).ToList();
            foreach (var model in dbList)
            {
                var exModel = taskHotel.FirstOrDefault(n => n.HotelId == model.HotelId);
                if (exModel != null)
                {
                    model.utime = DateTime.Now;
                    model.status = 1;
                    model.Name = exModel.Name;
                    model.CityCode = exModel.CityCode;
                    model.CityName = exModel.CityName;
                    model.Address = exModel.Address;
                    model.Phone = exModel.Phone;
                    model.StarRate = exModel.StarRate;
                    model.Category = exModel.Category;
                    model.BaiduLat = exModel.BaiduLat;
                    model.BaiduLon = exModel.BaiduLon;
                    model.Hid = exModel.Hid;
                    model.stockTime = exModel.stockTime;
                    model.rateplanTime = exModel.rateplanTime;
                    model.roomtypeTime = exModel.roomtypeTime;
                }
            }
            await _repository.GetRepository<SyncHotel>().BatchUpdateAsync(dbList);
            taskHotel.Clear();
        }

        private async Task GetHotelById(object hotelid)
        {
            try
            {
                var data = await GetHotelByApi(hotelid.ToString());
                if (data != null)
                {
                    bool needSave = false;
                    //同步酒店信息到渠道
                    await PushHotelToChannel(data);
                    try
                    {
                        taskHotel.Add(data);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else
                {
                    //没有获取到酒店信息，打印下来对应id
                    lock (errorHotelLocker)
                    {
                        errorHotel.Add(hotelid.ToString());
                        if (errorHotel.Count > 100 || (DateTime.Now - lastErrorTime).TotalSeconds > 60)
                        {
                            Log.Error(errorHotel.ToJson());
                            errorHotel.Clear();
                            lastErrorTime = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errorHotelLocker)
                {
                    errorHotel.Add(hotelid.ToString());
                    if (errorHotel.Count > 100 || (DateTime.Now - lastErrorTime).TotalSeconds > 60)
                    {
                        Log.Error(errorHotel.ToJson());
                        errorHotel.Clear();
                        lastErrorTime = DateTime.Now;
                    }
                }
            }
        }

        #region 获取酒店信息 推送酒店信息

        /// <summary>
        /// 供应商 获取酒店信息接口
        /// </summary>
        /// <param name="hotelid"></param>
        /// <returns></returns>
        private async Task<SyncHotel> GetHotelByApi(string hotelid)
        {
            try
            {
                var hotelUrl = _appsetting.GetHotelUrl;
                hotelUrl = $"{hotelUrl}?hotelId={hotelid}";
                var res = await HttpHelper.HttpGetAsync(hotelUrl);
                var model = JsonConvert.DeserializeObject<SyncHotel>(res);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error($"酒店【{hotelid}】序列化失败{ex.Message}");
                return null;
            }
        }

        private async Task<bool> PushHotelToChannel(SyncHotel model)
        {
            var token = await GetSettingKey();

            var request = new TaobaoXhotelUpdateRequest();

            request.Name = model.Name;
            request.OuterId = model.HotelId;
            if (!string.IsNullOrWhiteSpace(model.CityCode))
            {
                long city = 0;
                long.TryParse(model.CityCode, out city);
                request.City = city;
            }
            request.Address = model.Address;
            request.Tel = model.Phone;
            request.Star = model.StarRate;
            request.Latitude = model.BaiduLat;
            request.Longitude = model.BaiduLon;

            var response = apiPackage.TaobaoXhotelUpdate(request, token);
            if (response.isSuccess())
            {
                model.Hid = response.Xhotel.Hid;
                await GetPriceDetail(model);
                return true;
            }
            else
            {
                //Log.Error($"Hotel:同步到渠道失败{response.Msg}");
                //todel
                await GetPriceDetail(model);
                return false;
            }
        }

        #endregion 获取酒店信息 推送酒店信息

        public async Task<bool> SyncHotel()
        {
            return await SaveHotelBaseDataFromAPI();
        }

        #endregion 推送酒店

        #region 推送房态库存价格

        /// <summary>
        /// 供应商获取报价
        /// </summary>
        /// <param name="hotelId"></param>
        /// <returns></returns>
        public async Task GetPriceDetail(SyncHotel model)
        {
            var hotelId = model.HotelId;
            try
            {
                var hotelUrl = _appsetting.GetPriceUrl;
                var startTime = DateTime.Now;
                var endTime = DateTime.Now.AddMonths(_appsetting.SyncMonth);
                //var endTime = DateTime.Now.AddDays(7);
                var startDate = startTime.ToString("yyyy-MM-dd");
                var endDate = endTime.ToString("yyyy-MM-dd");
                var url = $"{hotelUrl}?key={_appsetting.SmileKey}&code={hotelId}&checkin={startDate}&checkout={endDate}&cache=1";
                var res = await HttpHelper.HttpGetAsync(url);
                await SyncRoomType(res, model);
                /*  while (startTime < endTime)
                  {
                      var startDate = startTime.ToString("yyyy-MM-dd");
                      var endDate = startTime.AddDays(1).ToString("yyyy-MM-dd");
                      var url = $"{hotelUrl}?key={_appsetting.SmileKey}&code={hotelId}&checkin={startDate}&checkout={endDate}&cache=1";
                      var res = HttpHelper.HttpGet(url);
                      await SyncRoomType(res, model);
                      startTime = startTime.AddDays(1);
                  }*/
            }
            catch (Exception ex)
            {
                Log.Error($"酒店【{hotelId}】序列化失败{ex.Message}");
            }
        }

        /// <summary>
        /// 构建床信息
        /// </summary>
        /// <param name="request"></param>
        /// <param name="bedinfo"></param>
        /// <returns></returns>
        private async Task BuildBedInfo(TaobaoXhotelRoomtypeUpdateRequest request)
        {
            var bedType = request.BedType;
            try
            {
                switch (bedType)
                {
                    case "大床1.8米":
                        request.BedSize = "1.8米";
                        request.BedInfo = "[{\"main_bed_type\":1,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"0\",\"width\":\"1.8\"}]}]";
                        break;

                    case "大床1.5米":
                        request.BedSize = "1.5米";
                        request.BedInfo = "[{\"main_bed_type\":1,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"0\",\"width\":\"1.5\"}]}]";
                        break;

                    case "大床2米":
                        request.BedSize = "2米";
                        request.BedInfo = "[{\"main_bed_type\":1,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"0\",\"width\":\"2\"}]}]";
                        break;

                    case "双床1.2米":
                    case "单人床1.2米":
                        request.BedSize = "1.2米";
                        if (bedType.Contains("双床"))
                        {
                            request.BedInfo = "[{\"main_bed_type\":2,\"sub_bed_type\":[{\"sub_bed_num\":2,\"bed_type\":\"2\",\"width\":\"1.2\"}]}]";
                        }
                        else
                        {
                            request.BedInfo = "[{\"main_bed_type\":3,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"2\",\"width\":\"1.2\"}]}]";
                        }
                        break;

                    case "双床1.1米":
                    case "单人床1.1米":
                        request.BedSize = "1.1米";
                        if (bedType.Contains("双床"))
                        {
                            request.BedInfo = "[{\"main_bed_type\":2,\"sub_bed_type\":[{\"sub_bed_num\":2,\"bed_type\":\"2\",\"width\":\"1.1\"}]}]";
                        }
                        else
                        {
                            request.BedInfo = "[{\"main_bed_type\":3,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"2\",\"width\":\"1.1\"}]}]";
                        }
                        break;

                    case "双床1.3米":
                    case "单人床1.3米":
                        request.BedSize = "1.3米";
                        if (bedType.Contains("双床"))
                        {
                            request.BedInfo = "[{\"main_bed_type\":2,\"sub_bed_type\":[{\"sub_bed_num\":2,\"bed_type\":\"2\",\"width\":\"1.3\"}]}]";
                        }
                        else
                        {
                            request.BedInfo = "[{\"main_bed_type\":3,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"2\",\"width\":\"1.3\"}]}]";
                        }
                        break;

                    case "双床1.35米":
                    case "单人床1.35米":
                        request.BedSize = "1.35米";
                        if (bedType.Contains("双床"))
                        {
                            request.BedInfo = "[{\"main_bed_type\":2,\"sub_bed_type\":[{\"sub_bed_num\":2,\"bed_type\":\"2\",\"width\":\"1.35\"}]}]";
                        }
                        else
                        {
                            request.BedInfo = "[{\"main_bed_type\":3,\"sub_bed_type\":[{\"sub_bed_num\":1,\"bed_type\":\"2\",\"width\":\"1.35\"}]}]";
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"构建床信息失败{ex.Message}");
            }
        }

        private List<string> windownType = new List<string>();

        private string BuildWindowDesc(string windowType)
        {
            switch (windowType)
            {
                case "84": //无窗
                    return new
                    {
                        windowType = 0,
                    }.ToJson();

                case "85": //部分窗
                    return new
                    {
                        windowType = 2,
                    }.ToJson();

                case "677": //有窗
                    return new
                    {
                        windowType = 1,
                    }.ToJson();

                case "897": //內窗
                    return new
                    {
                        windowType = 1,
                        windowTypeDefect = 2
                    }.ToJson();

                case "898": //天窗
                    return new
                    {
                        windowType = 1,
                        windowTypeSpecial = 2
                    }.ToJson();

                case "899": //封闭窗
                case "2269": //装饰性假窗
                    return new
                    {
                        windowType = 1,
                        windowTypeDefect = 0
                    }.ToJson();

                case "900": //飘窗
                    return new
                    {
                        windowType = 1,
                        windowTypeSpecial = 1
                    }.ToJson();

                case "2268": //落地窗
                    return new
                    {
                        windowType = 1,
                        windowTypeSpecial = 0
                    }.ToJson();

                case "2270": //窗户较小
                    return new
                    {
                        windowType = 1,
                        windowTypeSpecial = 3
                    }.ToJson();

                case "2271": //窗外有墙体或遮挡
                    return new
                    {
                        windowType = 1,
                        windowTypeDefect = 1
                    }.ToJson();

                case "2272": //部分有窗且位于走廊或过道
                    return new
                    {
                        windowType = 2,
                        windowTypeDefect = 2
                    }.ToJson();

                case "2273": //部分有窗且为天窗
                    return new
                    {
                        windowType = 2,
                        windowTypeSpecial = 2
                    }.ToJson();

                case "2274": //部分有窗且为封闭窗
                case "2277": //部分有窗且为装饰性假窗
                    return new
                    {
                        windowType = 2,
                        windowTypeDefect = 0
                    }.ToJson();

                case "2275": //部分有窗且窗户较小
                    return new
                    {
                        windowType = 2,
                        windowTypeSpecial = 3
                    }.ToJson();

                case "2276": //部分有窗且窗外有墙体或遮挡
                    return new
                    {
                        windowType = 2,
                        windowTypeDefect = 1
                    }.ToJson();

                case "2278": //部分有窗且为飘窗
                    return new
                    {
                        windowType = 2,
                        windowTypeSpecial = 1
                    }.ToJson();

                case "2279": //部分有窗且为落地窗
                    return new
                    {
                        windowType = 2,
                        windowTypeSpecial = 0
                    }.ToJson();
            }
            return null;
        }

        /// <summary>
        /// 同步房型
        /// </summary>
        /// <param name="data"></param>
        /// <param name="dbModel"></param>
        /// <returns></returns>
        public async Task SyncRoomType(string data, SyncHotel dbModel)
        {
            var token = await GetSettingKey();

            dynamic model = JsonConvert.DeserializeObject(data);

            if (model.status == 0)
            {
                dynamic hotel = model.data;
                var rooms = hotel.Rooms;
                List<object> stockList = new List<object>(); //价格库存列表
                foreach (var room in rooms)
                {
                    try
                    {
                        var request = new TaobaoXhotelRoomtypeUpdateRequest();
                        request.Rid = room.RoomId;
                        request.Name = room.Name;
                        request.MaxOccupancy = room.Capcity;
                        request.Area = room.Area;
                        request.Floor = room.Floor;

                        request.BedType = room.BedType;
                        await BuildBedInfo(request);

                        //宽带服务。A,B,C,D。分别代表： A：无宽带，B：免费宽带，C：收费宽带，D：部分收费宽带
                        request.Internet = room.Broadnet == 0 ? "A" : room.Broadnet == 1 ? "B" : room.Broadnet == 2 ? "C" : "D";

                        //request.Service = ""; //设施服务。JSON格式。 value值true有此服务，false没有。 bar：吧台，catv：有线电视，ddd：国内长途电话，idd：国际长途电话，toilet：独立卫生间，pubtoliet：公共卫生间。 如： {"bar":false,"catv":false,"ddd":false,"idd":false,"pubtoilet":false,"toilet":false}

                        request.WindowType = string.IsNullOrWhiteSpace(room.WindosType.ToString()) || room.WindosType.ToString().Contains("无窗") ? 0 : 1;

                        var wintype = room.WindowTypeId;

                        request.WindowDesc = BuildWindowDesc(wintype?.ToString());

                        //扩展信息的JSON。注：此字段的值需要ISV在接入前与淘宝沟通，且确认能解析
                        request.Extend = "";
                        //必填
                        request.OuterId = room.RoomId;
                        request.Hid = dbModel.Hid;
                        //该字段只有确定的时候，才允许填入。用于标示和淘宝房型的匹配关系。目前尚未启动该字段
                        //request.Srid = 123123;
                        //系统商，不要使用，只有申请才可用
                        //request.Vendor = "taobao";
                        //商家酒店ID(如果更新房型的时候房型不存在，会拿该code去新增房型)
                        request.HotelCode = room.RoomId;
                        var image = room.ImageUrl.ToString();
                        if (!string.IsNullOrWhiteSpace(room.ImageUrl.ToString()))
                        {
                            var img = new
                            {
                                url = room.ImageUrl,
                                ismain = true,
                            };
                            request.Pics = img.ToJson();
                        }
                        //房型状态。0:正常，-1:删除，-2:停售
                        request.Status = 0;
                        request.StandardRoomFacilities = "";
                        request.NameE = room.NameEn;

                        //request.ChildrenPolicy = ""; //todo
                        var response = apiPackage.TaobaoXhotelRoomtypeUpdate(request, token);
                        dbModel.roomtypeTime = DateTime.Now;
                        if (response.isSuccess())
                        {
                            await SyncRatePlan(response.Xroomtype, hotel, room, dbModel);
                            Log.Information($"同步房态成功");
                        }
                        else
                        {
                            await SyncRatePlan(response.Xroomtype, hotel, room, dbModel);
                            //Log.Error($"同步房态【{dbModel.HotelId}】失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"待处理异常 【{dbModel.HotelId}】同步房态失败{ex.Message}");
                    }
                    var ratePlan = room.RatePlans;

                    if (ratePlan != null)
                    {
                        foreach (var plan in ratePlan)
                        {
                            //同步价格库存
                            try
                            {
                                var NightlyRates = plan.NightlyRates;
                                if (NightlyRates != null)
                                {
                                    List<object> inventory_price = new List<object>();
                                    foreach (var price in NightlyRates)
                                    {
                                        var dateObj = new
                                        {
                                            date = price.Date,
                                            quota = price.Status == true ? 8 : 0, //默认8个库存
                                            price = price.Cost,
                                            status = price.Status == true ? 1 : 0
                                        };
                                        inventory_price.Add(dateObj);
                                    }

                                    var priceInfo = new
                                    {
                                        out_rid = plan.RoomTypeId,
                                        rateplan_code = plan?.RatePlanId,
                                        data = new
                                        {
                                            use_room_inventory = false,
                                            inventory_price = inventory_price
                                        }
                                    };
                                    stockList.Add(priceInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"同步库存价格异常{ex.Message}");
                            }
                        }
                    }
                }
                if (stockList.Count > 0)
                {
                    // create request
                    var request = new TaobaoXhotelRatesUpdateRequest();
                    request.RateInventoryPriceMap = stockList.ToJson();
                    //Log.Information($"同步库存价格数据【{dbModel.HotelId}】");
                    var response = apiPackage.TaobaoXhotelRatesUpdate(request, token);
                    if (response.isSuccess())
                    {
                        Log.Information($"同步库存价格成功");
                    }
                    else
                    {
                        //Log.Error($"同步库存价格失败{dbModel.HotelId}");
                    }
                    dbModel.stockTime = DateTime.Now;
                }
            }
        }

        public async Task<string> GetSettingKey()
        {
            if (!string.IsNullOrWhiteSpace(_appsetting.SessionKey))
            {
                return _appsetting.SessionKey;
            }
            else
            {
                var token = InitConfig.Get_Token();
                if (token == null)
                {
                    await GetUserSession();
                    token = InitConfig.Get_Token();
                    return token?.access_token;
                }
            }
            return "";
        }

        public async Task SyncRatePlan(TaobaoXhotelRoomtypeUpdateXRoomType roomType, dynamic hotel, dynamic model, SyncHotel dbModel)
        {
            var token = await GetSettingKey();
            // create Client
            var ratePlan = model.RatePlans;

            if (ratePlan != null)
            {
                foreach (var plan in ratePlan)
                {
                    //同步价格计划
                    try
                    {
                        // create request
                        var request = new TaobaoXhotelRateplanUpdateRequest();

                        request.Name = plan.RatePlanName;

                        request.MinDays = plan.MinDays;
                        request.MaxDays = plan.MaxDays;
                        request.MinAmount = plan.MinAmount;
                        request.MinAdvHours = plan.MinAdvHours;
                        request.MaxAdvHours = plan.MaxAdvHours;
                        request.StartTime = plan.StartTime;
                        request.EndTime = plan.EndTime;
                        var prepayResult = plan.PrepayResult;
                        if (plan.RatePlanId == "275966957")
                        {
                            var todel = "";
                        }
                        if (prepayResult != null)
                        {
                            var parseList = prepayResult.LadderParseList;
                            var cancelPolicy = new List<string>();
                            var percentInfo = new Dictionary<string, int>(); //按比例扣款需要汇总
                            foreach (var pay in parseList)
                            {
                                //0:不扣费；1:金额；2：比例；3：首晚房费；
                                if (pay.CutType == 0)
                                {
                                    // 假设你有一个 Unix 时间戳，以秒为单位（10位）
                                    long unixTimeStamp = pay.EndTime; // 例如：2021-07-01 00:00:00 UTC

                                    // 转换为 DateTime 对象
                                    DateTime endtime = DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).DateTime;
                                    endtime = endtime.ToLocalTime();
                                    var cancelPay = new
                                    {
                                        cancelPolicyType = 5,
                                        timeBefore = endtime.Hour == 0 ? 0 : 24 - endtime.Hour
                                    };
                                    cancelPolicy.Add(cancelPay.ToJson());
                                }
                                else if (pay.CutType == 1)
                                {
                                    Log.Error($"usertodo:存在金额退款，todo");
                                }
                                else if (pay.CutType == 2)
                                {
                                    var ss = pay.CutValue;
                                    //不可退
                                    if (pay.CutValue == 100)
                                    {
                                        if (cancelPolicy.Count > 0 || percentInfo.Count > 0)
                                        {
                                            continue;
                                        }
                                        else
                                        {
                                            //不可取消
                                            var cancelPay = new
                                            {
                                                cancelPolicyType = 2,
                                            };
                                            cancelPolicy.Add(cancelPay.ToJson());
                                            continue;
                                        }
                                    }
                                    long unixTimeStamp = pay.EndTime; // 例如：2021-07-01 00:00:00 UTC

                                    // 转换为 DateTime 对象
                                    DateTime endtime = DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).UtcDateTime;
                                    endtime = endtime.ToLocalTime();
                                    var timeberfore = endtime.Hour == 0 ? "0" : (24 - endtime.Hour).ToString();
                                    var cutValue = pay.CutValue.ToString();
                                    var q = model.ToString();
                                    if (!string.IsNullOrWhiteSpace(cutValue))
                                    {
                                        percentInfo.Add(timeberfore, Convert.ToInt32(cutValue));
                                    }
                                }
                                else if (pay.CutType == 3) //首晚
                                {
                                    long unixTimeStamp = pay.EndTime; // 例如：2021-07-01 00:00:00 UTC

                                    // 转换为 DateTime 对象
                                    DateTime endtime = DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).UtcDateTime;
                                    endtime = endtime.ToLocalTime();
                                    var timeberfore = endtime.Hour == 0 ? "0" : (24 - endtime.Hour).ToString();

                                    var cancelPay = new
                                    {
                                        cancelPolicyType = 6,
                                        policyInfo = new Dictionary<string, int>()
                                        {
                                              {timeberfore,1 }
                                        }
                                    };
                                    cancelPolicy.Add(cancelPay.ToJson());
                                }
                            }
                            if (percentInfo.Count > 0)
                            {
                                var cancelPay = new
                                {
                                    cancelPolicyType = 4,
                                    policyInfo = percentInfo
                                };
                                cancelPolicy.Add(cancelPay.ToJson());
                            }
                            request.CancelPolicy = string.Join("|", cancelPolicy);
                            //Log.Information($"cal:{dbModel.HotelId} 【{model.RoomId}】 【{plan.RatePlanId}】取消政策{request.CancelPolicy}");
                        }
                        //request.CancelPolicy = "{"cancelPolicyType":1}|{"cancelPolicyType":2}|{"cancelPolicyType":4,"policyInfo":{"48":10,"24":20}}|{"cancelPolicyType":5,"policyInfo":{"timeBefore":6}}|{"cancelPolicyType":6,"policyInfo":{"14":1}}";
                        //该产品产品、价格是否有效的状态，这里为false就表示这个产品不能卖了。如果为true，那么还需要依次判断NightlyRate数组中每个节点的状态，只有这些全部都为true，这个产品才可售 ，详见。
                        //false--不可销售（可能是产品无效、部分日期缺少价格）true--可销售
                        request.Status = 2;
                        if (plan.Status == true)
                        {
                            var nightlyRates = plan.NightlyRates;
                            if (nightlyRates == null)
                            {
                                request.Status = 2;
                            }
                            else
                            {
                                var allpass = true;
                                foreach (var rate in nightlyRates)
                                {
                                    if (rate.Status != true)
                                    {
                                        allpass = false;
                                    }
                                }
                                if (allpass)
                                {
                                    request.Status = 1;
                                }
                            }
                        }
                        //不推荐 担保
                        //if (plan.GuaranteeRuleIds!=null)
                        if (plan.GuaranteeResult != null)
                        {
                            //	担保类型，只支持： 0 无担保 1 峰时首晚担保 2峰时全额担保 3全天首晚担保 4全天全额担保
                            if (plan.GuaranteeResult.GuaranteeType == 0) //无担保
                            {
                                request.GuaranteeType = 0;
                            }
                        }
                        //会员等级
                        //request.MemberLevel = "1";
                        //渠道 销售渠道。如需开通，需要申请权限。目前支持的渠道有 H:飞猪全渠道（选择H，可实现飞猪、高德、支付宝、手淘均可售卖） O:钉钉商旅 。如果有多个用","分开，比如H,O。如果需要投放其他渠道，请联系飞猪运营或者技术支持。
                        request.Channel = "H";
                        //request.Vendor = "taobao";
                        request.RateplanCode = plan.RatePlanId;
                        // 	需申请会员权限。是否是新用户首住优惠rp。1-代表是。0-代表否。不填写代表不更新该字段。
                        //request.FirstStay = 1;
                        //价格类型字段：0.非协议价；1.集采协议价；如果不是协议价，请不要填写该字段。该字段有权限控制，如需使用，请联系阿里旅行运营。 如果不填写或者填写为0，默认是阿里旅行价
                        //request.Agreement = 2;
                        if (plan.ValueAddIds != null)
                        {
                            string valuestr = plan.ValueAddIds.ToString();
                            var valueids = valuestr.Split(',').Where(n => !String.IsNullOrWhiteSpace(n)).ToList();
                            if (valueids.Count > 0)
                            {
                                var addIds = hotel.ValueAdds;
                                // 将JSON字符串解析为JArray对象
                                List<dynamic> AddValueList = JsonConvert.DeserializeObject<List<dynamic>>(addIds.ToString());
                                var valueId = AddValueList.Where(n => valueids.Contains(n.ValueAddId.ToString())).ToList();
                                var todelss = valueId.ToJson();
                                //Log.Information($"break:{dbModel.HotelId} 【{model.RoomId}】 【{plan.RatePlanId}】附加服务{todelss}");
                                //justdo

                                foreach (var vu in valueId)
                                {
                                    if (vu.TypeCode == "01" || vu.TypeCode == "99")
                                    {
                                        //早餐服务
                                        request.BreakfastCount = vu.Amount;
                                        // Log.Information($"break:{dbModel.HotelId} 【{model.RoomId}】 【{plan.RatePlanId}】早餐{request.BreakfastCount}");
                                    }
                                    else
                                    {
                                        // Log.Information($"break:usertodo{dbModel.HotelId} 【{model.RoomId}】 【{plan.RatePlanId}】附加服务{todelss}");
                                    }
                                }
                            }
                        }
                        if (plan.Meals != null)
                        {
                            var dayMealTable = plan.Meals.dayMealTable;
                            var breakfastList = new List<object>();

                            foreach (var daymeal in dayMealTable)
                            {
                                //超过10年的会报错
                                if (string.IsNullOrWhiteSpace(daymeal.date.ToString()))
                                {
                                    continue;
                                }
                                if (Convert.ToDateTime(daymeal.date) > DateTime.Now.AddYears(-10))
                                {
                                    var breakfast = new
                                    {
                                        date = daymeal.date,
                                        breakfast_count = daymeal.breakfastShare
                                    };
                                    breakfastList.Add(breakfast);
                                }
                            }
                        }
                        if (plan.GuaranteeResult != null) //担保节点
                        {
                            Log.Error($"usertodo:存在担保节点  {dbModel.HotelId}");
                        }
                        //生效开始时间 结束时间
                        request.EffectiveTime = DateTime.Now;
                        request.DeadlineTime = DateTime.Now;

                        // all不赋值？
                        if (plan.PaymentType == "Prepay")
                        {
                            request.PaymentType = 1;
                        }
                        else if (plan.PaymentType == "SelfPay")
                        {
                            request.PaymentType = 5;
                        }
                        //根据小时房特有字段赋值
                        if (plan.earliestToliveTime != null)
                        {
                            //钟点房
                            request.RpType = "1";
                            request.Hourage = plan.stayTime;
                            request.CanCheckinEnd = plan.latestToliveTime;
                            request.CanCheckinStart = plan.earliestToliveTime;
                        }

                        //request.MaxChildAge = plan.RoomChildAge;
                        request.MinChildAge = plan.RoomChildAge;
                        //request.MaxInfantAge = 5;
                        //request.MinInfantAge = 5;
                        if (plan.Meals != null)
                        {
                            request.DinningDesc = plan.Meals.mealCopyWriting;
                        }

                        //request.IsStudent = 1;
                        request.Hid = dbModel.Hid;
                        request.Rid = roomType?.Rid;
                        request.OutRid = model?.RoomId;
                        request.OutHid = dbModel.HotelId;
                        //super rp标记，1是；0否
                        //request.SuperRpFlag = 1;
                        //	base rp标记，1是；0否
                        //request.BaseRpFlag = 1;
                        request.GuaranteeMode = 1;

                        /*   request.ParentRpCode = "0";
                           request.ParentRpid = 0;*/
                        /* request.TagJson = "{"non - direct - RP":1,"super - could - price - change - RP":0,"base - could - derived - RP":1,"ebk - tail - room - RP":0,"free - room":1}";
                         request.AllotmentReleaseTime = "0";
                         request.PackRoomFlag = "0";
                         request.BottomPriceFlag = "0";
                         request.DisplayName = "0";
                         request.Source = 0;
                         request.CommonAllotReleaseTime = "0";
                         request.CompanyAssist = 1;
                         request.HotelCompanyMappingDOS = "[]";
                         request.ResourceType = "1";
                         request.CanCheckoutEnd = plan.latestToliveTime;
                         request.MemDiscFlag = 1;
                         request.MemberDiscountCal = "[{c:"8",t:1,s: "20191211",e:"20191225"}]";
                         request.Benefits = "benefits";
                         request.ActivityType = "1";*/
                        request.GuestLimit = plan.xStayPeopleNum;
                        /*  request.OnlineBookingBindingInfo = "[{"itemId":1234235235,"skuId":1234235235,"priceRuleInfoList":[{"priceRuleNumber":"14235253"},{"priceRuleNumber":"14235253"},{"priceRuleNumber":"14235253"}]},{"itemId":1234235235,"skuId":1234235235,"priceRuleInfoList":[{"priceRuleNumber":"14235253"},{"priceRuleNumber":"14235253"},{"priceRuleNumber":"14235253"}]}]";
                          request.Rights = "[{"type": "eot", “value“: "1,2,3,4,5,6"}]";
                          request.FreeRoomChargeDstRole = "hotel";
                          request.ChildrenPricePolicy = "{"childrenPricePolicyList":[{"max":1,"min":0,"t":"1","v":"30.23"},{"max":17,"min":2,"t":"2","v":"20000"}]}";
          */
                        var response = apiPackage.TaobaoXhotelRateplanUpdate(request, token);
                        if (response.isSuccess())
                        {
                            Log.Information($"同步rateplan成功");
                        }
                        else
                        {
                            //Log.Error($"同步rateplan 【{plan.RatePlanId}】失败{response.SubCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"待处理异常rateplan【{dbModel.HotelId}】失败{ex.Message}");
                    }
                }
            }
            dbModel.rateplanTime = DateTime.Now;
        }

        #endregion 推送房态库存价格

        #region Api回调地址

        public async Task<string> Process(string body)
        {
            var xmlDoc = new XmlDocument();

            xmlDoc.LoadXml(body);
            var rootNodeName = xmlDoc.DocumentElement.Name;

            //校验用户名密码
            string username = XmlHelper.GetNodeValue(xmlDoc.DocumentElement, "AuthenticationToken/Username");
            string password = XmlHelper.GetNodeValue(xmlDoc.DocumentElement, "AuthenticationToken/Password");
            if (username != _appsetting.UserName || password != _appsetting.Password)
            {
                var err = new ValidateRS
                {
                    ResultCode = -4,
                    CurrencyCode = "",
                    InventoryPrice = "",
                    CreateOrderValidateKey = "",
                    Message = $"用户名密码错误"
                };
                return XmlHelper.SerializeToXml(err);
                //错误 usertodo
            }

            switch (rootNodeName)
            {
                //校验库存
                case "ValidateRQ":
                    var res = await ValidateRQ(xmlDoc);
                    // 将响应对象序列化为 XML
                    var xmlResponse = XmlHelper.SerializeToXml(res);
                    return xmlResponse;

                case "BookRQ":
                    return await BookRQ(xmlDoc);

                    /*  default:
                          return BadRequest("Unknown request type.");*/
            }
            return null;
        }

        /// <summary>
        /// 校验库存
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <returns></returns>
        private async Task<ValidateRS> ValidateRQ(XmlDocument xmlDoc)
        {
            var xmlSerializer = new XmlSerializer(typeof(ValidateRQ));
            ValidateRQ validateRQ;

            using (var stringReader = new StringReader(xmlDoc.OuterXml))
            {
                validateRQ = (ValidateRQ)xmlSerializer.Deserialize(stringReader);
            }

            // TODO: 处理 validateRQ 对象，进行价格库存验证逻辑
            var url = $"{_appsetting.CheckUrl}?key={_appsetting.SmileKey}&code={validateRQ.HotelId}&rid={validateRQ.RoomTypeId}&pid={validateRQ.RatePlanCode}" +
                $"&checkin={validateRQ.CheckIn}&checkout={validateRQ.CheckOut}&roomnum={validateRQ.RoomNum}&total={validateRQ.TotalPrice}";
            var res = await HttpHelper.HttpGetAsync(url);
            var result = JsonConvert.DeserializeObject<dynamic>(res);
            if (result.status == 0)
            {
                return new ValidateRS
                {
                    ResultCode = 0,
                    CurrencyCode = "",
                    InventoryPrice = "",
                    CreateOrderValidateKey = "",
                    Message = $"{result.msg}"
                };
            }
            else
            {
                if (result.msg.ToString().Contains("价格已过期"))
                {
                    return new ValidateRS
                    {
                        ResultCode = -2,
                        CurrencyCode = "",
                        InventoryPrice = "",
                        CreateOrderValidateKey = "",
                        Message = $"{result.msg}价格为{result.data}"
                    };
                }
                else
                {
                    return new ValidateRS
                    {
                        ResultCode = -4,
                        CurrencyCode = "",
                        InventoryPrice = "",
                        CreateOrderValidateKey = "",
                        Message = $"{result.msg}"
                    };
                }
            }
        }

        private async Task<string> BookRQ(XmlDocument xmlDoc)
        {
            try
            {
                var xmlSerializer = new XmlSerializer(typeof(BookRequest));
                BookRequest validateRQ;

                using (var stringReader = new StringReader(xmlDoc.OuterXml))
                {
                    validateRQ = (BookRequest)xmlSerializer.Deserialize(stringReader);
                }
            }
            catch(Exception ex)
            {
                Log.Error($"sdsad{ex.Message}");
            }

            return "";
        }
        
        #endregion Api回调地址
    }
}