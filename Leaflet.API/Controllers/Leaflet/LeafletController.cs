using Leaflet.API.Filters;
using Leaflet.API.MinIO;
using Leaflet.API.MongoDBServices.Leaflet;
using Leaflet.API.MongoDBServices.UserCard;
using Leaflet.API.Protos.BriefUserInfo;
using Leaflet.API.Redis;
using Leaflet.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Leaflet.API.Controllers.Leaflet
{
    public class PostLeafletRequestData
    {
        public PostLeafletRequestData()
        {
        }

        public PostLeafletRequestData(string title, string description, Dictionary<string, string> labels, List<string> tags, List<PostMediaMetadata> medias, string channel, DateTime deadline)
        {
            Title = title;
            Description = description;
            Labels = labels;
            Tags = tags;
            Medias = medias;
            Channel = channel;
            Deadline = deadline;
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string>? Labels { get; set; }
        public List<string>? Tags { get; set; }
        public List<PostMediaMetadata>? Medias { get; set; }
        public string Channel { get; set; }
        public DateTime Deadline { get; set; }
    }

    public class PostLeafletResponseData
    {
        public PostLeafletResponseData(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
    }

    public class LeafletDataForClient 
    {
        public LeafletDataForClient(string id, ReusableClass.BriefUserInfo poster, string title, string description, Dictionary<string, string> labels, List<string> tags, List<MediaMetadata> medias, string channel, DateTime createdTime, DateTime deadline, bool isDeleted)
        {
            Id = id;
            Poster = poster;
            Title = title;
            Description = description;
            Labels = labels;
            Tags = tags;
            Medias = medias;
            Channel = channel;
            CreatedTime = createdTime;
            Deadline = deadline;
            IsDeleted = isDeleted;
        }

        public LeafletDataForClient(Models.Leaflet.Leaflet leaflet,ReusableClass.BriefUserInfo briefUserInfo) 
        {
            Id = leaflet.Id!;
            Poster = briefUserInfo;
            Title = leaflet.Title;
            Description = leaflet.Description;
            Labels = leaflet.Labels;
            Tags = leaflet.Tags;
            Medias = leaflet.Medias;
            Channel = leaflet.Channel;
            CreatedTime = leaflet.CreatedTime;
            Deadline = leaflet.Deadline;
            IsDeleted = leaflet.IsDeleted;
        }

        public string Id { get; set; }
        public ReusableClass.BriefUserInfo Poster { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Labels { get; set; }
        public List<string> Tags { get; set; }
        public List<MediaMetadata> Medias { get; set; }
        public string Channel { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime Deadline { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class GetLeafletsResponseData 
    {
        public GetLeafletsResponseData(List<LeafletDataForClient> dataList)
        {
            DataList = dataList;
        }

        public List<LeafletDataForClient> DataList { get; set; }
    }

    public class GetLeaflefDetailsResponseData 
    {
        public GetLeaflefDetailsResponseData(LeafletDataForClient leaflet, UserCardDataForClient userCard)
        {
            Leaflet = leaflet;
            UserCard = userCard;
        }

        public LeafletDataForClient Leaflet { get; set; }
        public UserCardDataForClient UserCard { get; set; }
    }

    public class SearchLeafletsRequestData
    {
        public SearchLeafletsRequestData(string searchKey, DateTime baseTime, int offset, string channel)
        {
            SearchKey = searchKey;
            BaseTime = baseTime;
            Offset = offset;
            Channel = channel;
        }

        public string SearchKey { get; set; }
        public DateTime BaseTime { get; set; }
        public int Offset { get; set; }
        public string Channel { get; set; }
    }

    [ApiController]
    [Route("/leaflet")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class LeafletController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly ILogger<LeafletController> _logger;
        private readonly LeafletService _leafletService;
        private readonly UserCardService _userCardService;
        private readonly RedisConnection _redisConnection;
        private readonly LeafletMediasMinIOService _leafletMediasMinIOService;
        private readonly GetBriefUserInfo.GetBriefUserInfoClient _rpcUserClient;

        public LeafletController(IConfiguration configuration, ILogger<LeafletController> logger, LeafletService leafletService, UserCardService userCardService, RedisConnection redisConnection, LeafletMediasMinIOService leafletMediasMinIOService, GetBriefUserInfo.GetBriefUserInfoClient rpcUserClient)
        {
            _configuration = configuration;
            _logger = logger;
            _leafletService = leafletService;
            _userCardService = userCardService;
            _redisConnection = redisConnection;
            _leafletMediasMinIOService = leafletMediasMinIOService;
            _rpcUserClient = rpcUserClient;
        }

        private readonly static List<string> channelList = new() {
            "全部",
            "生活",
            "活动",
            "学习",
            "运动",
            "游戏",
            "旅游",
            "其它",
        };

        [HttpGet("channelList")]
        public IActionResult GetChannelList([FromHeader] string JWT, [FromHeader] int UUID)
        {
            return Ok(new ResponseT<List<string>>(0, "获取成功", channelList));
        }

        [HttpPost]
        public async Task<IActionResult> PostLeaflet([FromForm] PostLeafletRequestData formData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            //有哪些限制条件？
            //1、最多上传9张图片
            //2、截止时间需要在合理范围内
            //3、标题和内容不能为空
            //4、传入的Labels和Tags的内容不能为空（好像也不是啥大问题）
            formData.Labels ??= new();
            formData.Tags ??= new();
            formData.Medias ??= new();

            if (IsStringEmpty(formData.Title) || IsStringEmpty(formData.Description))
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，原因为标题或描述为空。", UUID);
                ResponseT<string> postLeafletFailed = new(2, "发布失败，标题或描述不允许为空");
                return Ok(postLeafletFailed);
            }

            TimeSpan timeDifference = formData.Deadline.ToUniversalTime().Subtract(DateTime.UtcNow);

            if (timeDifference.TotalMinutes <= 30 || timeDifference.TotalDays > 181)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，原因为选择的截止时间[ {deadline} ]距现在过短或过长。", UUID,formData.Deadline);
                ResponseT<string> postLeafletFailed = new(3, "发布失败，选择的截止时间距现在过短或过长");
                return Ok(postLeafletFailed);
            }

            if (formData.Medias.Count > 9)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，原因为用户上传了超过限制数量的文件，疑似正绕过前端进行操作。", UUID);
                ResponseT<string> postLeafletFailed = new(4, "发布失败，上传文件数超过限制");
                return Ok(postLeafletFailed);
            }

            for (int i = 0; i < formData.Medias.Count; i++)
            {
                if (!formData.Medias[i].File.ContentType.Contains("image"))
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，原因为用户上传了图片以外的媒体文件，疑似正绕过前端进行操作。", UUID);
                    ResponseT<string> postLeafletFailed = new(5, "发布失败，禁止上传规定格式以外的文件");
                    return Ok(postLeafletFailed);
                }
            }

            List<Task<bool>> tasks = new();
            List<MediaMetadata> medias = new();
            List<string> paths = new();
            for (int i = 0; i < formData.Medias.Count; i++)
            {
                if (formData.Medias[i].File.ContentType.Contains("image"))
                {
                    IFormFile file = formData.Medias[i].File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    string timestamp = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = timestamp + extension;

                    paths.Add(fileName);

                    string url = _configuration["MinIO:LeafletMediasURLPrefix"]! + fileName;

                    tasks.Add(_leafletMediasMinIOService.UploadImageAsync(fileName, stream));

                    medias.Add(new MediaMetadata(url, formData.Medias[i].AspectRatio));
                }
            }

            Task.WaitAll(tasks.ToArray());
            bool isStoreMediasSucceed = true;
            foreach (var task in tasks)
            {
                if (!task.Result)
                {
                    isStoreMediasSucceed = false;
                    break;
                }
            }
            if (!isStoreMediasSucceed)
            {
                _ = _leafletMediasMinIOService.DeleteFilesAsync(paths);
                _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，MinIO存储媒体文件时发生错误。", UUID);
                ResponseT<string> postLeafletFailed = new(6, "发生错误，发布失败");
                return Ok(postLeafletFailed);
            }

            Models.Leaflet.Leaflet leaflet = new(null, UUID, formData.Title, formData.Description, formData.Labels, formData.Tags, medias, formData.Channel, DateTime.Now, formData.Deadline,false);
            try
            {
                await _leafletService.CreateAsync(leaflet);
            }
            catch (Exception ex)
            {
                _ = _leafletMediasMinIOService.DeleteFilesAsync(paths);
                _logger.LogWarning("Warning：用户[ {UUID} ]发布Leaflet时失败，将数据[ {leaflet} ]存入数据库时发生错误，报错信息为[ {ex} ]。", UUID,leaflet,ex);
                ResponseT<string> postLeafletFailed = new(7, "发生错误，发布失败");
                return Ok(postLeafletFailed);
            }

            ResponseT<PostLeafletResponseData> postLeafletSucceed = new(0, "发布成功", new PostLeafletResponseData(leaflet.Id!));
            return Ok(postLeafletSucceed);
        }

        [HttpGet("{baseTime}&{offset}&{channel}")]
        public IActionResult GetLeafletsByOffset([FromRoute] DateTime baseTime, [FromRoute] int offset, [FromRoute] string channel, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            List<Models.Leaflet.Leaflet> leaflets = _leafletService.GetLeafletsByOffset(baseTime,offset,channel);

            GetLeafletsResponseData getLeafletsResponseData = new(AssembleLeafletData(leaflets));
            ResponseT<GetLeafletsResponseData> getLeafletsSucceed = new(0, "获取成功", getLeafletsResponseData);
            return Ok(getLeafletsSucceed);
        }

        [HttpGet("me/history/{lastDateTime?}/{lastId?}")]
        public async Task<IActionResult> GetMyLeaflets([FromRoute] DateTime? lastDateTime, [FromRoute] string? lastId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            List<Models.Leaflet.Leaflet> leaflets = await _leafletService.GetMyLeafletsByLastResultAsync(UUID,lastDateTime,lastId);

            GetLeafletsResponseData getLeafletsResponseData = new(AssembleLeafletData(leaflets));
            ResponseT<GetLeafletsResponseData> getLeafletsSucceed = new(0, "获取成功", getLeafletsResponseData);
            return Ok(getLeafletsSucceed);
        }

        [HttpGet("details/{leafletId}")]
        public async Task<IActionResult> GetLeafletDetails([FromRoute] string leafletId, [FromHeader] string JWT, [FromHeader] int UUID) 
        {
            var leaflet = await _leafletService.GetLeafletByIdAsync(leafletId);

            if (leaflet == null) 
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在查询不存在的Leaflet[ {leafletId} ]的信息。", UUID, leafletId);
                ResponseT<string> getLeafletFailed = new(2, "您正在对一个不存在的搭搭请求进行查询");
                return Ok(getLeafletFailed);
            }

            if (leaflet.Deadline.ToUniversalTime().CompareTo(DateTime.UtcNow) <= 0 || leaflet.IsDeleted) 
            {
                ResponseT<string> getLeafletFailed = new(3, "您正在对一个已失效的搭搭请求进行查询");
                return Ok(getLeafletFailed);
            }

            var briefUserInfo = await GetBriefUserInfoAsync(leaflet.UUID);

            var userCard = await _userCardService.GetUserCardByUUIDAsync(leaflet.UUID);

            UserCardDataForClient userCardDataForClient;
            if (userCard == null)
            {
                userCardDataForClient = new(briefUserInfo,null, new(_configuration["DefaultBackgroundImageUrl"]!, 3.375 / 2.125));
            }
            else 
            {
                userCardDataForClient = new(briefUserInfo,userCard.Summary,userCard.BackgroundImage?? new(_configuration["DefaultBackgroundImageUrl"]!, 3.375 / 2.125));
            }

            ResponseT<GetLeaflefDetailsResponseData> getLeafletSucceed = new(0, "获取成功",new(new(leaflet,briefUserInfo),userCardDataForClient));
            return Ok(getLeafletSucceed);
        }

        [HttpDelete("{leafletId}")]
        public async Task<IActionResult> DeleteLeafletById([FromRoute] string leafletId, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            Models.Leaflet.Leaflet? leaflet = await _leafletService.GetLeafletByIdAsync(leafletId);

            if (leaflet == null || leaflet.IsDeleted)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在删除不存在或已被删除的Leaflet[ {leafletId} ]。", UUID, leafletId);
                ResponseT<string> deleteLeafletFailed = new(2, "您正在对一个不存在或已被删除的搭搭请求进行删除");
                return Ok(deleteLeafletFailed);
            }

            if (leaflet.UUID != UUID)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]正在删除一个不属于该用户的Leaflet[ {leafletId} ]。", UUID, leafletId);
                ResponseT<string> deleteLeafletFailed = new(3, "您正在对一个不属于您的搭搭请求进行删除");
                return Ok(deleteLeafletFailed);
            }

            if (leaflet.Medias.Count != 0)
            {
                List<string> paths = new();
                foreach (var medias in leaflet.Medias)
                {
                    paths.Add(medias.URL.Replace(_configuration["MinIO:LeafletMediasURLPrefix"]!, ""));
                }
                _ = _leafletMediasMinIOService.DeleteFilesAsync(paths);
            }

            _ = _leafletService.DeleteAsync(leafletId);

            ResponseT<bool> deleteLeafletSucceed = new(0, "删除成功", true);
            return Ok(deleteLeafletSucceed);
        }

        [HttpPost("search")]
        public IActionResult SearchLeaflets([FromBody] SearchLeafletsRequestData searchRequestData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            var searchKeys = Regex.Split(searchRequestData.SearchKey, " +").ToList();
            searchKeys.RemoveAll(key => key == "");

            if (searchKeys.Count == 0)
            {
                _logger.LogWarning("Warning：用户[ {UUID} ]在查询Leaflets时传递了不合法的参数[ {searchRequestData} ]，疑似正绕过前端进行操作。", UUID, searchRequestData);
                ResponseT<string> searchLeafletsFailed = new(2, "查询失败，查询关键词不可为空");
                return Ok(searchLeafletsFailed);
            }
            else
            {
                List<Models.Leaflet.Leaflet> leaflets = _leafletService.Search(searchKeys,searchRequestData.BaseTime,searchRequestData.Offset,searchRequestData.Channel);

                GetLeafletsResponseData getLeafletsResponseData = new(AssembleLeafletData(leaflets));
                ResponseT<GetLeafletsResponseData> getLeafletsSucceed = new(0, "查询成功", getLeafletsResponseData);
                return Ok(getLeafletsSucceed);
            }
        }

        private List<LeafletDataForClient> AssembleLeafletData(List<Models.Leaflet.Leaflet> leaflets) 
        {
            if (leaflets.Count == 0) 
            {
                return new();
            }

            IDatabase briefUserInfoRedis = _redisConnection.GetBriefUserInfoDatabase();
            var briefUserInfoBatch = briefUserInfoRedis.CreateBatch();
            Dictionary<int, Task<RedisValue>> briefUserInfoDictionary = new();

            foreach (var leaflet in leaflets)
            {
                if (!briefUserInfoDictionary.ContainsKey(leaflet.UUID))
                {
                    briefUserInfoDictionary.Add(leaflet.UUID, briefUserInfoBatch.StringGetAsync(leaflet.UUID.ToString()));
                }
            }
            briefUserInfoBatch.Execute();
            briefUserInfoBatch.WaitAll(briefUserInfoDictionary.Values.ToArray());

            GetBriefUserInfoMapRequest request = new();
            foreach (var leaflet in leaflets)
            {
                if (briefUserInfoDictionary[leaflet.UUID].Result == RedisValue.Null)
                {
                    request.QueryList.Add(leaflet.UUID);
                }
            }

            Dictionary<int, ReusableClass.BriefUserInfo> briefUserInfoMap = new();
            if (request.QueryList.Count != 0)
            {
                GetBriefUserInfoMapReply reply = _rpcUserClient.GetBriefUserInfoMap(request);
                var briefUserInfoCacheBatch = briefUserInfoRedis.CreateBatch();

                foreach (KeyValuePair<int, Protos.BriefUserInfo.BriefUserInfo> entry in reply.BriefUserInfoMap)
                {
                    _ = briefUserInfoCacheBatch.StringSetAsync(entry.Key.ToString(), JsonSerializer.Serialize(new ReusableClass.BriefUserInfo(entry.Value)), TimeSpan.FromMinutes(15));
                    briefUserInfoMap.Add(entry.Key, new ReusableClass.BriefUserInfo(entry.Value));
                }

                briefUserInfoCacheBatch.Execute();
            }

            foreach (var entry in briefUserInfoDictionary)
            {
                if (entry.Value.Result != RedisValue.Null)
                {
                    briefUserInfoMap.Add(entry.Key, JsonSerializer.Deserialize<ReusableClass.BriefUserInfo>(entry.Value.Result.ToString())!);
                }
            }

            List<LeafletDataForClient> dataList = new();
            for (int i = 0; i < leaflets.Count; i++) 
            {
                dataList.Add(new(leaflets[i], briefUserInfoMap[leaflets[i].UUID]));
            }

            return dataList;
        }

        private async Task<ReusableClass.BriefUserInfo> GetBriefUserInfoAsync(int UUID) 
        {
            IDatabase briefUserInfoRedis = _redisConnection.GetBriefUserInfoDatabase();

            var briefUserInfoCache = await briefUserInfoRedis.StringGetAsync(UUID.ToString());

            if (briefUserInfoCache.IsNull)
            {
                GetBriefUserInfoSingleRequest request = new() 
                {
                    UUID = UUID,
                };
                var reply = _rpcUserClient.GetBriefUserInfoSingle(request);
                _ = briefUserInfoRedis.StringSetAsync(UUID.ToString(), JsonSerializer.Serialize(new ReusableClass.BriefUserInfo(reply.BriefUserInfo)), TimeSpan.FromMinutes(15));
                return new(reply.BriefUserInfo);
            }
            else 
            {
                return JsonSerializer.Deserialize<ReusableClass.BriefUserInfo>(briefUserInfoCache.ToString())!;
            }
        }

        private static bool IsStringEmpty(string s)
        {
            var keys = Regex.Split(s, " +").ToList();
            keys.RemoveAll(key => key == "");

            return keys.Count == 0;
        }
    }
}
