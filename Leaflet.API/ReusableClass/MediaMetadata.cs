namespace Leaflet.API.ReusableClass
{
    public class MediaMetadata
    {
        public MediaMetadata()
        {
        }

        public MediaMetadata(string URL, double aspectRatio)
        {
            this.URL = URL;
            AspectRatio = aspectRatio;
        }

        public string URL { get; set; }
        public double AspectRatio { get; set; }
    }
}
