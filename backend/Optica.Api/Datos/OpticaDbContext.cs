using Microsoft.EntityFrameworkCore;

namespace Optica.Api.Datos;

public class OpticaDbContext(DbContextOptions<OpticaDbContext> options) : DbContext(options)
{
}
