# StackExchange.Metrics

![Build status](https://github.com/StackExchange/StackExchange.Metrics/workflows/Build,%20Test%20&%20Package/badge.svg)

A thread-safe C# .NET client for reporting metrics to various providers, including [Bosun (Time Series Alerting Framework)](http://bosun.org) and SignalFx. This library is more than a simple wrapper around relevant APIs. It is designed to encourage best-practices while making it easy to create counters and gauges, including multi-aggregate gauges. It automatically reports metrics on an interval and handles temporary API or network outages using a re-try queue.

__[VIEW CHANGES IN StackExchange.Metrics 2.0](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/ReleaseNotes.md)__

#### Package Status

MyGet Pre-release feed: https://www.myget.org/gallery/stackoverflow

| Package | NuGet Stable | NuGet Pre-release | Downloads | MyGet |
| ------- | ------------ | ----------------- | --------- | ----- |
| [StackExchange.Metrics](https://www.nuget.org/packages/StackExchange.Metrics/) | [![StackExchange.Metrics](https://img.shields.io/nuget/v/StackExchange.Metrics.svg)](https://www.nuget.org/packages/StackExchange.Metrics/) | [![StackExchange.Metrics](https://img.shields.io/nuget/vpre/StackExchange.Metrics.svg)](https://www.nuget.org/packages/StackExchange.Metrics/) | [![StackExchange.Metrics](https://img.shields.io/nuget/dt/StackExchange.Metrics.svg)](https://www.nuget.org/packages/StackExchange.Metrics/) | [![StackExchange.Metrics MyGet](https://img.shields.io/myget/stackoverflow/vpre/StackExchange.Metrics.svg)](https://www.myget.org/feed/stackoverflow/package/nuget/StackExchange.Metrics) |

### Basic Usage

#### .NET Full Framework

First, create a `MetricsCollector` object. This is the top-level container which will hold all of your metrics and handle sending them to various metric endpoints. Therefore, you should only instantiate one, and make it a global singleton.

```csharp
var collector = new MetricsCollector(
    new MetricsCollectorOptions
    {
        ExceptionHandler = ex => HandleException(ex),
	    MetricsNamePrefix = "app_name.",
	    Endpoints = new[] {
		    new MetricEndpoint("Bosun", new BosunMetricHandler(new Uri("http://bosun.mydomain.com:8070"))),
		    new MetricEndpoint("SignalFx", new SignalFxMetricHandler(new Uri("https://mydomain.signalfx.com/api", "API_KEY"))),
	    },
	    PropertyToTagName = NameTransformers.CamelToLowerSnakeCase,
	    DefaultTags = new Dictionary<string, string> 
		{
            {"host", NameTransformers.Sanitize(Environment.MachineName.ToLower())},
            {"tier", "dev"}
        }
    }
);
```

#### .NET Core

For .NET Core, you can configure a `MetricsCollector` in your `Startup.cs`. 

Using the snippet below will register an `IHostedService` in the service collection that manages the lifetime of the `MetricsCollector`
and configures it with the specified endpoints, metric sets and tags.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMetricsCollector()
            // add default metric sets
            .AddDefaultSets()
            // add additional tags across all metrics. By default we add the "host" tag
            .AddDefaultTag("tier", "dev");
            // add endpoints we care about. By default we add a `LocalMetricHandler` that 
            // just maintains the latest metrics in memory (useful for debugging)
            .AddBosunEndpoint(new Uri("http://bosun.mydomain.com:8070"))
            .AddSignalFxEndpoint(new Uri("https://mydomain.signalfx.com/api", "API_KEY"))
            .UseExceptionHandler(ex => HandleException(ex))
            // tweak other parts of `MetricsCollectionOptions`
            .Configure(
                o => {
                    o.SnapshotInterval = TimeSpan.FromSeconds(5);
                }
            )
    }
}
```

> All of the available options are documented in the [MetricCollectorOptions class](https://github.com/StackExchange/StackExchange.Metrics/blob/master/src/StackExchange.Metrics/MetricCollectorOptions.cs) or the individual metric handlers:
 - [BosunMetricHandler](https://github.com/StackExchange/StackExchange.Metrics/blob/master/src/StackExchange.Metrics/Handlers/BosunMetricHandler.cs)
 - [LocalMetricHandler](https://github.com/StackExchange/StackExchange.Metrics/blob/master/src/StackExchange.Metrics/Handlers/LocalMetricHandler.cs)
 - [SignalFxMetricHandler](https://github.com/StackExchange/StackExchange.Metrics/blob/master/src/StackExchange.Metrics/Handlers/SignalFxMetricHandler.cs)

Create a counter with only the default tags:

```csharp
var counter = collector.CreateMetric<Counter>("my_counter", "units", "description");
```

Increment the counter by 1:

```csharp
counter.Increment();
```

### Using Tags

Tags are used to subdivide data in various metric platforms. In StackExchange.Metrics, tag sets are defined as C# classes. For example:

```csharp
public class SomeCounter : Counter
{
	[MetricTag] public readonly string SomeTag;
	
	public RouteCounter(string tag)
	{
		SomeTag = tag;
	}
}
```

For more details, see the [Tags Documentation](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/Tags.md).

### Metric Types

There are two high-level metric types: counters and gauges.

__[Counters](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#counters)__ are for _counting_ things. The most common use case is to increment a counter each time an event occurs. Many metric platforms normalize this data and is able to show you a rate (events per second) in the graphing interface. StackExchange.Metrics has two built-in counter types.

| Name                                     | Description                              |
| ---------------------------------------- | ---------------------------------------- |
| [Counter](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#counter) | A general-purpose manually incremented long-integer counter. |
| [SnapshotCounter](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#snapshotcounter) | Calls a user-provided `Func<long?>` to get the current counter value each time metrics are going to be posted to a metric handler. |
| [CumulativeCounter](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#cumulativecounter) | A persistent counter (no resets) for very low-volume events. |

__[Gauges](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#gauges)__ describe a measurement at a point in time. A good example would be measuring how much RAM is being consumed by a process. StackExchange.Metrics provides several different built-in types of gauges in order to support different programmatic use cases.

| Name                                     | Description                              |
| ---------------------------------------- | ---------------------------------------- |
| [SnapshotGauge](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#snapshotgauge) | Similar to a SnapshotCounter, it calls a user provided `Func<double?>` to get the current gauge value each time metrics are going to be posted to the metrics handlers. |
| [EventGauge](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#eventgauge) | Every data point is sent to the metrics handlers. Good for low-volume events. |
| [AggregateGauge](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#aggregategauge) | Aggregates data points (min, max, avg, median, etc) before sending them to the metrics handlers. Good for recording high-volume events. |
| [SamplingGauge](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#samplinggauge) | Record as often as you want, but only the last value recorded before the reporting interval is sent to the metrics handlers (it _samples_ the current value). |

If none of the built-in metric types meet your specific needs, it's easy to [create your own](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricTypes.md#create-your-own).

### Metric Groups

Metric groups allow you to easily setup metrics which share the same name, but with different tag values. [See Documentation](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricGroup.md).

### Metric sets

Metric sets are pre-packaged sets of metrics that are useful across different applications. [See Documentation](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/MetricSet.md) for further details.

## Implementation Notes

Periodically a `MetricsCollector` instance serializes all the metrics that it is responsible for collecting. 
When it does so it serially calls Serialize on each metric which eventually results in a call to WriteValue. 
WriteValue uses an `IMetricBatch` to assist in writing metrics into an endpoint-defined format using an 
implementation of `IBufferWriter<byte>` for buffering purposes.

For each type of payload that can be sent to an endpoint an `IBufferWriter<byte>` is created that manages 
an underlying buffer consisting of zero or more contiguous byte arrays. 

At a specific interval the `MetricsCollector` flushes all metrics that have been serialized into the `IBufferWriter<byte>`
to the underlying transport implemented by an endpoint (generally an HTTP JSON API or statsd UDP endpoint). Once flushed the associated buffer
is released back to be used by the next batch of metrics being serialized. This keeps memory allocations low.
