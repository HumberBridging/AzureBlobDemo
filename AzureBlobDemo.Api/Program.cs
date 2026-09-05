
using Azure.Identity;
using AzureBlobDemo.Api.Options;
using AzureBlobDemo.Api.Services;
using Microsoft.Extensions.Azure;
using Scalar.AspNetCore;

namespace AzureBlobDemo.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        //Configuration //Fail fast approach
        builder.Services.AddOptions<BlobStorageOptions>()
            .Bind(builder.Configuration.GetSection(BlobStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.ServiceUri is not null || !string.IsNullOrWhiteSpace(o.ConnectionString),
                "Set BlobStorage:ServiceUri (production, Managed Identity) or BlobStorage:ConnectionString (Azurite only).")
            .ValidateOnStart();

        var storageOptions = builder.Configuration
            .GetSection(BlobStorageOptions.SectionName)
            .Get<BlobStorageOptions>() ?? new BlobStorageOptions();

        // AddAzureClients() gives us, for free:
        //   - correct singleton lifetime (these clients are thread-safe and hold a connection pool;
        //   - creating one per request is the classic HttpClient socket-exhaustion bug all over again)
        //   - a shared retry/telemetry policy via ConfigureDefaults
        //   - IAzureClientFactory<T> when you need more than one account
        //   - one line to swap a connection string for a Managed Identity

        //Creating the Azure client based on the configuration
        builder.Services.AddAzureClients(clients => 
        {
            //Production
            if (storageOptions.ServiceUri is not null)
            {
                clients.AddBlobServiceClient(storageOptions.ServiceUri);
                
                //We would be setting up Managed Identity later. But for now we would use the connection string
                clients.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    // In class, exclude the slow interactive fallbacks so a failure is fast and obvious.
                    ExcludeInteractiveBrowserCredential = true
                }));

            }
            else if (!string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
            {
                clients.AddBlobServiceClient(storageOptions.ConnectionString);
            }

            // You cna configure common configurations here
            clients.ConfigureDefaults(options =>
            {
                options.Retry.Mode = Azure.Core.RetryMode.Exponential;
                options.Retry.MaxRetries = 5;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.MaxDelay = TimeSpan.FromSeconds(10);
            });

        });

        //DI
        builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

        // ProblemDetails (RFC 9457) for every unhandled failure, instead of a stack trace or an empty 500.
        builder.Services.AddProblemDetails();

        // .NET 10 ships OpenAPI document generation in the box - no Swashbuckle generator needed.
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
