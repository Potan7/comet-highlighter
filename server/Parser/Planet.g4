grammar Planet;

// ========================= LEXER MEMBERS (must be before any rules) =========================
@lexer::members {
    // 직전 개행의 인덱스(마지막 '\n'의 위치), 파일 시작 전은 -1
    private int _lastNl = -1;

    // 현재 위치가 "논리적 줄의 첫 비공백"인지 검사
    private bool IsAtLineIndentStart() {
        int stop = this.InputStream.Index - 1;   // 현재 토큰 시작 직전 문자
        int start = _lastNl + 1;                 // 직전 개행 다음 문자
        if (stop < start) return true;           // 개행 직후 바로 시작

        var text = this.InputStream.GetText(new Antlr4.Runtime.Misc.Interval(start, stop));
        for (int i = 0; i < text.Length; i++) {
            char ch = text[i];
            if (ch != ' ' && ch != '\t') return false;
        }
        return true;
    }
}

// ========================= PARSER RULES =========================

program
  : (topLevelDecl | statement)* EOF
  ;

topLevelDecl
  : functionDecl
  ;

functionDecl
  : DEF Identifier LPAREN paramList? RPAREN block
  ;

paramList
  : Identifier (COMMA Identifier)*
  ;

block
  : LBRACE statement* RBRACE
  ;

// ----- statements -----
statement
  : varDecl
  | importStmt
  | ifStmt
  | whileStmt
  | returnStmt
  | commandStmt                 // /... 한 줄 or execute(...) { ... }
  | assignStmt
  | exprStmt
  | emptyStmt
  ;

varDecl
  : VAR Identifier (ASSIGN expr)? eos
  ;

importStmt
  : IMPORT stringLiteral eos
  ;

ifStmt
  : IF LPAREN expr RPAREN block (ELSE block)?
  ;

whileStmt
  : WHILE LPAREN expr RPAREN block
  ;

returnStmt
  : RETURN expr? eos
  ;

// /로 시작하는 한 줄 명령 or execute(...) { ... } 블록
commandStmt
  : CMD_LINE NEWLINE+           // 예: /say hello
  | EXEC_HEADER block           // 예: execute(as @s){ ... }
  ;

assignStmt
  : lvalue ASSIGN expr eos
  ;

// a, a[0], a[i][j] 등을 LHS로 허용
lvalue
  : Identifier (indexer)*
  ;


exprStmt
  : expr eos
  ;

emptyStmt
  : eos
  ;

// 문장 종료
eos
  : (SEMI | NEWLINE)+
  ;

// ========================= EXPRESSIONS =========================
// (우선순위: 괄호 > 인덱싱/호출 > */% > +- > 관계 > 동등 > and > or > 단항(!))

expr       : logicOr ;
logicOr    : logicAnd (OR  logicAnd)* ;
logicAnd   : equality (AND equality)* ;
equality   : relation ((EQ | NEQ) relation)* ;
relation   : add ((LT | LTE | GT | GTE) add)* ;
add        : mul ((PLUS | MINUS) mul)* ;
mul        : unary ((MUL | DIV | MOD) unary)* ;

unary
  : NOT LPAREN expr RPAREN
  | postfix
  ;

// 원자 + (호출/인덱싱)*
postfix
  : atom (callSuffix | indexer)*
  ;

atom
  : literal
  | Identifier
  | LPAREN expr RPAREN
  | arrayLiteral
  ;

// f(...), foo(a, b, c)
callSuffix
  : LPAREN argList? RPAREN
  ;

argList
  : expr (COMMA expr)*
  ;

// a[expr], foo()[i]
indexer
  : LBRACK expr RBRACK
  ;

// 리터럴
literal
  : INT
  | FLOAT
  | DOUBLE
  | stringLiteral
  | nbtObject
  ;

stringLiteral
  : STRING
  ;

arrayLiteral
  : LBRACK (expr (COMMA expr)*)? RBRACK
  ;

// NBT(JSON 유사)
nbtObject
  : LBRACE (nbtPair (COMMA nbtPair)*)? RBRACE
  ;

nbtPair
  : nbtKey COLON nbtValue
  ;

nbtKey
  : Identifier
  | STRING
  ;

nbtValue
  : STRING
  | INT
  | FLOAT
  | DOUBLE
  | nbtObject
  | arrayLiteral
  | TRUE
  | FALSE
  | NULL
  ;

// ========================= LEXER RULES =========================

// robust line command: 줄의 첫 비공백에서 시작하는 /... 만 한 줄 명령으로 인식
// (DIV보다 위에 둬야 산술 나눗셈과 충돌하지 않음)
CMD_LINE
  : {IsAtLineIndentStart()}? '/' ~[\r\n]*
  ;

// execute(...) { ... } 헤더 (함수 정의와 충돌 방지용)
EXEC_HEADER
  : 'execute' [ \t\f]* '(' ~[\r\n]* ')'
  ;

// 키워드
DEF      : 'def';
VAR      : 'var';
IF       : 'if';
ELSE     : 'else';
WHILE    : 'while';
RETURN   : 'return';
IMPORT   : 'import';
AND      : 'and';
OR       : 'or';
NOT      : '!';
TRUE     : 'true';
FALSE    : 'false';
NULL     : 'null';
EXECUTE  : 'execute';

// 특수 예약어(README 설명용)
UNDER_NS   : '__namespace__';
UNDER_MAIN : '__main__';

// 연산자/구분자
ASSIGN : '=';
EQ     : '==';
NEQ    : '!=';
LTE    : '<=';
GTE    : '>=';
LT     : '<';
GT     : '>';
PLUS   : '+';
MINUS  : '-';
MUL    : '*';
DIV    : '/';
MOD    : '%';
LPAREN : '(';
RPAREN : ')';
LBRACE : '{';
RBRACE : '}';
LBRACK : '[';
RBRACK : ']';
COMMA  : ',';
COLON  : ':';
SEMI   : ';';

// 숫자
FLOAT
  : '-'? (DIGIT+ ('.' DIGIT+)? | '.' DIGIT+) 'f'
  ;

DOUBLE
  : '-'? (DIGIT+ '.' DIGIT* | '.' DIGIT+)
  ;

INT
  : '-'? DIGIT+
  ;

// 문자열
STRING
  : '"' ( '\\' . | ~["\\\r\n] )* '"'
  ;

// 식별자
Identifier
  : [A-Za-z_][A-Za-z_0-9]*
  ;

// 줄바꿈 (상태 업데이트 포함)
NEWLINE
  : ('\r'? '\n')+ { _lastNl = this.InputStream.Index - 1; }
  ;

// 공백
WS
  : [ \t\f]+ -> channel(HIDDEN)
  ;

// 주석 (# ...)
COMMENT
  : '#' ~[\r\n]* -> channel(HIDDEN)
  ;

// 프래그먼트
fragment DIGIT : [0-9];
