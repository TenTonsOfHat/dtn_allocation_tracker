using Library;
using Microsoft.AspNetCore.Mvc;
using Polly;
using Polly.Extensions.Http;
using Refit;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddRefitClient<IAllocationTrackerRefitClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.dtn.com/fuelsuite/"))
    .AddPolicyHandler(_ =>
    {
        return HttpPolicyExtensions.HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    });

builder.Services.AddTransient<IAllocationTrackerService, AllocationTrackerService>();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddEasyCaching(options =>
{
    // use memory cache with a simple way
    options.UseInMemory("default");
});

var app = builder.Build();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.ConfigObject.AdditionalItems.Add("syntaxHighlight", false);
    });
}

app.MapPost("/allocations", async ([FromServices] IAllocationTrackerService service, AllocationTrackerCredentials credentials) =>
    {
        var resp = await service.Allocations(new AllocationRequest()
        {
            Credentials = credentials
        });

        return resp.Content;
    })
    .WithName("Allocations")
    .WithOpenApi();

app.Run();

