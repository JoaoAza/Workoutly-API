using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Workoutly.Mobile.Services;

public sealed record AccountProfile(string Id, string Name, string Email, string Role = "student");
public sealed record TeacherInvitation(string? Code, DateTimeOffset? ExpiresAt);
public sealed record EnrollmentInfo(AccountProfile? Teacher);
public sealed record JoinTeacherPayload(string Code);
public sealed record AccountSession(string AccessToken, DateTimeOffset ExpiresAt, AccountProfile User);
public sealed record LoginPayload(string Email, string Password);
public sealed record RegisterPayload(string Name, string Email, string Password, string Role);
public sealed record StoredSession(string ApiUrl, string Token);
public sealed record ExerciseItem(string Id, string Name, string MuscleGroup, string Equipment, string Description, bool IsShared, bool IsOwned = false, bool IsInPlan = false)
{
    public string Origin => IsOwned ? "Meu exercício" : IsInPlan ? "Na minha ficha" : "Catálogo";
    public bool IsMine => IsOwned || IsInPlan;
    public string Summary => $"{MuscleGroup} • {Equipment}";
    public ExerciseVisual Visual => ExerciseVisuals.Find(Id, Name);
    public string? ImageSource => Visual?.ImageSource;
    public string? QuickInstruction => Visual?.QuickInstruction;
}
public sealed record CreateExercisePayload(string Name, string MuscleGroup, string Equipment, string Description);

public sealed record PlanExerciseItem(string ExerciseId, string Name, string MuscleGroup, string Equipment, string Description, int Sets, int Repetitions);
public sealed record PlanItem(string Id, string StudentId, string Name, string Notes, int Version, PlanExerciseItem[] Exercises)
{
    public string Summary => $"{Exercises.Length} exercício(s) • {Exercises.Sum(item => item.Sets)} séries previstas";
}
public sealed record PlanExercisePayload(string ExerciseId, int Sets, int Repetitions);
public sealed record CreatePlanPayload(string Id, string Name, string Notes, PlanExercisePayload[] Exercises);
public sealed record UpdatePlanPayload(int Version, string Name, string Notes, PlanExercisePayload[] Exercises);

public sealed record WorkoutSetItem(int Repetitions, decimal WeightKg);
public sealed record WorkoutExercisePayload(string ExerciseId, WorkoutSetItem[] Sets);
public sealed record CreateWorkoutPayload(string Id, string PlanId, int PlanVersion, DateTimeOffset StartedAt, int DurationSeconds, string Notes, WorkoutExercisePayload[] Exercises);
public sealed record UpdateWorkoutPayload(string EditId, int Version, int DurationSeconds, string Notes, WorkoutExercisePayload[] Exercises);
public sealed record WorkoutExerciseItem(string ExerciseId, string Name, string MuscleGroup, string Equipment, string Description, int PlannedSets, int PlannedRepetitions, WorkoutSetItem[] Sets);
public sealed record WorkoutSessionItem(string Id, string StudentId, string PlanId, int PlanVersion, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, string Notes, WorkoutExerciseItem[] Exercises, int Version = 1, PlanExerciseItem[]? PlanExercises = null, decimal VolumeKg = 0);
public sealed record WorkoutSummaryItem(string Id, string PlanName, DateTimeOffset StartedAt, int DurationSeconds, int ExerciseCount, int SetCount, decimal VolumeKg = 0)
{
    public string DateLabel => StartedAt.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm");
    public string Summary => $"{ExerciseCount} exercício(s) • {SetCount} série(s) • {FormatDuration(DurationSeconds)}\nVolume de carga: {VolumeKg:0.##} kg";
    public static string FormatDuration(int seconds) => $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
}

public sealed record WorkoutDayItem(DateOnly Date, int SessionCount);
public sealed record LoadPointItem(string SessionId, DateTimeOffset StartedAt, decimal MaxWeightKg, int SetCount, long RepetitionCount, decimal VolumeKg);
public sealed record ExerciseEvolutionItem(string ExerciseId, string Name, LoadPointItem[] Points);
public sealed record WorkoutHistoryItem(DateOnly From, DateOnly To, int OffsetMinutes, int SessionCount, int ActiveDays,
    int SetCount, long RepetitionCount, long DurationSeconds, decimal VolumeKg,
    WorkoutDayItem[] Days, ExerciseEvolutionItem[] Exercises, WorkoutSummaryItem[] Sessions);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(WorkoutHistoryItem))]
[JsonSerializable(typeof(CreateWorkoutPayload))]
[JsonSerializable(typeof(UpdateWorkoutPayload))]
[JsonSerializable(typeof(WorkoutSessionItem))]
[JsonSerializable(typeof(WorkoutSummaryItem[]))]
[JsonSerializable(typeof(PlanItem))]
[JsonSerializable(typeof(PlanItem[]))]
[JsonSerializable(typeof(CreatePlanPayload))]
[JsonSerializable(typeof(UpdatePlanPayload))]
[JsonSerializable(typeof(AccountProfile))]
[JsonSerializable(typeof(AccountProfile[]))]
[JsonSerializable(typeof(TeacherInvitation))]
[JsonSerializable(typeof(EnrollmentInfo))]
[JsonSerializable(typeof(JoinTeacherPayload))]
[JsonSerializable(typeof(AccountSession))]
[JsonSerializable(typeof(LoginPayload))]
[JsonSerializable(typeof(RegisterPayload))]
[JsonSerializable(typeof(StoredSession))]
[JsonSerializable(typeof(ExerciseItem))]
[JsonSerializable(typeof(ExerciseItem[]))]
[JsonSerializable(typeof(CreateExercisePayload))]
internal partial class AccountJson : JsonSerializerContext;

public sealed class AccountClient
{
    private const string SessionKey = "workoutly.account.session";
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(120) };
    private string? _token;
    public string ApiUrl { get; private set; } = Preferences.Default.Get("workoutly.api_url", DefaultApiUrl);

    private static string DefaultApiUrl
    {
        get
        {
#if DEBUG
            return DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080/" : "http://localhost:5080/";
#else
            return string.Empty;
#endif
        }
    }

    public void ConfigureServer(string address)
    {
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new AccountException("Informe um endereço válido para o serviço.");
#if !DEBUG
        if (uri.Scheme != "https")
            throw new AccountException("A conexão com sua conta precisa usar HTTPS.");
#endif
        var normalized = uri.AbsoluteUri.TrimEnd('/') + "/";
        if (normalized != ApiUrl)
            ClearSession();
        ApiUrl = normalized;
        Preferences.Default.Set("workoutly.api_url", normalized);
    }

    public async Task<AccountProfile?> RestoreAsync()
    {
        if (!string.IsNullOrWhiteSpace(ApiUrl)) ConfigureServer(ApiUrl);
        StoredSession? stored;
        try
        {
            var value = await SecureStorage.Default.GetAsync(SessionKey);
            stored = value is null ? null : JsonSerializer.Deserialize(value, AccountJson.Default.StoredSession);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ClearSession();
            throw new AccountException("Não foi possível recuperar a sessão protegida. Entre novamente.");
        }
        if (stored is null || stored.ApiUrl != ApiUrl)
            return null;
        _token = stored.Token;
        try { return await GetProfileAsync(); }
        catch (SessionExpiredException) { return null; }
    }

    public async Task<AccountProfile> LoginAsync(string email, string password)
    {
        using var content = JsonContent.Create(new LoginPayload(email.Trim(), password), AccountJson.Default.LoginPayload);
        var session = await SendAsync(HttpMethod.Post, "auth/login", content, AccountJson.Default.AccountSession, false);
        return await StoreSessionAsync(session);
    }

    public async Task<AccountProfile> RegisterAsync(string name, string email, string password, string role)
    {
        using var content = JsonContent.Create(new RegisterPayload(name.Trim(), email.Trim(), password, role), AccountJson.Default.RegisterPayload);
        var session = await SendAsync(HttpMethod.Post, "auth/register", content, AccountJson.Default.AccountSession, false);
        return await StoreSessionAsync(session);
    }

    public Task<AccountProfile> GetProfileAsync() => SendAsync(HttpMethod.Get, "auth/me", null, AccountJson.Default.AccountProfile, true);

    public Task<AccountProfile[]> GetStudentsAsync() => SendAsync(HttpMethod.Get, "teaching/students", null, AccountJson.Default.AccountProfileArray, true);
    public Task<TeacherInvitation> GetInvitationAsync() => SendAsync(HttpMethod.Get, "teaching/invitation", null, AccountJson.Default.TeacherInvitation, true);
    public Task<TeacherInvitation> CreateInvitationAsync() => SendAsync(HttpMethod.Post, "teaching/invitation", null, AccountJson.Default.TeacherInvitation, true);
    public Task<EnrollmentInfo> GetEnrollmentAsync() => SendAsync(HttpMethod.Get, "enrollment", null, AccountJson.Default.EnrollmentInfo, true);
    public async Task<EnrollmentInfo> JoinTeacherAsync(string code)
    {
        using var content = JsonContent.Create(new JoinTeacherPayload(code.Trim()), AccountJson.Default.JoinTeacherPayload);
        return await SendAsync(HttpMethod.Post, "enrollment", content, AccountJson.Default.EnrollmentInfo, true);
    }
    public async Task RemoveStudentAsync(string id)
    {
        using var response = await RequestAsync(HttpMethod.Delete, $"teaching/students/{Uri.EscapeDataString(id)}", null, true);
    }
    public async Task LeaveTeacherAsync()
    {
        using var response = await RequestAsync(HttpMethod.Delete, "enrollment", null, true);
    }

    public Task<ExerciseItem[]> GetExercisesAsync() => SendAsync(HttpMethod.Get, "exercises", null, AccountJson.Default.ExerciseItemArray, true);

    public async Task<ExerciseItem> CreateExerciseAsync(string name, string muscleGroup, string equipment, string description)
    {
        using var content = JsonContent.Create(new CreateExercisePayload(name, muscleGroup, equipment, description), AccountJson.Default.CreateExercisePayload);
        return await SendAsync(HttpMethod.Post, "exercises", content, AccountJson.Default.ExerciseItem, true);
    }

    private static string PlansPath(string? studentId) => studentId is null ? "plans" : $"teaching/students/{Uri.EscapeDataString(studentId)}/plans";
    public Task<PlanItem[]> GetPlansAsync(string? studentId) => SendAsync(HttpMethod.Get, PlansPath(studentId), null, AccountJson.Default.PlanItemArray, true);
    public Task<PlanItem> GetPlanAsync(string? studentId, string id) => SendAsync(HttpMethod.Get, $"{PlansPath(studentId)}/{Uri.EscapeDataString(id)}", null, AccountJson.Default.PlanItem, true);
    public async Task<PlanItem> SavePlanAsync(string? studentId, string id, int version, string name, string notes, PlanExercisePayload[] exercises)
    {
        using var content = version == 0
            ? JsonContent.Create(new CreatePlanPayload(id, name, notes, exercises), AccountJson.Default.CreatePlanPayload)
            : JsonContent.Create(new UpdatePlanPayload(version, name, notes, exercises), AccountJson.Default.UpdatePlanPayload);
        return await SendAsync(version == 0 ? HttpMethod.Post : HttpMethod.Put,
            version == 0 ? PlansPath(studentId) : $"{PlansPath(studentId)}/{Uri.EscapeDataString(id)}", content, AccountJson.Default.PlanItem, true);
    }
    public async Task DeletePlanAsync(string? studentId, string id, int version)
    {
        using var response = await RequestAsync(HttpMethod.Delete, $"{PlansPath(studentId)}/{Uri.EscapeDataString(id)}?version={version}", null, true);
    }

    public Task<WorkoutSummaryItem[]> GetWorkoutsAsync() => SendAsync(HttpMethod.Get, "sessions", null, AccountJson.Default.WorkoutSummaryItemArray, true);
    public Task<WorkoutSessionItem> GetWorkoutAsync(string id, string? studentId = null) => SendAsync(HttpMethod.Get,
        studentId is null ? $"sessions/{Uri.EscapeDataString(id)}" : $"teaching/students/{Uri.EscapeDataString(studentId)}/sessions/{Uri.EscapeDataString(id)}",
        null, AccountJson.Default.WorkoutSessionItem, true);
    public Task<WorkoutHistoryItem> GetHistoryAsync(DateOnly from, DateOnly to, int offsetMinutes, string? studentId = null)
    {
        var path = studentId is null ? "history" : $"teaching/students/{Uri.EscapeDataString(studentId)}/history";
        return SendAsync(HttpMethod.Get, FormattableString.Invariant($"{path}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&offsetMinutes={offsetMinutes}"), null, AccountJson.Default.WorkoutHistoryItem, true);
    }
    public async Task<WorkoutSessionItem> SaveWorkoutAsync(CreateWorkoutPayload payload)
    {
        using var content = JsonContent.Create(payload, AccountJson.Default.CreateWorkoutPayload);
        return await SendAsync(HttpMethod.Post, "sessions", content, AccountJson.Default.WorkoutSessionItem, true);
    }

    public async Task<WorkoutSessionItem> UpdateWorkoutAsync(string id, UpdateWorkoutPayload payload)
    {
        using var content = JsonContent.Create(payload, AccountJson.Default.UpdateWorkoutPayload);
        return await SendAsync(HttpMethod.Put, $"sessions/{Uri.EscapeDataString(id)}", content, AccountJson.Default.WorkoutSessionItem, true);
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var response = await RequestAsync(HttpMethod.Post, "auth/logout", null, true);
        }
        catch (SessionExpiredException) { }
        ClearSession();
    }

    private async Task<AccountProfile> StoreSessionAsync(AccountSession session)
    {
        _token = session.AccessToken;
        try
        {
            var value = JsonSerializer.Serialize(new StoredSession(ApiUrl, session.AccessToken), AccountJson.Default.StoredSession);
            await SecureStorage.Default.SetAsync(SessionKey, value);
            return session.User;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            try { await LogoutAsync(); } catch (AccountException) { ClearSession(); }
            throw new AccountException("A conta foi validada, mas não foi possível guardar a sessão protegida. Tente entrar novamente.");
        }
    }

    private void ClearSession()
    {
        _token = null;
        try { SecureStorage.Default.Remove(SessionKey); }
        catch (Exception exception) when (exception is not OutOfMemoryException) { }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, JsonTypeInfo<T> type, bool authenticated)
    {
        using var response = await RequestAsync(method, path, content, authenticated);
        try
        {
            return await response.Content.ReadFromJsonAsync(type) ?? throw new AccountException("O serviço enviou uma resposta vazia.");
        }
        catch (JsonException) { throw new AccountException("Não foi possível compreender a resposta do serviço."); }
    }

    private async Task<HttpResponseMessage> RequestAsync(HttpMethod method, string path, HttpContent? content, bool authenticated)
    {
        if (string.IsNullOrWhiteSpace(ApiUrl))
            throw new AccountException("Configure o endereço do serviço em Conexão.");
        using var request = new HttpRequestMessage(method, new Uri(new Uri(ApiUrl), path)) { Content = content };
        if (authenticated && _token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        HttpResponseMessage response;
        try { response = await _http.SendAsync(request); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new AccountException("Não foi possível conectar ao serviço. Confira sua conexão e tente novamente. Seus campos foram preservados.");
        }
        if (response.IsSuccessStatusCode)
            return response;
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (authenticated) { ClearSession(); throw new SessionExpiredException(); }
                throw new AccountException("E-mail ou senha inválidos.");
            }
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new AccountException(path == "auth/register" ? "Este e-mail já está cadastrado. Use Entrar." : path == "enrollment" ? "Você já tem um professor vinculado. Remova o vínculo atual antes de trocar." : path.StartsWith("sessions/", StringComparison.Ordinal) ? "Este treino já foi alterado. Seus campos foram preservados. Recarregue o registro para corrigir a versão atual." : path == "sessions" ? "Esta tentativa já corresponde a outro conteúdo salvo. Consulte os treinos salvos antes de registrar novamente." : "A ficha já foi salva, alterada ou excluída. Seus campos foram preservados. Volte à lista ou recarregue a ficha para abrir a versão atual.", HttpStatusCode.Conflict);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new AccountException("Seu perfil não tem permissão para esta ação. Volte à sua conta e atualize o acesso.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new AccountException("Registro não encontrado ou não disponível para sua conta. Atualize a tela.");
            if ((int)response.StatusCode == 429)
                throw new AccountException("Muitas tentativas. Aguarde um minuto e tente novamente.");
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                try
                {
                    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (body.RootElement.TryGetProperty("errors", out var errors))
                        throw new AccountException(string.Join("\n", errors.EnumerateObject().SelectMany(field => field.Value.EnumerateArray()).Select(value => value.GetString())), HttpStatusCode.BadRequest);
                }
                catch (JsonException) { }
                throw new AccountException("Revise os dados informados e o endereço do serviço.", HttpStatusCode.BadRequest);
            }
            throw new AccountException("Serviço indisponível. Tente novamente em instantes.");
        }
    }
}

public class AccountException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
public sealed class SessionExpiredException() : AccountException("Sua sessão expirou. Entre novamente.");
