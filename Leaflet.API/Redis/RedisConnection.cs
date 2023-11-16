using StackExchange.Redis;

namespace Leaflet.API.Redis
{
    public class RedisConnection
    {
        private readonly IConfiguration _configuration;
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisConnection(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionMultiplexer = ConnectionMultiplexer.Connect(_configuration.GetConnectionString("Redis")!);
        }

        public IDatabase GetLeafletDatabase() 
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Leaflet"]!));
        }

        public IDatabase GetBriefUserInfoDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:BriefUserInfo"]!));
        }

        public IDatabase GetChatRequestDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:ChatRequest"]!));
        }
    }
}
