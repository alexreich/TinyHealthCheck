using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Threading.Tasks;
using TinyHealthCheck.HealthChecks;
using TinyHealthCheck.Models;

namespace TinyHealthCheck
{
    public static class AspNetCoreExtensions
    {
        /// <summary>
        /// Registers a TinyHealthCheck implementation for use in an ASP.NET Core pipeline.
        /// </summary>
        /// <typeparam name="T">The custom health check type</typeparam>
        /// <param name="services">Service Collection</param>
        /// <returns>Service Collection</returns>
        public static IServiceCollection AddTinyHealthCheck<T>(this IServiceCollection services) where T : class, IHealthCheck
        {
            return services.AddTransient<T>();
        }

        /// <summary>
        /// Maps a TinyHealthCheck endpoint into an existing ASP.NET Core app.
        /// </summary>
        /// <typeparam name="T">The custom health check type</typeparam>
        /// <param name="endpoints">Endpoint route builder</param>
        /// <param name="configFunc">Custom TinyHealthCheck configuration object</param>
        /// <returns>Endpoint convention builder</returns>
        public static IEndpointConventionBuilder MapTinyHealthCheck<T>(this IEndpointRouteBuilder endpoints, Func<TinyHealthCheckConfig, TinyHealthCheckConfig> configFunc)
            where T : class, IHealthCheck
        {
            if (configFunc == null)
                throw new ArgumentNullException(nameof(configFunc));

            var config = configFunc(new TinyHealthCheckConfig());
            return endpoints.MapTinyHealthCheck<T>(config.UrlPath);
        }

        /// <summary>
        /// Maps a TinyHealthCheck endpoint into an existing ASP.NET Core app.
        /// </summary>
        /// <typeparam name="T">The custom health check type</typeparam>
        /// <param name="endpoints">Endpoint route builder</param>
        /// <param name="pattern">Route pattern to map</param>
        /// <returns>Endpoint convention builder</returns>
        public static IEndpointConventionBuilder MapTinyHealthCheck<T>(this IEndpointRouteBuilder endpoints, string pattern)
            where T : class, IHealthCheck
        {
            if (endpoints == null)
                throw new ArgumentNullException(nameof(endpoints));
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Endpoint pattern is required.", nameof(pattern));

            return endpoints.MapGet(pattern, async context => await ExecuteHealthCheck<T>(context).ConfigureAwait(false));
        }

        private static async Task ExecuteHealthCheck<T>(HttpContext context) where T : class, IHealthCheck
        {
            var healthCheck = context.RequestServices.GetRequiredService<T>();
            var healthCheckResult = await healthCheck.ExecuteAsync(context.RequestAborted).ConfigureAwait(false);

            context.Response.StatusCode = (int)healthCheckResult.StatusCode;
            context.Response.ContentType = BuildContentType(healthCheckResult);

            if (healthCheckResult.Body == null)
                return;

            byte[] data = Encoding.UTF8.GetBytes(healthCheckResult.Body);
            context.Response.ContentLength = data.LongLength;
            await context.Response.Body.WriteAsync(data, 0, data.Length, context.RequestAborted).ConfigureAwait(false);
        }

        private static string BuildContentType(IHealthCheckResult result)
        {
            if (result.ContentEncoding == null || string.IsNullOrWhiteSpace(result.ContentType))
                return result.ContentType;

            if (result.ContentType.IndexOf("charset=", StringComparison.OrdinalIgnoreCase) >= 0)
                return result.ContentType;

            return $"{result.ContentType}; charset={result.ContentEncoding.WebName}";
        }
    }
}
