# Authentication integration checks

Build the server, then run this dependency-free .NET test executable from the repository root:

```powershell
dotnet build RoghtiaSystem_JahidKawa.Server -p:BuildProjectReferences=false
dotnet run --project RoghtiaSystem_JahidKawa.Server.Tests
```

An optional first argument specifies a different compiled server DLL. The harness launches the actual API with `Testing` configuration, random signing keys, and a separate encrypted SQLite database in a temporary directory. It stops its server processes and removes only its temporary databases afterwards. It never uses the application's database or credentials.

Checks cover registration, Unicode credentials, case and Unicode normalization, simultaneous duplicate registration, invalid input, incorrect credentials, bearer authentication, safe user responses, session and Remember Me token lifetimes, tampered and expired tokens, and authentication throttling. A failure exits with code 1; successful execution prints each passing check and exits with code 0.

The harness sets `Auth:RateLimitPermitLimit` to 100 for the primary test process and 3 for the isolated throttling check. Production uses the server's configured default.
