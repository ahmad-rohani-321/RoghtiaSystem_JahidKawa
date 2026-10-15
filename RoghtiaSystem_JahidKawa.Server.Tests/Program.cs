using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// This executable exercises the real HTTP pipeline and a fresh, encrypted database.
// It intentionally has no test framework or server package dependencies.
var repository = FindRepository();
var serverAssembly = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(repository, "RoghtiaSystem_JahidKawa.Server", "bin", "Debug", "net10.0",
        "RoghtiaSystem_JahidKawa.Server.dll");

if (!File.Exists(serverAssembly))
{
    Console.Error.WriteLine("Build the server first: dotnet build RoghtiaSystem_JahidKawa.Server -p:BuildProjectReferences=false");
    return 1;
}

var passed = 0;
try
{
    await using var server = await TestServer.StartAsync(repository, serverAssembly, permitLimit: 100);
    var suffix = Guid.NewGuid().ToString("N")[..10];
    var userName = $"Clinician_{suffix}";
    const string password = "Secure🔒پټنوم123";
    const string pinCode = "123456";
    string? sessionToken = null;

    await Check("Anonymous requests cannot access the current user", async () =>
    {
        using var response = await server.Client.GetAsync("/api/auth/me");
        ExpectStatus(response, HttpStatusCode.Unauthorized);
    });

    await Check("Registration initializes a fresh encrypted database and returns a safe user", async () =>
    {
        var response = await Post(server.Client, "/api/auth/register", new { userName, password, pinCode });
        ExpectStatusCode(response.Status, HttpStatusCode.OK, response.Body);
        sessionToken = ValidateAuthResponse(response.Json, userName);
        Expect(File.Exists(server.DatabasePath), "The isolated database was not created.");
        var header = new byte[16];
        await using var database = File.Open(server.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await database.ReadExactlyAsync(header);
        Expect(Encoding.ASCII.GetString(header) != "SQLite format 3\0", "The test database is not encrypted.");
    });

    await Check("Duplicate usernames are rejected regardless of case", async () =>
    {
        await ExpectError(server.Client, "/api/auth/register",
            new { userName = userName.ToUpperInvariant(), password, pinCode }, HttpStatusCode.Conflict, "USERNAME_TAKEN");
    });

    await Check("Unicode canonical equivalents cannot create duplicate users", async () =>
    {
        var canonicalName = $"Tést_{suffix}";
        var first = await Post(server.Client, "/api/auth/register", new { userName = canonicalName, password, pinCode });
        ExpectStatusCode(first.Status, HttpStatusCode.OK, first.Body);
        await ExpectError(server.Client, "/api/auth/register",
            new { userName = $"TE\u0301ST_{suffix.ToUpperInvariant()}", password, pinCode },
            HttpStatusCode.Conflict, "USERNAME_TAKEN");
    });

    await Check("Simultaneous registrations create exactly one account", async () =>
    {
        var raceName = $"Race_{suffix}";
        var results = await Task.WhenAll(
            Post(server.Client, "/api/auth/register", new { userName = raceName, password, pinCode }),
            Post(server.Client, "/api/auth/register", new { userName = raceName.ToUpperInvariant(), password, pinCode }));
        Expect(results.Count(result => result.Status == HttpStatusCode.OK) == 1,
            "Concurrent registration did not produce exactly one success: " + string.Join(" | ", results.Select(result => result.Body)));
        var duplicate = results.Single(result => result.Status != HttpStatusCode.OK);
        ExpectStatusCode(duplicate.Status, HttpStatusCode.Conflict, duplicate.Body);
        ExpectErrorCode(duplicate.Json, "USERNAME_TAKEN");
    });

    await Check("Login accepts the original Unicode password and a case-insensitive username", async () =>
    {
        var response = await Post(server.Client, "/api/auth/login",
            new { userName = userName.ToLowerInvariant(), password, rememberMe = false });
        ExpectStatusCode(response.Status, HttpStatusCode.OK, response.Body);
        sessionToken = ValidateAuthResponse(response.Json, userName);
        ExpectLifetime(response.Json, TimeSpan.FromDays(1));
    });

    await Check("Remember Me receives the configured 30-day token lifetime", async () =>
    {
        var response = await Post(server.Client, "/api/auth/login", new { userName, password, rememberMe = true });
        ExpectStatusCode(response.Status, HttpStatusCode.OK, response.Body);
        ValidateAuthResponse(response.Json, userName);
        ExpectLifetime(response.Json, TimeSpan.FromDays(30));
    });

    await Check("Invalid credentials and unknown users return the same safe error", async () =>
    {
        await ExpectError(server.Client, "/api/auth/login", new { userName, password = "wrong-password" },
            HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        await ExpectError(server.Client, "/api/auth/login", new { userName = $"Missing_{suffix}", password },
            HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    });

    await Check("Registration validates username and password boundaries", async () =>
    {
        foreach (var invalid in new[]
        {
            new { userName = "   ", password },
            new { userName = new string('a', 65), password },
            new { userName = $"Short_{suffix}", password = "1234567" },
            new { userName = $"Long_{suffix}", password = new string('a', 129) }
        })
            await ExpectError(server.Client, "/api/auth/register", invalid,
                HttpStatusCode.BadRequest, "VALIDATION_ERROR");
    });

    await Check("A valid bearer token returns a user without credential material", async () =>
    {
        using var response = await GetUser(server.Client, sessionToken!);
        ExpectStatus(response, HttpStatusCode.OK);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        ValidateUser(json, userName);
        ExpectNoSecrets(json);
    });

    await Check("Patient codes are generated separately for each logged-in user", async () =>
    {
        var first = await AuthorizedJson(server.Client, HttpMethod.Post, "/api/patients", sessionToken!,
            new { name = "Ahmad One", phone = "0701111111", age = 35, gender = "Male", address = "Kabul" });
        ExpectStatusCode(first.Status, HttpStatusCode.Created, first.Body);
        Expect(first.Json["code"]?.GetValue<string>() == "RT-1", "The first patient code is wrong.");
        var second = await AuthorizedJson(server.Client, HttpMethod.Post, "/api/patients", sessionToken!,
            new { name = "Ahmad Two", phone = "0702222222", age = 29, gender = "Female", address = "Herat" });
        ExpectStatusCode(second.Status, HttpStatusCode.Created, second.Body);
        Expect(second.Json["code"]?.GetValue<string>() == "RT-2", "The second patient code is wrong.");

        var otherUserName = $"Other_{suffix}";
        var other = await Post(server.Client, "/api/auth/register", new { userName = otherUserName, password, pinCode });
        ExpectStatusCode(other.Status, HttpStatusCode.OK, other.Body);
        var otherToken = ValidateAuthResponse(other.Json, otherUserName);
        var otherPatient = await AuthorizedJson(server.Client, HttpMethod.Post, "/api/patients", otherToken,
            new { name = "Other Patient", phone = "0703333333", age = 41, gender = "Male", address = "Kandahar" });
        ExpectStatusCode(otherPatient.Status, HttpStatusCode.Created, otherPatient.Body);
        Expect(otherPatient.Json["code"]?.GetValue<string>() == "RT-1", "The second user should have an independent code sequence.");

        var ownCode = await AuthorizedJson(server.Client, HttpMethod.Get, "/api/patients/by-code/RT-1", sessionToken!);
        ExpectStatusCode(ownCode.Status, HttpStatusCode.OK, ownCode.Body);
        Expect(ownCode.Json["name"]?.GetValue<string>() == "Ahmad One", "Code lookup returned the wrong user's patient.");
        var secondUserCode = await AuthorizedJson(server.Client, HttpMethod.Get, "/api/patients/by-code/RT-1", otherToken);
        ExpectStatusCode(secondUserCode.Status, HttpStatusCode.OK, secondUserCode.Body);
        Expect(secondUserCode.Json["name"]?.GetValue<string>() == "Other Patient", "Code lookup crossed user ownership.");
        var inaccessibleCode = await AuthorizedJson(server.Client, HttpMethod.Get, "/api/patients/by-code/RT-2", otherToken);
        ExpectStatusCode(inaccessibleCode.Status, HttpStatusCode.NotFound, inaccessibleCode.Body);
        using var anonymous = await server.Client.GetAsync("/api/patients/by-code/RT-1");
        ExpectStatus(anonymous, HttpStatusCode.Unauthorized);

        var filtered = await AuthorizedJson(server.Client, HttpMethod.Get,
            "/api/patients?name=Ahmad&phone=070111&gender=Male", sessionToken!);
        ExpectStatusCode(filtered.Status, HttpStatusCode.OK, filtered.Body);
        Expect(filtered.Json["total"]?.GetValue<int>() == 1 &&
            filtered.Json["items"]?[0]?["name"]?.GetValue<string>() == "Ahmad One", "Combined patient filters returned incorrect results.");
        var otherList = await AuthorizedJson(server.Client, HttpMethod.Get, "/api/patients?name=Ahmad", otherToken);
        ExpectStatusCode(otherList.Status, HttpStatusCode.OK, otherList.Body);
        Expect(otherList.Json["total"]?.GetValue<int>() == 0, "A patient filter exposed another user's record.");

        var patientId = first.Json["id"]!.GetValue<int>();
        var forbiddenUpdate = await AuthorizedJson(server.Client, HttpMethod.Put, $"/api/patients/{patientId}", otherToken,
            new { name = "Changed", phone = "0701111111", age = 35, gender = "Male", address = "Kabul" });
        ExpectStatusCode(forbiddenUpdate.Status, HttpStatusCode.NotFound, forbiddenUpdate.Body);
        var unchanged = await AuthorizedJson(server.Client, HttpMethod.Get, $"/api/patients/{patientId}", sessionToken!);
        Expect(unchanged.Json["name"]?.GetValue<string>() == "Ahmad One", "A different user changed the patient.");
    });

    await Check("Tampered and expired signed tokens are rejected", async () =>
    {
        var pieces = sessionToken!.Split('.');
        pieces[2] = (pieces[2][0] == 'A' ? "B" : "A") + pieces[2][1..];
        using var tampered = await GetUser(server.Client, string.Join('.', pieces));
        ExpectStatus(tampered, HttpStatusCode.Unauthorized);

        var expiredToken = ExpireAndSignToken(sessionToken, server.SigningKey);
        using var expired = await GetUser(server.Client, expiredToken);
        ExpectStatus(expired, HttpStatusCode.Unauthorized);
    });

    await Check("Authentication throttling returns a structured 429 response", async () =>
    {
        await using var throttled = await TestServer.StartAsync(repository, serverAssembly, permitLimit: 3);
        for (var request = 0; request < 3; request++)
            await ExpectError(throttled.Client, "/api/auth/login", new { userName = "unknown", password },
                HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
        await ExpectError(throttled.Client, "/api/auth/login", new { userName = "unknown", password },
            HttpStatusCode.TooManyRequests, "TOO_MANY_REQUESTS");
    });

    Console.WriteLine($"Passed {passed} integration checks.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL after {passed} passing checks: {error}");
    return 1;
}

async Task Check(string name, Func<Task> test)
{
    await test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

static async Task<(HttpStatusCode Status, JsonNode Json, string Body)> Post(HttpClient client, string route, object value)
{
    using var response = await client.PostAsJsonAsync(route, value);
    var body = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, JsonNode.Parse(body) ?? throw new Exception($"Empty JSON response from {route}."), body);
}

static async Task<(HttpStatusCode Status, JsonNode Json, string Body)> AuthorizedJson(
    HttpClient client, HttpMethod method, string route, string token, object? value = null)
{
    using var request = new HttpRequestMessage(method, route);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (value is not null) request.Content = JsonContent.Create(value);
    using var response = await client.SendAsync(request);
    var body = await response.Content.ReadAsStringAsync();
    return (response.StatusCode, JsonNode.Parse(body) ?? throw new Exception($"Empty JSON response from {route}."), body);
}

static async Task ExpectError(HttpClient client, string route, object value, HttpStatusCode status, string code)
{
    var response = await Post(client, route, value);
    ExpectStatusCode(response.Status, status, response.Body);
    ExpectErrorCode(response.Json, code);
    Expect(response.Json["token"] is null, "An unsuccessful request returned a token.");
    ExpectNoSecrets(response.Json);
}

static void ExpectErrorCode(JsonNode json, string code) =>
    Expect(json["code"]?.GetValue<string>() == code, $"Expected error code {code}: {json}");

static string ValidateAuthResponse(JsonNode json, string userName)
{
    ExpectNoSecrets(json);
    var token = json["token"]?.GetValue<string>();
    Expect(!string.IsNullOrWhiteSpace(token) && token.Split('.').Length == 3, "A JWT token was not returned.");
    Expect(DateTimeOffset.TryParse(json["expiresAt"]?.GetValue<string>(), out var expiresAt)
        && expiresAt > DateTimeOffset.UtcNow, "The token expiration is missing or invalid.");
    ValidateUser(json["user"] ?? throw new Exception("The user was not returned."), userName);
    return token!;
}

static void ValidateUser(JsonNode json, string userName)
{
    Expect(json["userName"]?.GetValue<string>() == userName, $"Unexpected username: {json}");
    Expect(json["id"] is not null && !string.IsNullOrWhiteSpace(json["id"]!.ToString()), "User ID is missing.");
    Expect(json["roles"] is JsonArray roles && roles.Count == 1 && roles[0]?.GetValue<string>() == "User",
        "New users must only receive the User role.");
    Expect(json["permissions"] is JsonArray permissions && permissions.Count == 0,
        "New users must not receive elevated permissions.");
}

static void ExpectNoSecrets(JsonNode node)
{
    if (node is JsonObject value)
        foreach (var field in value)
        {
            Expect(!new[] { "password", "passwordHash", "passwordSalt", "pinCode", "securityStamp", "normalizedUserName" }
                .Contains(field.Key, StringComparer.OrdinalIgnoreCase), $"Sensitive field {field.Key} appeared in an API response.");
            if (field.Value is not null) ExpectNoSecrets(field.Value);
        }
    else if (node is JsonArray array)
        foreach (var child in array)
            if (child is not null) ExpectNoSecrets(child);
}

static void ExpectLifetime(JsonNode response, TimeSpan expected)
{
    var token = response["token"]!.GetValue<string>();
    var payload = JsonNode.Parse(DecodeBase64Url(token.Split('.')[1]))!;
    var expiry = DateTimeOffset.FromUnixTimeSeconds(payload["exp"]!.GetValue<long>());
    Expect(Math.Abs((expiry - DateTimeOffset.UtcNow - expected).TotalSeconds) < 30,
        $"Unexpected token lifetime: {expiry - DateTimeOffset.UtcNow}, expected {expected}.");
    var displayedExpiry = DateTimeOffset.Parse(response["expiresAt"]!.GetValue<string>());
    Expect(Math.Abs((displayedExpiry - expiry).TotalSeconds) < 1, "Response and signed JWT expiration do not match.");
}

static Task<HttpResponseMessage> GetUser(HttpClient client, string token)
{
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return SendAndDisposeRequest(client, request);
}

static async Task<HttpResponseMessage> SendAndDisposeRequest(HttpClient client, HttpRequestMessage request)
{
    using (request) return await client.SendAsync(request);
}

static string ExpireAndSignToken(string token, string signingKey)
{
    var pieces = token.Split('.');
    var header = JsonNode.Parse(DecodeBase64Url(pieces[0]))!;
    var payload = JsonNode.Parse(DecodeBase64Url(pieces[1]))!;
    payload["exp"] = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
    payload["iat"] = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
    if (payload["nbf"] is not null) payload["nbf"] = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();
    var content = pieces[0] + "." + EncodeBase64Url(Encoding.UTF8.GetBytes(payload.ToJsonString()));
    var key = Encoding.UTF8.GetBytes(signingKey);
    using HMAC hmac = header["alg"]?.GetValue<string>() switch
    {
        "HS256" => new HMACSHA256(key),
        "HS384" => new HMACSHA384(key),
        "HS512" => new HMACSHA512(key),
        var algorithm => throw new Exception($"Unsupported test signing algorithm: {algorithm}")
    };
    return content + "." + EncodeBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(content)));
}

static byte[] DecodeBase64Url(string value)
{
    var padded = value.Replace('-', '+').Replace('_', '/');
    return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
}

static string EncodeBase64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
static void ExpectStatus(HttpResponseMessage response, HttpStatusCode expected) => ExpectStatusCode(response.StatusCode, expected);
static void ExpectStatusCode(HttpStatusCode actual, HttpStatusCode expected, string? body = null) =>
    Expect(actual == expected, $"Expected HTTP {(int)expected}, received {(int)actual}. {body}");
static void Expect(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string FindRepository()
{
    foreach (var origin in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        for (var directory = new DirectoryInfo(origin); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "RoghtiaSystem_JahidKawa.Server")))
                return directory.FullName;
    throw new DirectoryNotFoundException("Cannot locate the Roghtia server project.");
}

sealed class TestServer : IAsyncDisposable
{
    private readonly Process process;
    private readonly string temporaryDirectory;
    private readonly ConcurrentQueue<string> output;

    private TestServer(Process process, string temporaryDirectory, string databasePath, string signingKey,
        HttpClient client, ConcurrentQueue<string> output)
    {
        this.process = process;
        this.temporaryDirectory = temporaryDirectory;
        this.output = output;
        DatabasePath = databasePath;
        SigningKey = signingKey;
        Client = client;
    }

    public HttpClient Client { get; }
    public string DatabasePath { get; }
    public string SigningKey { get; }

    public static async Task<TestServer> StartAsync(string repository, string assembly, int permitLimit)
    {
        using var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "roghtia-auth-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var databasePath = Path.Combine(temporaryDirectory, "auth-tests.db");
        var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(repository, "RoghtiaSystem_JahidKawa.Server"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(assembly);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        start.Environment["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"] = "";
        start.Environment["Auth__SigningKey"] = signingKey;
        start.Environment["ConnectionStrings__MainDatabase"] = $"Data Source={databasePath};Password=isolated-auth-test-database;Mode=ReadWriteCreate";
        start.Environment["Auth__RateLimitPermitLimit"] = permitLimit.ToString();
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        var output = new ConcurrentQueue<string>();
        var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => { if (line.Data is not null) output.Enqueue(line.Data); };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) output.Enqueue(line.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        var server = new TestServer(process, temporaryDirectory, databasePath, signingKey, client, output);
        try
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (process.HasExited) throw new Exception($"Test server exited with {process.ExitCode}.");
                try
                {
                    // An unmapped endpoint checks readiness without consuming an auth rate-limit permit.
                    using var ready = await client.GetAsync("/api/test-readiness");
                    return server;
                }
                catch (HttpRequestException) { }
                await Task.Delay(150);
            }
            throw new TimeoutException("The test server did not become available.");
        }
        catch (Exception error)
        {
            var diagnostics = string.Join(Environment.NewLine, output);
            await server.DisposeAsync();
            throw new Exception(error.Message + Environment.NewLine + diagnostics, error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        process.Dispose();
        var resolved = Path.GetFullPath(temporaryDirectory);
        var temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("roghtia-auth-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing cleanup outside the isolated test temporary directory.");
        Directory.Delete(resolved, recursive: true);
    }
}
