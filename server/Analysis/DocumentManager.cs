using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Concurrent;

namespace CometLangServer.Analysis;

public class DocumentManager
{
    private readonly ConcurrentDictionary<string, List<Diagnostic>> _diagnosticsCache = new();

    public List<Diagnostic> UpdateDocument(string uri, string text)
    {
        var inputStream = new AntlrInputStream(text);
        var lexer       = new PlanetLexer(inputStream);
        var tokens      = new CommonTokenStream(lexer);
        var parser      = new PlanetParser(tokens);

        // 기본 리스너 제거 후, 커스텀 리스너(사람 친화 메시지) 부착
        var listener = new SyntaxErrorListener(tokens, parser.Vocabulary);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        // lexer.AddErrorListener(listener);   // lexer 에도 붙여서 토큰화 오류 수집
        parser.AddErrorListener(listener);

        // 여러 에러를 계속 수집하려면 기본 전략 유지
        // parser.ErrorHandler = new DefaultErrorStrategy();

        parser.program();

        var diagnostics = new List<Diagnostic>(listener.Diagnostics);
        _diagnosticsCache[uri] = diagnostics;
        return diagnostics;
    }
}
