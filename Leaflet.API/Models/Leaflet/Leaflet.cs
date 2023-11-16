using Leaflet.API.ReusableClass;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leaflet.API.Models.Leaflet
{
    public class Leaflet
    {
        public Leaflet(string? id, int UUID, string title, string description, Dictionary<string, string> labels, List<string> tags, List<MediaMetadata> medias, string channel, DateTime createdTime, DateTime deadline, bool isDeleted)
        {
            Id = id;
            this.UUID = UUID;
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

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public int UUID { get; set; }
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
}
