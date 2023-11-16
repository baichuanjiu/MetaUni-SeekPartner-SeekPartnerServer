using Leaflet.API.ReusableClass;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leaflet.API.Models.UserCard
{
    public class UserCard
    {
        public UserCard(string? id, int UUID, string? summary, MediaMetadata? backgroundImage)
        {
            Id = id;
            this.UUID = UUID;
            Summary = summary;
            BackgroundImage = backgroundImage;
        }

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public int UUID { get; set; }
        public string? Summary { get; set; }
        public MediaMetadata? BackgroundImage { get; set; }
    }
}
