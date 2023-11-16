using Leaflet.API.DataCollection.UserCard;
using Leaflet.API.ReusableClass;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leaflet.API.MongoDBServices.UserCard
{
    public class UserCardService
    {
        private readonly IMongoCollection<Models.UserCard.UserCard> _userCardCollection;
        private readonly IMongoCollection<BsonDocument> _bsonDocumentsCollection;

        public UserCardService(IOptions<UserCardCollectionSettings> userCardCollectionSettings)
        {
            var mongoClient = new MongoClient(
                userCardCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                userCardCollectionSettings.Value.DatabaseName);

            _userCardCollection = mongoDatabase.GetCollection<Models.UserCard.UserCard>(
                userCardCollectionSettings.Value.UserCardCollectionName);

            _bsonDocumentsCollection = mongoDatabase.GetCollection<BsonDocument>(
                userCardCollectionSettings.Value.UserCardCollectionName);
        }

        public async Task<Models.UserCard.UserCard?> GetUserCardByUUIDAsync(int UUID)
        {
            return await _userCardCollection.Find(card => card.UUID == UUID).FirstOrDefaultAsync();
        }

        public async Task EditUserCardAsync(int UUID, string? summary)
        {
            var update = Builders<Models.UserCard.UserCard>.Update
                .Set(card => card.Summary,summary);

            if ((await _userCardCollection.FindOneAndUpdateAsync(card => card.UUID == UUID, update)) == null) 
            {
                await _userCardCollection.InsertOneAsync(new(null,UUID,summary,null));
            }
        }

        public async Task EditUserCardAsync(int UUID, string? summary, MediaMetadata backgroundImage) 
        {
            var update = Builders<Models.UserCard.UserCard>.Update
                .Set(card => card.Summary, summary)
                .Set(card => card.BackgroundImage,backgroundImage);

            if ((await _userCardCollection.FindOneAndUpdateAsync(card => card.UUID == UUID, update)) == null) 
            {
                await _userCardCollection.InsertOneAsync(new(null, UUID, summary, backgroundImage));
            }
        }
    }
}
