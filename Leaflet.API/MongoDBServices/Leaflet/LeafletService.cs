using Leaflet.API.DataCollection.Leaflet;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Leaflet.API.MongoDBServices.Leaflet
{
    public class LeafletService
    {
        private readonly IMongoCollection<Models.Leaflet.Leaflet> _leafletCollection;
        private readonly IMongoCollection<BsonDocument> _bsonDocumentsCollection;

        public LeafletService(IOptions<LeafletCollectionSettings> leafletCollectionSettings)
        {
            var mongoClient = new MongoClient(
                leafletCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                leafletCollectionSettings.Value.DatabaseName);

            _leafletCollection = mongoDatabase.GetCollection<Models.Leaflet.Leaflet>(
                leafletCollectionSettings.Value.LeafletCollectionName);

            _bsonDocumentsCollection = mongoDatabase.GetCollection<BsonDocument>(
                leafletCollectionSettings.Value.LeafletCollectionName);
        }

        public async Task CreateAsync(Models.Leaflet.Leaflet leaflet)
        {
            await _leafletCollection.InsertOneAsync(leaflet);
        }

        public List<Models.Leaflet.Leaflet> GetLeafletsByOffset(DateTime baseTime, int offset, string channel)
        {
            //MongoDB涉及到DateTime需要一律使用UTC进行操作（因为MongoDB默认采用UTC存储时间）
            baseTime = baseTime.ToUniversalTime();

            return _leafletCollection
                .Find(leaflet => leaflet.CreatedTime.CompareTo(baseTime) <= 0 && leaflet.Deadline.CompareTo(baseTime) >= 0 && (channel == "全部" || leaflet.Channel == channel) && !leaflet.IsDeleted)
                .ToList()
                .OrderByDescending(leaflet => (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds) * (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds) - (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds))
                .Skip(offset)
                .Take(20)
                .ToList();
        }

        public async Task<List<Models.Leaflet.Leaflet>> GetMyLeafletsByLastResultAsync(int uuid, DateTime? lastDateTime, string? lastId)
        {
            //MongoDB涉及到DateTime需要一律使用UTC进行操作（因为MongoDB默认采用UTC存储时间）
            if (lastDateTime == null)
            {
                lastDateTime = DateTime.UtcNow;
            }

            return await _leafletCollection
                .Find(leaflet => leaflet.UUID == uuid && leaflet.CreatedTime.CompareTo(lastDateTime) <= 0 && !leaflet.IsDeleted && leaflet.Id != lastId)
                .SortByDescending(leaflet => leaflet.CreatedTime)
                .Limit(20)
                .ToListAsync();
        }

        public async Task<Models.Leaflet.Leaflet?> GetLeafletByIdAsync(string id)
        {
            return await _leafletCollection.Find(leaflet => leaflet.Id == id).FirstOrDefaultAsync();
        }

        public async Task DeleteAsync(string id)
        {
            var update = Builders<Models.Leaflet.Leaflet>.Update
                .Set(leaflet => leaflet.IsDeleted, true);

            await _leafletCollection.UpdateOneAsync(leaflet => leaflet.Id == id, update);
        }

        public List<Models.Leaflet.Leaflet> Search(List<string> searchKeys, DateTime baseTime, int offset,string channel)
        {
            //MongoDB涉及到DateTime需要一律使用UTC进行操作（因为MongoDB默认采用UTC存储时间）
            baseTime = baseTime.ToUniversalTime();

            var builder = Builders<Models.Leaflet.Leaflet>.Filter;

            FilterDefinition<Models.Leaflet.Leaflet> notDeletedFilter = builder.Where(leaflet => !leaflet.IsDeleted);

            FilterDefinition<Models.Leaflet.Leaflet> dateTimeFilter = builder.Where(leaflet => leaflet.CreatedTime.CompareTo(baseTime) <= 0 && leaflet.Deadline.CompareTo(baseTime) >= 0);

            FilterDefinition<Models.Leaflet.Leaflet> channelFilter = builder.Where(leaflet => channel == "全部" || leaflet.Channel == channel);

            FilterDefinition<Models.Leaflet.Leaflet> keyFilter;

            string firstKey = searchKeys[0];
            if (firstKey.StartsWith("#"))
            {
                keyFilter = builder.Where(leaflet => leaflet.Tags.Contains(firstKey.Remove(0, 1)));
            }
            else
            {
                keyFilter = builder.Regex(leaflet => leaflet.Title, firstKey);
                keyFilter |= builder.Regex(leaflet => leaflet.Description, firstKey);
            }
            searchKeys.RemoveAt(0);
            foreach (var key in searchKeys)
            {
                if (key.StartsWith("#"))
                {
                    keyFilter |= builder.Where(leaflet => leaflet.Tags.Contains(key.Remove(0, 1)));
                }
                else
                {
                    keyFilter |= builder.Regex(leaflet => leaflet.Title, key);
                    keyFilter |= builder.Regex(leaflet => leaflet.Description, key);
                }
            }

            return _leafletCollection
                .Find(notDeletedFilter & dateTimeFilter & channelFilter & keyFilter)
                .ToList()
                .OrderByDescending(leaflet => (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds) * (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds) - (baseTime.Subtract(leaflet.CreatedTime).TotalMicroseconds / leaflet.Deadline.Subtract(leaflet.CreatedTime).TotalMicroseconds))
                .Skip(offset)
                .Take(20)
                .ToList();
        }
    }
}
