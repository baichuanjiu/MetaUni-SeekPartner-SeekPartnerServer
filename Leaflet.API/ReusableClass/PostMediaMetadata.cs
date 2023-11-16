namespace Leaflet.API.ReusableClass
{
    public class PostMediaMetadata
    {
        public PostMediaMetadata()
        {
        }

        public PostMediaMetadata(IFormFile file, double aspectRatio)
        {
            File = file;
            AspectRatio = aspectRatio;
        }

        public IFormFile File { get; set; }
        public double AspectRatio { get; set; }
    }
}
