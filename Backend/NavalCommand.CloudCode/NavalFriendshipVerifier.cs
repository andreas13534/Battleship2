using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.Friends.Model;

namespace NavalCommandOnline;

public interface INavalFriendshipVerifier
{
    Task EnsureFriendAsync(IExecutionContext context, string otherPlayerId);
}

public sealed class NavalFriendshipVerifier : INavalFriendshipVerifier
{
    private readonly IGameApiClient _api;
    public NavalFriendshipVerifier(IGameApiClient api) => _api = api;

    public async Task EnsureFriendAsync(IExecutionContext context, string otherPlayerId)
    {
        const int pageSize = 100;
        for (int offset = 0; ; offset += pageSize)
        {
            var response = await _api.FriendsRelationshipsApi.GetRelationshipsAsync(
                context, context.AccessToken, pageSize, offset, false, false,
                new List<RelationshipType> { RelationshipType.FRIEND });
            if (response.Data.Any(relationship => relationship.Type == RelationshipType.FRIEND &&
                relationship.Members.Any(member => member.Id == otherPlayerId))) return;
            if (response.Data.Count < pageSize) break;
        }
        throw new InvalidOperationException("FRIENDSHIP_REQUIRED");
    }
}
