import * as path from "path";
import * as vscode from "vscode";
import { LanguageClient, TransportKind } from "vscode-languageclient/node";

let client: LanguageClient;

export async function activate(ctx: vscode.ExtensionContext) {
  // 서버 실행 파일 경로 (dll → dotnet 실행, 또는 단일 exe 가능)
  const serverPath = ctx.asAbsolutePath(path.join("server", "CometLangServer.dll"));
  const serverOptions = { 
    command: "dotnet", 
    args: [serverPath], 
    transport: TransportKind.stdio 
  };

  // 여기서 planet 언어 ID와 연결
  const clientOptions = {
    documentSelector: [
      { scheme: "file", language: "planet" },   // 파일 저장소 내 .planet 파일
      { scheme: "untitled", language: "planet" } // 아직 저장 안 한 임시 문서도 인식
    ]
  };

  client = new LanguageClient(
    "planet-lsp",   // 내부 ID
    "Planet LSP",   // VSCode UI에 표시되는 이름
    serverOptions,
    clientOptions
  );

  ctx.subscriptions.push(client);
  client.start();

  // 명령 → 서버 ExecuteCommand 호출
  ctx.subscriptions.push(
    vscode.commands.registerCommand("comet.compile", async () => {
      await client.sendRequest("workspace/executeCommand", {
        command: "comet.compile",
        arguments: []
      });
    })
  );
  console.log("Comet LSP activated");
}

export function deactivate() { 
  return client?.stop(); 
}
