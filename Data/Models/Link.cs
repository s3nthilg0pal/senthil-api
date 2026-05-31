public sealed class Link
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ContentType { get; set; } = "link";

    public DateTime Date { get; set; }

    public DateTime CreatedAt { get; set; }
}