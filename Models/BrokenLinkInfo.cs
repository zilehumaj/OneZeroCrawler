namespace OneZeroCrawler.Models
{
    public class BrokenLinkInfo
    {
        public string SourcePage { get; set; } = "";

        public string BrokenUrl { get; set; } = "";

        public string AnchorText { get; set; } = "";

        public int StatusCode { get; set; }
    }
}
