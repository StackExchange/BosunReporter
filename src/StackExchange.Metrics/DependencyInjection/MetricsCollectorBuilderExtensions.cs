﻿using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Metrics.Handlers;
using StackExchange.Metrics.Infrastructure;
using StackExchange.Metrics.Metrics;

namespace StackExchange.Metrics.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="IMetricsCollectorBuilder" />.
    /// </summary>
    public static class MetricsCollectorBuilderExtensions
    {
        /// <summary>
        /// Adds the default built-in <see cref="IMetricSet" /> implementations to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddDefaultSets(this IMetricsCollectorBuilder builder)
        {
            return builder.AddProcessMetricSet()
#if NETCOREAPP
                .AddAspNetMetricSet()
                .AddRuntimeMetricSet()
#endif
            ;
        }

        /// <summary>
        /// Adds a <see cref="ProcessMetricSet" /> to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddProcessMetricSet(this IMetricsCollectorBuilder builder) => builder.AddSet<ProcessMetricSet>();

#if NETCOREAPP
        /// <summary>
        /// Adds a <see cref="RuntimeMetricSet" /> to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddRuntimeMetricSet(this IMetricsCollectorBuilder builder) => builder.AddSet<RuntimeMetricSet>();

        /// <summary>
        /// Adds a <see cref="AspNetMetricSet" /> to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddAspNetMetricSet(this IMetricsCollectorBuilder builder) => builder.AddSet<AspNetMetricSet>();
#endif

        /// <summary>
        /// Adds a Bosun endpoint to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddBosunEndpoint(this IMetricsCollectorBuilder builder, Uri baseUri, Action<BosunMetricHandler> configure)
        {
            var handler = new BosunMetricHandler(baseUri);
            configure?.Invoke(handler);
            return builder.AddEndpoint("Bosun", handler);
        }

        /// <summary>
        /// Adds a SignalFx endpoint to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddSignalFxEndpoint(this IMetricsCollectorBuilder builder, Uri baseUri, Action<SignalFxMetricHandler> configure = null)
        {
            var handler = new SignalFxMetricHandler(baseUri);
            configure?.Invoke(handler);
            return builder.AddEndpoint("SignalFx", handler);
        }

        /// <summary>
        /// Adds a SignalFx endpoint to the collector.
        /// </summary>
        public static IMetricsCollectorBuilder AddSignalFxEndpoint(this IMetricsCollectorBuilder builder, Uri baseUri, string accessToken, Action<SignalFxMetricHandler> configure = null)
        {
            var handler = new SignalFxMetricHandler(baseUri, accessToken);
            configure?.Invoke(handler);
            return builder.AddEndpoint("SignalFx", handler);
        }
    }
}
