using Library;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Extensions.Http;
using Refit;

void MapEndpoints(WebApplication app1)
{
    app1.MapPost("/allocations", async ([FromServices] IAllocationTrackerService service, AllocationTrackerCredentials credentials) =>
        {
            var resp = await service.Allocations(new AllocationRequest()
            {
                Credentials = credentials
            });

            return resp.Content;
        })
        .WithName("Allocations")
        .WithOpenApi();
}

void ConfigureServices(WebApplicationBuilder webApplicationBuilder)
{
    webApplicationBuilder.Services.AddEndpointsApiExplorer();
    webApplicationBuilder.Services.AddSwaggerGen();
    webApplicationBuilder.Services
        .AddRefitClient<IAllocationTrackerRefitClient>(new RefitSettings(
            new NewtonsoftJsonContentSerializer(
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            )
        ))
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.dtn.com/fuelsuite/"))
        .AddPolicyHandler(_ =>
        {
            return HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        });

    webApplicationBuilder.Services.AddTransient<IAllocationTrackerService, AllocationTrackerService>();

    webApplicationBuilder.Services.AddResponseCompression(options => { options.EnableForHttps = true; });

    webApplicationBuilder.Services.AddEasyCaching(options =>
    {
        // use memory cache with a simple way
        options.UseInMemory("default");
    });
}

void ConfigureApp(WebApplication webApplication)
{
    webApplication.UseResponseCompression();
    webApplication.UseSwagger();
    webApplication.UseSwaggerUI(options => { options.ConfigObject.AdditionalItems.Add("syntaxHighlight", false); });
}

var builder = WebApplication.CreateBuilder(args);

ConfigureServices(builder);

var app = builder.Build();

ConfigureApp(app);

MapEndpoints(app);

app.Run();