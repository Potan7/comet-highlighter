using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace CometLangServer.Handlers;

public class CompletionHandler : ICompletionHandler
{
    public CompletionRegistrationOptions GetRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("planet"),
            TriggerCharacters = new[] { " ", "." }
        };

    static readonly string[] Keywords =
    [
        "if","while","execute","return","import","var","def"
    ];

    public Task<CompletionList> Handle(CompletionParams _, CancellationToken __)
    {
        var items = Keywords.Select(keyword => new CompletionItem { Label = keyword, Kind = CompletionItemKind.Keyword });
        return Task.FromResult(new CompletionList(items, isIncomplete: false));
    }
}
