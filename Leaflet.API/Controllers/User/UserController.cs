using Leaflet.API.Filters;
using Leaflet.API.MinIO;
using Leaflet.API.MongoDBServices.UserCard;
using Leaflet.API.Protos.BriefUserInfo;
using Leaflet.API.Protos.ChatRequest;
using Leaflet.API.Redis;
using Leaflet.API.ReusableClass;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;

namespace Leaflet.API.Controllers.User
{
    public class GetUserCardResponseData 
    {
        public GetUserCardResponseData(UserCardDataForClient data)
        {
            Data = data;
        }

        public UserCardDataForClient Data { get; set; }
    }

    public class EditUserCardRequestData 
    {
        public EditUserCardRequestData()
        {
        }

        public EditUserCardRequestData(string? summary, PostMediaMetadata? backgroundImage)
        {
            Summary = summary;
            BackgroundImage = backgroundImage;
        }

        public string? Summary { get; set; }
        public PostMediaMetadata? BackgroundImage { get; set; }
    }

    public class SendChatRequestRequestData
    {
        public SendChatRequestRequestData(string title, int targetUser, string greetText)
        {
            Title = title;
            TargetUser = targetUser;
            GreetText = greetText;
        }

        public string Title { get; set; }
        public int TargetUser { get; set; }
        public string GreetText { get; set; }
    }

    [ApiController]
    [Route("/user")]
    [ServiceFilter(typeof(JWTAuthFilterService), IsReusable = true)]
    public class UserController : Controller
    {
        //依赖注入
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;
        private readonly UserCardService _userCardService;
        private readonly RedisConnection _redisConnection;
        private readonly UserCardMinIOService _userCardMinIOService;
        private readonly GetBriefUserInfo.GetBriefUserInfoClient _rpcUserClient;
        private readonly SendChatRequest.SendChatRequestClient _rpcChatRequestClient;

        public UserController(IConfiguration configuration, ILogger<UserController> logger, UserCardService userCardService, RedisConnection redisConnection, UserCardMinIOService userCardMinIOService, GetBriefUserInfo.GetBriefUserInfoClient rpcUserClient, SendChatRequest.SendChatRequestClient rpcChatRequestClient)
        {
            _configuration = configuration;
            _logger = logger;
            _userCardService = userCardService;
            _redisConnection = redisConnection;
            _userCardMinIOService = userCardMinIOService;
            _rpcUserClient = rpcUserClient;
            _rpcChatRequestClient = rpcChatRequestClient;
        }

        [HttpGet("me/userCard")]
        public async Task<IActionResult> GetUserCard([FromHeader] string JWT, [FromHeader] int UUID) 
        {
            var briefUserInfo = await GetBriefUserInfoAsync(UUID);

            var userCard = await _userCardService.GetUserCardByUUIDAsync(UUID);

            UserCardDataForClient userCardDataForClient;
            if (userCard == null)
            {
                userCardDataForClient = new(briefUserInfo, null, new(_configuration["DefaultBackgroundImageUrl"]!, 3.375 / 2.125));
            }
            else
            {
                userCardDataForClient = new(briefUserInfo, userCard.Summary, userCard.BackgroundImage ?? new(_configuration["DefaultBackgroundImageUrl"]!, 3.375 / 2.125));
            }

            ResponseT<GetUserCardResponseData> getUserCardSucceed = new(0, "获取成功", new(userCardDataForClient));
            return Ok(getUserCardSucceed);
        }

        [HttpPut("me/userCard")]
        public IActionResult EditUserCard([FromForm] EditUserCardRequestData formData,[FromHeader] string JWT, [FromHeader] int UUID) 
        {
            if (formData.BackgroundImage != null) 
            {
                if (formData.BackgroundImage.File.ContentType.Contains("image"))
                {
                    IFormFile file = formData.BackgroundImage.File;

                    string extension = Path.GetExtension(file.FileName);

                    Stream stream = file.OpenReadStream();

                    string timestamp = (DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0)).TotalMilliseconds.ToString().Replace(".", "");

                    string fileName = $"/{UUID}/" + timestamp + extension;

                    string url = _configuration["MinIO:UserCardURLPrefix"]! + fileName;

                    _ = _userCardMinIOService.UploadImageAsync(fileName, stream);

                    _ = _userCardService.EditUserCardAsync(UUID,formData.Summary,new(url,formData.BackgroundImage.AspectRatio));

                    return Ok(new ResponseT<string>(0, "修改成功"));
                }
                else 
                {
                    _logger.LogWarning("Warning：用户[ {UUID} ]修改UserCard信息时失败，原因为用户上传了图片以外的媒体文件，疑似正绕过前端进行操作。", UUID);
                    ResponseT<string> editUserCardFailed = new(2, "修改失败，禁止上传规定格式以外的文件");
                    return Ok(editUserCardFailed);
                }
            }
            _ = _userCardService.EditUserCardAsync(UUID, formData.Summary);

            return Ok(new ResponseT<string>(0, "修改成功"));
        }

        //向某人发送私聊请求
        [HttpPost("chatRequest")]
        public async Task<IActionResult> SendChatRequest([FromBody] SendChatRequestRequestData requestData, [FromHeader] string JWT, [FromHeader] int UUID)
        {
            IDatabase database = _redisConnection.GetChatRequestDatabase();

            //无法向自己发送私聊请求
            if (requestData.TargetUser == UUID) 
            {
                ResponseT<string> sendChatRequestFailed = new(2, "发送私聊请求失败，您无法向自己发送私聊请求");
                return Ok(sendChatRequestFailed);
            }

            //同一用户在六十分钟内无法受到来自相同用户的多次私聊请求
            if (database.KeyExists($"{UUID}SendChatRequestTo{requestData.TargetUser}"))
            {
                ResponseT<string> sendChatRequestFailed = new(3, "发送私聊请求失败，您向该用户发送私聊请求的操作太过频繁");
                return Ok(sendChatRequestFailed);
            }

            //发送RPC请求
            SendChatRequestSingleRequest request = new()
            {
                SenderUUID = UUID,
                TargetUUID = requestData.TargetUser,
                GreetText = $"来自《{requestData.Title}》：{requestData.GreetText}",
                MessageText = "来自搭搭的私聊请求",
            };

            GeneralReply reply = await _rpcChatRequestClient.SendChatRequestSingleAsync(
                          request);

            switch (reply.Code)
            {
                case 0:
                    {
                        _ = database.StringSetAsync($"{UUID}SendChatRequestTo{requestData.TargetUser}", "", expiry: TimeSpan.FromMinutes(60));
                        ResponseT<string> sendChatRequestSucceed = new(0, "发送私聊请求成功");
                        return Ok(sendChatRequestSucceed);
                    }
                default:
                    {
                        ResponseT<string> sendChatRequestFailed = new(4, reply.Message);
                        return Ok(sendChatRequestFailed);
                    }
            }
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
    }
}
