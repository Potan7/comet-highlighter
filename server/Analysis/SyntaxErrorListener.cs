using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

public sealed class SyntaxErrorListener : BaseErrorListener
{
    private readonly List<Diagnostic> _diags = new();
    private readonly CommonTokenStream _tokens;
    private readonly IVocabulary _vocab;

    public IReadOnlyList<Diagnostic> Diagnostics => _diags;

    public SyntaxErrorListener(CommonTokenStream tokens, IVocabulary vocab)
    {
        _tokens = tokens;
        _vocab  = vocab;
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        var pretty = PrettyMessage(recognizer, offendingSymbol, msg, e);

        var start = new Position(line - 1, Math.Max(charPositionInLine, 0));
        var endCol = offendingSymbol?.Text?.Length ?? 1;
        var end   = new Position(line - 1, Math.Max(charPositionInLine + endCol, start.Character + 1));

        _diags.Add(new Diagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Message  = pretty,
            Range    = new Range(start, end),
            Source   = "PlanetParser"
        });
    }

    // === 메시지 꾸미기 ===
    private string PrettyMessage(IRecognizer recognizer, IToken offending, string msg, RecognitionException e)
    {
        // 1) ; 또는 줄바꿈 누락 힌트
        if (e is InputMismatchException ime && ime.GetExpectedTokens() is IntervalSet set)
        {
            if (set.Contains(PlanetParser.SEMI) || set.Contains(PlanetParser.NEWLINE))
                return "문장 끝이 필요해요. 마지막에 세미콜론(;) 또는 줄바꿈(Enter)으로 문장을 종료하세요.";
        }

        // 2) 식별자 다음의 '=' 대입문 힌트
        if (offending != null && offending.Type == PlanetLexer.ASSIGN)
        {
            var prev = PreviousNonHidden(offending.TokenIndex);
            if (prev != null && prev.Type == PlanetLexer.Identifier)
                return "대입문 형태가 올바르지 않아요. 예) i = i + 1  (문장 끝에는 ; 또는 줄바꿈이 필요합니다)";
        }

        // 3) 공통 케이스
        switch (e)
        {
            // case MissingTokenException mte:
                // return $"여기에 '{Friendly(mte.MissingType)}' 가(이) 빠졌어요.";
            case NoViableAltException:
                return "이 위치의 코드를 해석할 수 없어요. 구문을 다시 확인하세요.";
            case InputMismatchException iptme:
                return $"여기서는 {ExpectedList(iptme.GetExpectedTokens())} 가(이) 올 수 있어요.";
        }

        var tok = offending == null ? "EOF" : $"'{offending.Text}'";
        return $"알 수 없는 구문 오류: {tok} 부근에서 문법을 이해하지 못했어요.";
    }

    private IToken? PreviousNonHidden(int idx)
    {
        for (int i = idx - 1; i >= 0; i--)
        {
            var t = _tokens.Get(i);
            if (t.Channel == TokenConstants.DefaultChannel) return t;
        }
        return null;
    }

    private string Friendly(int tokenType)
    {
        if (tokenType == PlanetParser.SEMI)    return "세미콜론(;)";
        if (tokenType == PlanetParser.NEWLINE) return "줄바꿈(Enter)";
        if (tokenType == PlanetParser.ASSIGN)  return "'='";
        return _vocab.GetDisplayName(tokenType);
    }

    private string ExpectedList(IntervalSet set)
    {
        var names = new List<string>();
        foreach (var t in set.ToArray())
        {
            var f = Friendly(t);
            if (!names.Contains(f)) names.Add(f);
            if (names.Count >= 5) break;
        }
        return string.Join(", ", names);
    }
}
