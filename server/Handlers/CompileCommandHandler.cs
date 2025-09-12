
using System.Diagnostics;
using System.Text.RegularExpressions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using MediatR;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;


namespace CometLangServer.Handlers;

public class CompileCommandHandler : IExecuteCommandHandler
{
    private readonly ILanguageServerFacade _server;
    public CompileCommandHandler(ILanguageServerFacade server) => _server = server;

    public ExecuteCommandRegistrationOptions GetRegistrationOptions(ExecuteCommandCapability capability, ClientCapabilities clientCapabilities)
        => new() { Commands = new Container<string>("comet.compile") };

    public async Task<Unit> Handle(ExecuteCommandParams req, CancellationToken ct)
    {
        // 1) 워크스페이스 루트에서 컴파일러 실행 (예: cometc)
        var psi = new ProcessStartInfo
        {
            FileName = "cometc", // 또는 절대경로/설정값
            Arguments = "build",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);

        // 2) 출력 파싱 → Diagnostics (예: file:line:col: error|warning: message)
        var diagByFile = new Dictionary<string, List<Diagnostic>>();
        var rx = new Regex(@"^(?<file>.+?):(?<line>\d+):(?<col>\d+):\s*(?<sev>error|warning):\s*(?<msg>.+)$",
                           RegexOptions.Multiline);
        foreach (Match m in rx.Matches(stdout + "\n" + stderr))
        {
            var file = m.Groups["file"].Value;
            var line = int.Parse(m.Groups["line"].Value) - 1;
            var col = int.Parse(m.Groups["col"].Value) - 1;
            var sev = m.Groups["sev"].Value == "error" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
            var msg = m.Groups["msg"].Value.Trim();

            var list = diagByFile.GetValueOrDefault(file) ?? (diagByFile[file] = new());
            list.Add(new Diagnostic
            {
                Severity = sev,
                Message = msg,
                Range = new Range(new Position(line, col), new Position(line, Math.Max(col + 1, col))),
                Source = "cometc"
            });
        }

        // 3) 파일별로 진단 푸시
        foreach (var (file, list) in diagByFile)
        {
            _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = DocumentUri.From(file),
                Diagnostics = list
            });
        }

        // 4) 요약 메시지
        _server.Window.ShowMessage(new ShowMessageParams
        {
            Message = $"Comet compile finished (exit {p.ExitCode})",
            Type = p.ExitCode == 0 ? MessageType.Info : MessageType.Error
        });

        return Unit.Value;
    }
}
