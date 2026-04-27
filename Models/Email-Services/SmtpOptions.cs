namespace BenueCommunityMapping.Models.Email_Services
{
    public class SmtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool UseSsl { get; set; } = true;
        public string UserName { get; set; }
        public string Password { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
    }
    public class MailConfigAPI
    {
        public string key { get; set; }
        public string mailuser { get; set; }
        public string endpoint { get; set; }
    }
    //App
    public class BaseUrls
    {
        public string BaseUrl { get; set; }
        //public string mailuser { get; set; }
        //public string endpoint { get; set; }
    }
}
