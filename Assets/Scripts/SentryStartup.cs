using Sentry;
using UnityEngine;

/// <summary>
/// Manually initializes the Sentry SDK at startup.
/// Once the Sentry Editor window (Tools > Sentry) is used to create a SentryOptions.asset,
/// this manual initialization will be superseded by the SDK's built-in auto-initialization.
/// </summary>
public static class SentryStartup
{
    private const string Dsn = "https://1b0bfd9260cad7a53bb0209db52f39a4@o213028.ingest.us.sentry.io/4511790686863360";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var environment = Application.isEditor ? "editor" : "production";

        SentrySdk.Init(options =>
        {
            options.Dsn = Dsn;
            options.Debug = Application.isEditor;
            options.Environment = environment;
        });

        Debug.Log($"[Sentry] Initialized — Environment: {environment}");
    }
}
