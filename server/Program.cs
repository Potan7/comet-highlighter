using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Window;
using CometLangServer.Handlers;
using Microsoft.Extensions.DependencyInjection;
using CometLangServer.Analysis;

namespace CometLangServer;

class Program
{
    static async Task Main(string[] args)
    {
        var server = await LanguageServer.From(opts => opts
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithServices(services =>
            {
                services.AddSingleton<DocumentManager>();
            })
            .WithHandler<TextSyncHandler>()
            .WithHandler<CompletionHandler>()
            .WithHandler<CompileCommandHandler>()
            .OnInitialize((s, _, _) => { s.Window.LogInfo("Planet LSP ready"); return Task.CompletedTask; })
        );
        await server.WaitForExit;
        
    }
}
