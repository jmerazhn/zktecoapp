using Microsoft.Extensions.DependencyInjection;

namespace ZKTecoManager.Helpers;

public static class ServiceLocator
{
    private static IServiceProvider? _provider;

    public static void Initialize(IServiceProvider provider) => _provider = provider;

    public static T GetService<T>() where T : notnull
        => _provider!.GetRequiredService<T>();
}
