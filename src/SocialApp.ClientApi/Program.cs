using System.Text;
using Microsoft.Extensions.Configuration;
using SocialApp.ClientApi;
using SocialApp.ClientApi.Cli;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

var cancelProgram = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) => cancelProgram.Cancel();

var root = new RootCommander();
Commander? current = root;

var initContext = new CommandContext(cancelProgram.Token);
foreach (var item in configuration.GetSection("Regions").GetChildren())
{
    await root.ProcessAsync(["region","add", item.Key, item.GetValue<string>("Url")??throw new ArgumentException("Missing region url in config.")], initContext);
    if(item.GetValue("Default", false))
    {
        await root.ProcessAsync(["region", "set", item.Key], initContext);
    }
}

Console.WriteLine("Configuration loaded!");
Console.WriteLine();

await InteractiveInput.RunAsync(root, cancelProgram.Token);
Console.WriteLine("Bye!");

