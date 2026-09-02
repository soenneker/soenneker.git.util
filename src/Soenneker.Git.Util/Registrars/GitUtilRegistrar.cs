using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Soenneker.Git.Util.Abstract;
using Soenneker.Utils.Directory.Registrars;
using Soenneker.Utils.File.Registrars;
using Soenneker.Utils.Process.Registrars;
using Soenneker.Utils.Paths.Resources.Registrars;

namespace Soenneker.Git.Util.Registrars;

/// <summary>
/// A utility library for useful and common Git operations
/// </summary>
public static class GitUtilRegistrar
{
    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a singleton service. <para/>
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddGitUtilAsSingleton(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsSingleton()
                .AddProcessUtilAsSingleton()
                .AddFileUtilAsSingleton()
                .AddResourcesPathUtilAsSingleton()
                .TryAddSingleton<IGitUtil, GitUtil>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IGitUtil"/> as a scoped service. <para/>
    /// </summary>
    /// <param name="services">Service collection that receives the registration.</param>
    /// <returns>The same service collection, so additional registrations can be chained.</returns>
    public static IServiceCollection AddGitUtilAsScoped(this IServiceCollection services)
    {
        services.AddDirectoryUtilAsScoped()
                .AddProcessUtilAsScoped()
                .AddFileUtilAsScoped()
                .AddResourcesPathUtilAsScoped()
                .TryAddScoped<IGitUtil, GitUtil>();

        return services;
    }
}
