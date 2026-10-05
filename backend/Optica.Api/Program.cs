using Microsoft.EntityFrameworkCore;
using Optica.Api.Datos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<OpticaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Optica")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.Run();
