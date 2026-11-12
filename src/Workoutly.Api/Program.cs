using Npgsql;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Threading.RateLimiting;
using Workoutly.Api.Auth;
using Workoutly.Api.Data;
using Workoutly.Api.Exercises;
using Workoutly.Api.Teaching;
using Workoutly.Api.Plans;
using Workoutly.Api.Workouts;

var migrateOnly = args.Contains("--migrate");
var createTeacher = args.Contains("--create-teacher");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument is not "--migrate" and not "--create-teacher").ToArray());
if (int.TryParse(builder.Configuration["PORT"], out var port) && port is > 0 and <= 65535)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
// Habilitar somente atrás do proxy do Render; fora dele mantém confiança padrão.
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, configuration) =>
{
    if (!configuration.GetValue<bool>("Hosting:RenderProxy")) return;
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
});
builder.Services.AddSingleton<PostgresDatabase>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddSingleton<IPasswordHasher<AccountProfile>, PasswordHasher<AccountProfile>>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ExerciseService>();
builder.Services.AddSingleton<TeachingService>();
builder.Services.AddSingleton<PlanService>();
builder.Services.AddSingleton<WorkoutService>();
builder.Services.AddSingleton<WorkoutHistoryService>();
builder.Services.AddAuthentication("Session")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>("Session", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("teacher", policy => policy.RequireRole("teacher"));
    options.AddPolicy("student", policy => policy.RequireRole("student"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
var app = builder.Build();
var renderProxy = app.Configuration.GetValue<bool>("Hosting:RenderProxy");

if (createTeacher)
{
    try
    {
        var name = builder.Configuration["TeacherSetup:Name"];
        var email = builder.Configuration["TeacherSetup:Email"];
        var password = builder.Configuration["TeacherSetup:Password"];
        var errors = AuthEndpoints.Validate(email, password, registration: true);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 120)
            errors["name"] = ["Informe um nome entre 2 e 120 caracteres."];
        if (errors.Count > 0)
        {
            foreach (var error in errors.Values.SelectMany(value => value)) Console.Error.WriteLine(error);
            Environment.ExitCode = 1;
        }
        else
        {
            var profile = await app.Services.GetRequiredService<AuthService>().CreateTeacherAsync(name!, email!, password!, CancellationToken.None);
            Console.WriteLine(profile is null ? "E-mail já cadastrado. Nenhuma conta ou permissão foi alterada." : "Conta de professor/admin criada. Entre pelo aplicativo.");
            if (profile is null) Environment.ExitCode = 1;
        }
    }
    catch (Exception exception) when (exception is NpgsqlException or ArgumentException or InvalidOperationException)
    {
        Console.Error.WriteLine("Não foi possível criar a conta. Confira a configuração e aplique as migrações.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}

if (migrateOnly)
{
    try
    {
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        Console.WriteLine("Migração PostgreSQL aplicada com sucesso.");
    }
    catch (Exception exception) when (exception is NpgsqlException or ArgumentException or InvalidOperationException)
    {
        var code = exception is PostgresException postgres ? $" (SQLSTATE {postgres.SqlState})" : string.Empty;
        Console.Error.WriteLine($"Não foi possível preparar o banco{code}. Confira a configuração, as permissões e a disponibilidade do PostgreSQL.");
        if (exception.InnerException is System.Net.Sockets.SocketException socket)
            Console.Error.WriteLine($"Falha de transporte: {socket.SocketErrorCode}.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}

if (renderProxy) app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var protectedResource = context.Request.Path.StartsWithSegments("/auth") || context.Request.Path.StartsWithSegments("/exercises") ||
        context.Request.Path.StartsWithSegments("/teaching") || context.Request.Path.StartsWithSegments("/enrollment") || context.Request.Path.StartsWithSegments("/plans") || context.Request.Path.StartsWithSegments("/sessions") || context.Request.Path.StartsWithSegments("/history");
    if (protectedResource)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.IsHttps && !app.Environment.IsDevelopment())
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Use HTTPS para acessar sua conta." });
            return;
        }
    }
    try { await next(context); }
    catch (Exception exception) when (protectedResource &&
        exception is NpgsqlException or InvalidOperationException or ArgumentException)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = "Serviço indisponível. Tente novamente em instantes." });
    }
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccountEndpoints();
app.MapExerciseEndpoints();
app.MapTeachingEndpoints();
app.MapPlanEndpoints();
app.MapWorkoutEndpoints();
app.MapWorkoutHistoryEndpoints();

// Liveness não depende do banco. Readiness comprova acesso ao esquema preparado.
app.MapGet("/health", (PostgresDatabase database) => Results.Ok(new
{
    service = "Workoutly.Api",
    status = "ok",
    databaseConfigured = database.IsConfigured
}));

app.MapGet("/health/ready", async (PostgresDatabase database, CancellationToken cancellationToken) =>
{
    var result = await database.CheckReadinessAsync(cancellationToken);
    return Results.Json(new
    {
        status = result.Ready ? "ready" : "unavailable",
        database = result.Database,
        schemaVersion = result.SchemaVersion
    }, statusCode: result.Ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.Run();

// Expõe o ponto de entrada para os testes HTTP com WebApplicationFactory.
public partial class Program { }
