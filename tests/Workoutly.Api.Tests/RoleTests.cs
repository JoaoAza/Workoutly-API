using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Data;
using Microsoft.AspNetCore.TestHost;

namespace Workoutly.Api.Tests;

public sealed class RoleTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Public_signup_creates_the_selected_role_and_the_server_authorizes_using_the_persisted_role()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        using var student = app.CreateClient();
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        var studentEmail = $"{Guid.NewGuid():N}@example.test";
        var registered = await student.PostAsJsonAsync("/auth/register", new
        {
            name = "Aluno Perfil", email = studentEmail, password = Password, role = "student"
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var session = (await registered.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal("student", session.User.Role);
        student.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal("student", (await student.GetFromJsonAsync<UserBody>("/auth/me"))?.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/teaching/students")).StatusCode);

        var teacherEmail = $"{Guid.NewGuid():N}@example.test";
        using var teacher = app.CreateClient();
        var teacherRegistration = await teacher.PostAsJsonAsync("/auth/register", new
        {
            name = "Professor Perfil", email = teacherEmail, password = Password, role = "teacher"
        });
        Assert.Equal(HttpStatusCode.Created, teacherRegistration.StatusCode);
        var teacherSession = (await teacherRegistration.Content.ReadFromJsonAsync<SessionBody>())!;
        Assert.Equal("teacher", teacherSession.User.Role);
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", teacherSession.AccessToken);
        Assert.Equal("teacher", (await teacher.GetFromJsonAsync<UserBody>("/auth/me"))?.Role);
        Assert.Empty((await teacher.GetFromJsonAsync<UserBody[]>("/teaching/students"))!);

        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restoredTeacher = restarted.CreateClient();
        restoredTeacher.DefaultRequestHeaders.Authorization = new("Bearer", teacherSession.AccessToken);
        Assert.Equal("teacher", (await restoredTeacher.GetFromJsonAsync<UserBody>("/auth/me"))?.Role);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/teaching/students")).StatusCode);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Enrollment_is_student_initiated_persists_and_is_visible_only_to_the_linked_teacher()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        async Task<(HttpClient Client, SessionBody Session)> Teacher()
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            Assert.Equal(0, await CreateTeacher(email));
            var client = app.CreateClient();
            var response = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
            var session = (await response.Content.ReadFromJsonAsync<SessionBody>())!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
            return (client, session);
        }
        var first = await Teacher();
        var second = await Teacher();
        using var teacher = first.Client;
        using var otherTeacher = second.Client;
        using var student = app.CreateClient();
        var registered = await student.PostAsJsonAsync("/auth/register", new { name = "Aluno Vinculado", email = $"{Guid.NewGuid():N}@example.test", password = Password });
        var studentSession = (await registered.Content.ReadFromJsonAsync<SessionBody>())!;
        student.DefaultRequestHeaders.Authorization = new("Bearer", studentSession.AccessToken);
        Assert.Null((await student.GetFromJsonAsync<EnrollmentBody>("/enrollment"))?.Teacher);
        var invitationResponse = await teacher.PostAsync("/teaching/invitation", null);
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitation = (await invitationResponse.Content.ReadFromJsonAsync<InvitationBody>())!;
        Assert.Equal(invitation, await teacher.GetFromJsonAsync<InvitationBody>("/teaching/invitation"));
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync("/teaching/invitation", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync("/enrollment", new { code = invitation.Code })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync("/enrollment", new { code = "000000000000" })).StatusCode);
        var joined = await student.PostAsJsonAsync("/enrollment", new { code = invitation.Code!.ToLowerInvariant(), studentId = first.Session.User.Id });
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        Assert.Equal(first.Session.User, (await joined.Content.ReadFromJsonAsync<EnrollmentBody>())?.Teacher);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/enrollment", new { code = invitation.Code })).StatusCode);
        Assert.Equal(studentSession.User, Assert.Single((await teacher.GetFromJsonAsync<UserBody[]>("/teaching/students"))!));
        Assert.Empty((await otherTeacher.GetFromJsonAsync<UserBody[]>("/teaching/students"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.DeleteAsync($"/teaching/students/{studentSession.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.GetAsync($"/teaching/students/{studentSession.User.Id}")).StatusCode);
        Assert.Equal(studentSession.User, await teacher.GetFromJsonAsync<UserBody>($"/teaching/students/{studentSession.User.Id}"));

        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = new("Bearer", studentSession.AccessToken);
        Assert.Equal(first.Session.User, (await restored.GetFromJsonAsync<EnrollmentBody>("/enrollment"))?.Teacher);
        var renewed = (await (await teacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!;
        Assert.NotEqual(invitation.Code, renewed.Code);
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync("/enrollment", new { code = invitation.Code })).StatusCode);
        var otherCode = (await (await otherTeacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync("/enrollment", new { code = otherCode.Code })).StatusCode);
        Assert.Equal(first.Session.User, (await student.GetFromJsonAsync<EnrollmentBody>("/enrollment"))?.Teacher);
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync("/enrollment")).StatusCode);
        Assert.Empty((await teacher.GetFromJsonAsync<UserBody[]>("/teaching/students"))!);
        Assert.Equal(HttpStatusCode.Created, (await student.PostAsJsonAsync("/enrollment", new { code = otherCode.Code })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await otherTeacher.DeleteAsync($"/teaching/students/{studentSession.User.Id}")).StatusCode);
        Assert.Null((await student.GetFromJsonAsync<EnrollmentBody>("/enrollment"))?.Teacher);
    }

    [PostgresFact]
    [Trait("Category", "Postgres")]
    public async Task Expired_invitation_is_not_offered_or_accepted_by_a_new_session()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(0, await CreateTeacher(email));
        using var teacher = app.CreateClient();
        var login = await teacher.PostAsJsonAsync("/auth/login", new { email, password = Password });
        teacher.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<SessionBody>())!.AccessToken);
        var code = (await (await teacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!.Code;
        await using var later = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new LaterTime())));
        using var client = later.CreateClient();
        login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<SessionBody>())!.AccessToken);
        Assert.Null((await client.GetFromJsonAsync<InvitationBody>("/teaching/invitation"))?.Code);
        var registered = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Posterior", email = $"{Guid.NewGuid():N}@example.test", password = Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await registered.Content.ReadFromJsonAsync<SessionBody>())!.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/enrollment", new { code })).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<EnrollmentBody>("/enrollment"))?.Teacher);
    }

    private sealed class LaterTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(8);
    }

    private sealed record EnrollmentBody(UserBody? Teacher);
    private sealed record InvitationBody(string? Code, DateTimeOffset? ExpiresAt);
    private const string Password = "Treino2026!seguro";
    private sealed record UserBody(string Id, string Name, string Email, string Role);
    private sealed record SessionBody(string AccessToken, UserBody User);

    private async Task<int> CreateTeacher(string email)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Workoutly.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = root.FullName
        };
        start.ArgumentList.Add(Path.Combine(root.FullName, "src/Workoutly.Api/bin/Debug/net10.0/Workoutly.Api.dll"));
        start.ArgumentList.Add("--create-teacher");
        start.Environment["ConnectionStrings__Workoutly"] = mysql.ConnectionString;
        start.Environment["TeacherSetup__Name"] = "Professor Teste";
        start.Environment["TeacherSetup__Email"] = email;
        start.Environment["TeacherSetup__Password"] = Password;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(timeout.Token);
        var output = await stdout + await stderr;
        Assert.DoesNotContain(Password, output);
        return process.ExitCode;
    }
}
