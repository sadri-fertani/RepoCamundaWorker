using TestWrkCmd.Common.Payloads;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/hello", () =>
{
    return new RespApi { Id = 1, Message = "Hello, World!" };
});

await app.RunAsync();
