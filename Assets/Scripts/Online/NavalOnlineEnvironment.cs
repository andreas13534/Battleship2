/// <summary>
/// Keeps development and production traffic separated at compile time.
/// Add NAVAL_PRODUCTION to the release build's scripting define symbols only
/// after the production UGS environment has been provisioned.
/// </summary>
public static class NavalOnlineEnvironment
{
    public const string Development = "development";
    public const string Production = "production";

#if NAVAL_PRODUCTION
    public const string Current = Production;
#else
    public const string Current = Development;
#endif
}
