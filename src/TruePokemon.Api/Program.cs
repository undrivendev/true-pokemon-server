using Serilog;
using Serilog.Events;
using SimpleInjector;
using TruePokemon.Api;
using TruePokemon.Application;
using TruePokemon.Application.Queries;
using TruePokemon.Core.Abstractions;
using TruePokemon.Core.Mediator;
using TruePokemon.Core.Mediator.DependencyInjection;
using TruePokemon.Infrastructure;
using Constants = TruePokemon.Application.Constants;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web host");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog(); // replace built-in logging with Serilog

// Add services to the container.
    builder.Services.AddControllers();

// swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddHttpClient(nameof(ShakespeareTranslationApiService))
        .AddPolicyHandler(Constants.DefaultRetryPolicy);
    builder.Services.AddHttpClient(nameof(PokemonDataApiRepository))
        .AddPolicyHandler(Constants.DefaultRetryPolicy);

// SimpleInjector
    var container = Container;
    container.Options.DefaultLifestyle = Lifestyle.Transient;
    builder.Services.AddSimpleInjector(container, options => options.AddAspNetCore().AddControllerActivation());
// mediator
    container.Register<IContainer>(() => new ContainerServiceProviderWrapper(container));
    container.Register<IMediator, Mediator>();
    container.Register(() =>
            builder.Configuration.GetSection("ShakespeareTranslationApiService")
                .Get<ShakespeareTranslationApiRepositoryOptions>(),
        Lifestyle.Scoped);
    container.Register<IPokemonDataRepository, PokemonDataApiRepository>();
    container.Register(() =>
            builder.Configuration.GetSection("PokemonDataApiRepository")
                .Get<PokemonDataApiRepositoryOptions>(),
        Lifestyle.Scoped);
    container.Register<ITranslationService, ShakespeareTranslationApiService>();


// mediator handlers
    container.Register(
        typeof(IQueryHandler<,>),
        typeof(PokemonQueryHandler).Assembly);

// handlers decorators
    container.RegisterDecorator(
        typeof(IQueryHandler<,>),
        typeof(QueryHandlerLoggingDecorator<,>));

    container.RegisterDecorator(
        typeof(IQueryHandler<,>),
        typeof(QueryHandlerCachingDecorator<,>));

    var app = builder.Build();

    app.Services.UseSimpleInjector(container);

    app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
    app.UseSwagger();
    app.UseSwaggerUI();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseCors(cors => cors
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

    app.UseAuthorization();
    app.MapControllers();

    container.Verify();

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
    public static readonly Container Container = new();
}