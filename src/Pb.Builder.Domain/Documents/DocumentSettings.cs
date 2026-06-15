namespace Pb.Builder.Domain.Documents;

public sealed record DocumentSettings(string BrowserTitle)
{
    public static DocumentSettings Create(string browserTitle)
    {
        return new DocumentSettings(browserTitle.Trim());
    }
}
