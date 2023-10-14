namespace ChatWithWeb.Models
{
    public class PageItem : EventArgs
    {
        public int No { get; set; }
        public string URL { get; set; }
        public string FileName { get; set; }
    }
    public class IndexItem : EventArgs
    {
        public int No { get; set; }
        public string FileName { get; set; }
    }
    public class RAGItem
    {
        public List<SourceItem> Sources { get; set; } = new();
        public string Question { get; set; }
        public string Answer { get; set; }
        public DateTime CreatedDate { get; set; }
    }
    public class SourceItem
    {
        public string Source { get; set; }
        public string Link { get; set; }
    }
 
}
