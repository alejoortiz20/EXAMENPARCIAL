using EXAMENPARCIAL.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=incidencias.db"));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Operaciones/Incidencias");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

await SeedAsync(app);

app.Run();

static async Task SeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    await db.Database.EnsureCreatedAsync();

    var supervisor = await users.FindByNameAsync("supervisor");

    if (supervisor is null)
    {
        supervisor = new IdentityUser { UserName = "supervisor" };
        await users.CreateAsync(supervisor, "Supervisor2026");
    }
    else if (!await users.HasPasswordAsync(supervisor))
    {
        await users.AddPasswordAsync(supervisor, "Supervisor2026");
    }

    if (!await db.Incidencias.AnyAsync())
    {
        db.Incidencias.AddRange(
            new Incidencia { Estacion = "Estacion Centro", Descripcion = "Freno delantero roto", Prioridad = 3, Estado = "Abierta" },
            new Incidencia { Estacion = "Estacion Plaza Mayor", Descripcion = "Llanta completamente desinflada", Prioridad = 1, Estado = "Abierta" },
            new Incidencia { Estacion = "Estacion Universidad", Descripcion = "Sillín pedernal suelto en el frame 14", Prioridad = 2, Estado = "Abierta" },
            new Incidencia { Estacion = "Estacion San Martin", Descripcion = "Cadena desprendida del plato trasero", Prioridad = 1, Estado = "Abierta" },
            new Incidencia { Estacion = "Estacion Mercado", Descripcion = "Canasto frontal deformado", Prioridad = 3, Estado = "Abierta" },
            new Incidencia { Estacion = "Estacion Plaza Norte", Descripcion = "Timbre de la stationCard no suena", Prioridad = 2, Estado = "Abierta" });

        await db.SaveChangesAsync();
    }
}
