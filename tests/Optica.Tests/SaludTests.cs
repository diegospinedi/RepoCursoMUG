using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Optica.Tests;

public class SaludTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Salud_responde_ok()
    {
        var respuesta = await factory.CreateClient().GetAsync("/api/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
