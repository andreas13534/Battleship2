using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;

namespace NavalCommandOnline;

public sealed class ModuleSetup : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
        config.Dependencies.AddSingleton<INavalCloudSaveStore, NavalCloudSaveStore>();
        config.Dependencies.AddSingleton<INavalFriendshipVerifier, NavalFriendshipVerifier>();
    }
}
