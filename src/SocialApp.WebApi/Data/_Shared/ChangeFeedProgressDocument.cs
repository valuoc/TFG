using System.Text;

namespace SocialApp.WebApi.Data._Shared;

public record ChangeFeedProgressDocument(string Processor, string Range, string? Continuation) 
    : Document(Key(Processor, Range))
{
    public static DocumentKey Key(string processor, string range)
    {
        var pk = "change_feed_progress:"+processor;
        var id = Convert.ToBase64String(Encoding.UTF8.GetBytes(range));
        return new DocumentKey(pk, id);
    }
}