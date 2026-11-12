using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Workoutly.Api.Auth;
using Workoutly.Api.Data;

namespace Workoutly.Api.Tests;

public sealed class PlanTests(PostgresFixture mysql) : IClassFixture<PostgresFixture>
{
    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Student_can_save_ordered_plan_edit_with_revision_and_delete_without_exposing_other_accounts()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        using var other = await Student(app);
        var exercises = (await student.GetFromJsonAsync<ExerciseBody[]>("/exercises"))!;
        var id = Guid.NewGuid().ToString();
        var payload = new { id, name = " Treino A ", notes = " Peito e costas ", exercises = new[] { new { exerciseId = exercises[1].Id, sets = 3, repetitions = 12 }, new { exerciseId = exercises[0].Id, sets = 4, repetitions = 10 } } };
        var created = await student.PostAsJsonAsync("/plans", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var plan = (await created.Content.ReadFromJsonAsync<PlanBody>())!;
        Assert.Equal("Treino A", plan.Name);
        Assert.Equal("Peito e costas", plan.Notes);
        Assert.Equal(1, plan.Version);
        Assert.Equal(new[] { exercises[1].Id, exercises[0].Id }, plan.Exercises.Select(item => item.ExerciseId));
        Assert.Equal(3, plan.Exercises[0].Sets);
        Assert.Equal(12, plan.Exercises[0].Repetitions);
        // Reenvio após perda da resposta não cria outra ficha.
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/plans", payload)).StatusCode);
        Assert.Single((await student.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        Assert.Empty((await other.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/plans/{id}")).StatusCode);
        var update = new { version = 1, name = "Treino A revisado", notes = "", exercises = new[] { new { exerciseId = exercises[0].Id, sets = 2, repetitions = 15 } } };
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/plans/{id}", update)).StatusCode);
        var edited = await student.PutAsJsonAsync($"/plans/{id}", update);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal(2, (await edited.Content.ReadFromJsonAsync<PlanBody>())!.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await student.PutAsJsonAsync($"/plans/{id}", update)).StatusCode);
        var original = (await student.GetFromJsonAsync<PlanBody>($"/plans/{id}?version=1"))!;
        Assert.Equal("Treino A", original.Name);
        Assert.Equal(2, original.Exercises.Length);
        await using var restarted = new DatabaseApplication(mysql.ConnectionString);
        using var restored = restarted.CreateClient();
        restored.DefaultRequestHeaders.Authorization = student.DefaultRequestHeaders.Authorization;
        Assert.Equal("Treino A revisado", (await restored.GetFromJsonAsync<PlanBody>($"/plans/{id}"))!.Name);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/plans/{id}?version=2")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.DeleteAsync($"/plans/{id}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync($"/plans/{id}?version=2")).StatusCode);
        Assert.Empty((await student.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/plans/{id}")).StatusCode);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/plans")).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Teacher_can_manage_only_linked_student_plans_and_loses_access_immediately_after_unlink()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        using var stranger = await Student(app);
        var studentId = (await student.GetFromJsonAsync<AccountProfile>("/auth/me"))!.Id;
        async Task<HttpClient> Teacher()
        {
            var email = $"{Guid.NewGuid():N}@example.test";
            await app.Services.GetRequiredService<AuthService>().CreateTeacherAsync("Professor Ficha", email, "Treino2026!seguro", default);
            var client = app.CreateClient();
            var login = await client.PostAsJsonAsync("/auth/login", new { email, password = "Treino2026!seguro" });
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<SessionBody>())!.AccessToken);
            return client;
        }
        using var teacher = await Teacher();
        using var otherTeacher = await Teacher();
        var path = $"/teaching/students/{studentId}/plans";
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/plans")).StatusCode);
        var invite = await teacher.PostAsync("/teaching/invitation", null);
        var code = (await invite.Content.ReadFromJsonAsync<InvitationBody>())!.Code;
        await student.PostAsJsonAsync("/enrollment", new { code });
        var exercise = await teacher.PostAsJsonAsync("/exercises", new { name = "Movimento privado do professor", muscleGroup = "Costas", equipment = "Halter", description = "Instrução compartilhada somente nesta ficha" });
        var exerciseId = (await exercise.Content.ReadFromJsonAsync<ExerciseBody>())!.Id;
        var id = Guid.NewGuid().ToString();
        var payload = new { id, name = "Ficha do professor", notes = "Plano do aluno", studentId = Guid.NewGuid(), exercises = new[] { new { exerciseId, sets = 3, repetitions = 10, name = "Nome adulterado" } } };
        Assert.Equal(HttpStatusCode.Created, (await teacher.PostAsJsonAsync(path, payload)).StatusCode);
        var plan = (await student.GetFromJsonAsync<PlanBody>($"/plans/{id}"))!;
        Assert.Equal(studentId, plan.StudentId);
        Assert.Equal("Movimento privado do professor", plan.Exercises[0].Name);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/exercises/{exerciseId}")).StatusCode);
        Assert.Empty((await stranger.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.PostAsJsonAsync(path, payload)).StatusCode);
        var update = new { version = 1, name = "Ajustada pelo aluno", notes = "", exercises = new[] { new { exerciseId, sets = 4, repetitions = 8 } } };
        Assert.Equal(HttpStatusCode.NotFound, (await otherTeacher.PutAsJsonAsync($"{path}/{id}", update)).StatusCode);
        // O aluno pode ajustar o item recebido sem receber acesso ao catálogo privado do professor.
        Assert.Equal(HttpStatusCode.OK, (await student.PutAsJsonAsync($"/plans/{id}", update)).StatusCode);
        Assert.Equal(4, (await teacher.GetFromJsonAsync<PlanBody>($"{path}/{id}"))!.Exercises[0].Sets);
        Assert.Equal(HttpStatusCode.NoContent, (await student.DeleteAsync("/enrollment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"{path}/{id}?version=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.PutAsJsonAsync($"{path}/{id}", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.DeleteAsync($"{path}/{id}?version=2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.PostAsJsonAsync(path, payload)).StatusCode);
        Assert.Equal("Ajustada pelo aluno", (await student.GetFromJsonAsync<PlanBody>($"/plans/{id}"))!.Name);
        // Outro professor passa a ter acesso somente após um novo vínculo aceito pelo aluno.
        code = (await (await otherTeacher.PostAsync("/teaching/invitation", null)).Content.ReadFromJsonAsync<InvitationBody>())!.Code;
        await student.PostAsJsonAsync("/enrollment", new { code });
        Assert.Single((await otherTeacher.GetFromJsonAsync<PlanBody[]>(path))!);
        Assert.Equal(HttpStatusCode.NoContent, (await otherTeacher.DeleteAsync($"{path}/{id}?version=2")).StatusCode);
    }

    [PostgresFact, Trait("Category", "Postgres")]
    public async Task Invalid_or_private_exercises_are_rejected_atomically_and_concurrent_edits_do_not_overwrite()
    {
        await using var app = new DatabaseApplication(mysql.ConnectionString);
        await app.Services.GetRequiredService<PostgresDatabase>().MigrateAsync();
        using var student = await Student(app);
        using var other = await Student(app);
        var exerciseId = (await student.GetFromJsonAsync<ExerciseBody[]>("/exercises"))![0].Id;
        var privateResponse = await other.PostAsJsonAsync("/exercises", new { name = "Privado alheio", muscleGroup = "Pernas", equipment = "Halter", description = "" });
        var privateId = (await privateResponse.Content.ReadFromJsonAsync<ExerciseBody>())!.Id;
        object Item(string id, int sets = 3, int repetitions = 10) => new { exerciseId = id, sets, repetitions };
        async Task Invalid(object[]? items, string name = "Treino válido", string notes = "")
        {
            var result = await student.PostAsJsonAsync("/plans", new { id = Guid.NewGuid(), name, notes, exercises = items });
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
        await Invalid(null);
        await Invalid([]);
        await Invalid([Item(exerciseId)], " ");
        await Invalid([Item(exerciseId)], notes: new string('a', 1001));
        await Invalid([Item(exerciseId, 0)]);
        await Invalid([Item(exerciseId, 21)]);
        await Invalid([Item(exerciseId, repetitions: 0)]);
        await Invalid([Item(exerciseId, repetitions: 201)]);
        await Invalid([Item(exerciseId), Item(exerciseId)]);
        await Invalid([Item(Guid.NewGuid().ToString())]);
        await Invalid([Item(privateId)]);
        await Invalid([Item(exerciseId), Item(privateId)]);
        Assert.Empty((await student.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        var id = Guid.NewGuid();
        var body = new { id, name = "Ficha concorrente", notes = "", exercises = new[] { Item(exerciseId) } };
        var creations = await Task.WhenAll(student.PostAsJsonAsync("/plans", body), student.PostAsJsonAsync("/plans", body));
        Assert.Contains(creations, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Contains(creations, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single((await student.GetFromJsonAsync<PlanBody[]>("/plans"))!);
        var invalidUpdate = new { version = 1, name = "Não persistir", notes = "", exercises = new[] { Item(privateId) } };
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PutAsJsonAsync($"/plans/{id}", invalidUpdate)).StatusCode);
        Assert.Equal("Ficha concorrente", (await student.GetFromJsonAsync<PlanBody>($"/plans/{id}"))!.Name);
        var update = new { version = 1, name = "Edição concorrente", notes = "", exercises = new[] { Item(exerciseId) } };
        var results = await Task.WhenAll(student.PutAsJsonAsync($"/plans/{id}", update), student.PutAsJsonAsync($"/plans/{id}", update));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, (await student.GetFromJsonAsync<PlanBody>($"/plans/{id}"))!.Version);
    }

    private sealed record InvitationBody(string Code);

    private static async Task<HttpClient> Student(DatabaseApplication app)
    {
        var client = app.CreateClient();
        var registered = await client.PostAsJsonAsync("/auth/register", new { name = "Aluno Ficha", email = $"{Guid.NewGuid():N}@example.test", password = "Treino2026!seguro" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await registered.Content.ReadFromJsonAsync<SessionBody>())!.AccessToken);
        return client;
    }

    private sealed record SessionBody(string AccessToken, AccountProfile User);
    private sealed record ExerciseBody(string Id, string Name);
    private sealed record ItemBody(string ExerciseId, string Name, string MuscleGroup, string Equipment, string Description, int Sets, int Repetitions);
    private sealed record PlanBody(string Id, string StudentId, string Name, string Notes, int Version, ItemBody[] Exercises);
}
