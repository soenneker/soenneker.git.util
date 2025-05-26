using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.Process.Registrars;

namespace Soenneker.Git.Util.Registrars;

/// <summary>
/// A utility library for useful and common Git operations
/// </summary>
public static class GitUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a singleton service. <para/>
    /// </summary>
    public static IServiceCollection AddGitUtilAsSingleton(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsSingleton().AddProcessUtilAsSingleton().TryAddSingleton<IGitUtil, GitUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a scoped service. <para/>
    /// </summary>
    public static IServiceCollection AddGitUtilAsScoped(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsScoped().AddProcessUtilAsScoped().TryAddScoped<IGitUtil, GitUtil>();

        return services;
    }
}