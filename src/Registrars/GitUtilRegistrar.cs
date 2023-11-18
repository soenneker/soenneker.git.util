using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Git.Util.Abstract;

namespace Soenneker.Git.Util.Registrars;

/// <summary>
/// A validation module checking for disposable email addresses
/// </summary>
public static class GitUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a singleton service. <para/>
    /// </summary>
    public static void AddGitUtilAsSingleton(this IServiceCollection services)
    {
        services.TryAddSingleton<IGitUtil, GitUtil>();
    }

    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a scoped service. <para/>
    /// </summary>
    public static void AddGitUtilAsScoped(this IServiceCollection services)
    {
        services.TryAddScoped<IGitUtil, GitUtil>();
    }
}
