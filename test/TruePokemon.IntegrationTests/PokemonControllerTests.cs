using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TruePokemon.Core.Models;
using TruePokemon.Infrastructure;
using TruePokemon.Tests.Common;
using Xunit;

namespace TruePokemon.IntegrationTests;

[Trait("Category", "Integration")]
public class CustomersControllerTests : IClassFixture<AppWebApplicationFactory>
{
    private readonly AppWebApplicationFactory _factory;

    public CustomersControllerTests(AppWebApplicationFactory factory)
    {
        _factory = factory;
    }


    [Fact]
    public async Task GetById_ValidRequest_ReturnsCorrectly()
    {
        // Arrange
        var pokemonName = "ditto";
        var expected = "Expected final trans";
        var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var typesToRemove = new[]
                    {
                        typeof(IHttpClientFactory),
                    };
                    var descriptors = services.Where(
                            d => typesToRemove.Contains(d.ServiceType))
                        .ToList();

                    foreach (var des in descriptors)
                    {
                        services.Remove(des);
                    }

                    Program.Container.Options.AllowOverridingRegistrations = true;
                    var optionsData = new PokemonDataApiRepositoryOptions
                        { BaseUrl = new Uri("http://localhost:5000") };
                    Program.Container.Register(() => optionsData);

                    var optionsTrans = new ShakespeareTranslationApiRepositoryOptions
                        { BaseUrl = new Uri("http://localhost:5001") };
                    Program.Container.Register(() => optionsTrans);

                    var handler = new MockHttpClientHandler();
                    handler.AddMockResponse(new Uri(optionsData.BaseUrl, $"pokemon/{pokemonName}"), HttpStatusCode.OK,
                        @"{""species"":{""url"":""https://www.test.com""}}");
                    handler.AddMockResponse(new Uri("https://www.test.com"), HttpStatusCode.OK,
                        @$"{{""flavor_text_entries"" : [{{""flavor_text"":""pokemontesttext""}}]}}");
                    handler.AddMockResponse(new Uri(optionsTrans.BaseUrl, $"shakespeare.json?text=pokemontesttext"),
                        HttpStatusCode.OK,
                        @$"{{""success"":{{""total"":1}},""contents"":{{""translated"":""{expected}"",""text"":"""",""translation"":""shakespeare""}}}}");

                    var clientFactory = new Mock<IHttpClientFactory>();
                    clientFactory.Setup(x => x.CreateClient(It.IsAny<string>()))
                        .Returns(() => new HttpClient(handler));
                    services.AddTransient(_ => clientFactory.Object);
                });
            })
            .CreateClient();


        // Act
        var response = await client.GetFromJsonAsync<PokemonTranslation>($"pokemon/{pokemonName}");

        // Assert
        response.Should().NotBeNull();
        response.Translation.Should().BeEquivalentTo(expected);
    }
}