using Common.Persistence;

namespace UserManagement.Tests;

internal sealed class TestActorProvider : ICurrentActorProvider
{
    public static readonly TestActorProvider Instance = new();

    public string ActorId => "11111111-1111-1111-1111-111111111111";
}
