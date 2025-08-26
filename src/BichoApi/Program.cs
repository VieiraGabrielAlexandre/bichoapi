using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// EF Core (Postgres) — troque para UseMySql se for MySQL
var cs = builder.Configuration.GetConnectionString("Db")!;
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

builder.Services.AddScoped<WalletService>();
builder.Services.AddSingleton<GroupTable>();
builder.Services.AddScoped<BetEngine>();
builder.Services.AddScoped<ReportsService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddNpgSql(cs);

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateBetRequestValidator>();

var app = builder.Build();

// EF Migrate + Seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();