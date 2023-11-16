namespace Leaflet.API.ReusableClass
{
    public class UserCardDataForClient
    {
        public UserCardDataForClient(ReusableClass.BriefUserInfo user, string? summary, MediaMetadata backgroundImage)
        {
            User = user;
            Summary = summary;
            BackgroundImage = backgroundImage;
        }

        public ReusableClass.BriefUserInfo User { get; set; }
        public string? Summary { get; set; }
        public MediaMetadata BackgroundImage { get; set; }
    }
}
