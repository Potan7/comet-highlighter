using CometLangServer.Analysis;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;

namespace CometLangServer.Handlers;

public class TextSyncHandler : ITextDocumentSyncHandler
{
    private readonly DocumentManager _documentManager;

    private readonly ILanguageServerFacade _server;
    public TextSyncHandler(ILanguageServerFacade server, DocumentManager documentManager)
    {
        _server = server;
        _documentManager = documentManager;
    }

    // 변경 감지 방식
    public TextDocumentSyncKind Change => TextDocumentSyncKind.Full;

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

    // SyncKind를 Full로 바꿨기 때문에, ContentChanges.First().Text에 전체 텍스트가 들어옴
    public Task<Unit> Handle(DidChangeTextDocumentParams req, CancellationToken _)
        => ValidateAsync(req.TextDocument.Uri, req.ContentChanges.First().Text);

    public Task<Unit> Handle(DidCloseTextDocumentParams req, CancellationToken _)
    {
        // 파일이 닫히면 진단 정보 클리어
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = req.TextDocument.Uri,
            Diagnostics = new Container<Diagnostic>() // 빈 목록 전달
        });
        return Unit.Task;
    }

    public Task<Unit> Handle(DidSaveTextDocumentParams req, CancellationToken _) => Unit.Task;

    // 진단 로직을 DocumentManager 호출로 변경
    private Task<Unit> ValidateAsync(DocumentUri uri, string text)
    {
        // _server.Window.LogInfo($"Validating {uri}...");
        try
        {
            // DocumentManager를 통해 파싱하고 진단 결과를 받아옴
            var diagnostics = _documentManager.UpdateDocument(uri.ToString(), text);

            // 서버를 통해 VS Code 클라이언트로 진단 정보 전송
            _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = new Container<Diagnostic>(diagnostics)
            });

            // _server.Window.LogInfo($"Validated {uri}, found {diagnostics.Count} issues.");
        }
        catch (Exception ex)
        {
            _server.Window.LogError($"Validation error: {ex}");
        }
        return Unit.Task;
    }
}
