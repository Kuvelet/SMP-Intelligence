using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Smp.Sample.Browser;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var host = builder.Build();
BrowserApi.Initialize((IJSInProcessRuntime)host.Services.GetRequiredService<IJSRuntime>());
await host.RunAsync();
