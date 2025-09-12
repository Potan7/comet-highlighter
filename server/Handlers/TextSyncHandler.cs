using System.Text.RegularExpressions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using LspPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace CometLangServer.Handlers;

public class TextSyncHandler : ITextDocumentSyncHandler
{
    private readonly ILanguageServerFacade _server;
    public TextSyncHandler(ILanguageServerFacade server) => _server = server;

    // 변경 감지 방식
    public TextDocumentSyncKind Change => TextDocumentSyncKind.Incremental;

    // ===== 필수 Registration 4종 (명시적 구현) =====

    // DidOpen
    TextDocumentOpenRegistrationOptions
        IRegistration<TextDocumentOpenRegistrationOptions, TextSynchronizationCapability>
        .GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        => new();

    // DidChange
    TextDocumentChangeRegistrationOptions
        IRegistration<TextDocumentChangeRegistrationOptions, TextSynchronizationCapability>
        .GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            SyncKind = Change
        };

    // DidClose
    TextDocumentCloseRegistrationOptions
        IRegistration<TextDocumentCloseRegistrationOptions, TextSynchronizationCapability>
        .GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        => new();

    // DidSave
    TextDocumentSaveRegistrationOptions
        IRegistration<TextDocumentSaveRegistrationOptions, TextSynchronizationCapability>
        .GetRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        => new()
        {
            IncludeText = false
        };

    // 문서 속성
    public TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "planet");

    // 핸들러들
    public Task<Unit> Handle(DidOpenTextDocumentParams req, CancellationToken _)
        => ValidateAsync(req.TextDocument.Uri, req.TextDocument.Text);

    public Task<Unit> Handle(DidChangeTextDocumentParams req, CancellationToken _)
        => ValidateAsync(req.TextDocument.Uri, req.ContentChanges.First().Text);

    public Task<Unit> Handle(DidCloseTextDocumentParams r, CancellationToken _)
    {
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams { Uri = r.TextDocument.Uri, Diagnostics = Array.Empty<Diagnostic>() });
        return Task.FromResult(Unit.Value);
    }

    public Task<Unit> Handle(DidSaveTextDocumentParams r, CancellationToken _) => Task.FromResult(Unit.Value);

    // 진단 로직
    private Task<Unit> ValidateAsync(DocumentUri uri, string text)
    {
        var diags = new List<Diagnostic>();

        var m = Regex.Match(text, @"\bTODO\b");
        if (m.Success)
        {
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Warning,
                Message = "Resolve TODO",
                Range = new LspRange(new LspPosition(0, 0), new LspPosition(0, 4)),
                Source = "planet"
            });
        }

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams { Uri = uri, Diagnostics = diags });
        return Task.FromResult(Unit.Value);
    }
}
