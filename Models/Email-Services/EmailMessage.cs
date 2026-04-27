namespace BenueCommunityMapping.Models.Email_Services
{
    public class EmailMessage
    {
        public string ToEmail { get; set; }
        public string ToName { get; set; }      // optional
        public string Subject { get; set; }
        public string BodyHtml { get; set; }    // html body optional
        public string BodyText { get; set; }    // plain-text fallback optional
        public List<(byte[] data, string filename, string contentType)> Attachments { get; set; } = new List<(byte[] data, string filename, string contentType)>();
    }
}
