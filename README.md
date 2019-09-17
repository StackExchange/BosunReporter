# BosunReporter

[![NuGet version](https://badge.fury.io/nu/BosunReporter.svg)](http://badge.fury.io/nu/BosunReporter)
[![Build status](https://ci.appveyor.com/api/projects/status/yt8nl66ha598jbr7/branch/master?svg=true)](https://ci.appveyor.com/project/StackExchange/bosunreporter-net/branch/master)

A thread-safe C# .NET client for reporting metrics to various providers, including [Bosun (Time Series Alerting Framework)](http://bosun.org), SignalFx and DataDog. This library is more than a simple wrapper around relevant APIs. It is designed to encourage best-practices while making it easy to create counters and gauges, including multi-aggregate gauges. It automatically reports metrics on an interval and handles temporary API or network outages using a re-try queue.

__[VIEW CHANGES IN 5.0](https://github.com/StackExchange/StackExchange.Metrics/blob/master/docs/ReleaseNotes.md)__

### Basic Usage

First, create a `MetricsCollector` object. This is the top-level container which will hold all of your metrics and handle sending them to the Bosun API. Therefore, you should only instantiate one, and make it a global singleton.

```csharp
var collector = new MetricsCollector(new BosunOptions(ex => HandleException(ex))
{
	MetricsNamePrefix = "app_name.",
	Endpoints = new[] {
		new MetricEndpoint("Bosun", new BosunMetricHandler("http://bosun.mydomain.com:8070")),
		new MetricEndpoint("DataDog", new DataDogMetricHandler("http://datadog.mydomain.com:1234", "API_KEY", "APP_KEY")),
	},
	PropertyToTagName = NameTransformers.CamelToLowerSnakeCase,
	DefaultTags = new Dictionary<string, string> 
		{ {"host", NameTransformers.Sanitize(Environment.MachineName.ToLower())} }
});
```

> All of the available options are documented in the [BosunOptions class](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/BosunOptions.cs) or the individual metric handlers:
 - [BosunMetricHandler](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/Handlers/BosunMetricHandler.cs)
 - [DataDogMetricHandler](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/Handlers/DataDogMetricHandler.cs)
 - [DataDogStatsdMetricHandler](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/Handlers/DataDogStatsdMetricHandler.cs)
 - [LocalMetricHandler](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/Handlers/LocalMetricHandler.cs)
 - [SignalFxMetricHandler](https://github.com/StackExchange/BosunReporter/blob/master/BosunReporter/Handlers/SignalFxMetricHandler.cs)

Create a counter with only the default tags:

```csharp
var counter = collector.CreateMetric<Counter>("my_counter", "units", "description");
```

Increment the counter by 1:

```csharp
counter.Increment();
```

### Using Tags

Tags are used to subdivide data in various metric platforms. In BosunReporter, tag sets are defined as C# classes. For example:

```csharp
public class SomeCounter : Counter
{
	[BosunTag] public readonly string SomeTag;
	
	public RouteCounter(string tag)
	{
		SomeTag = tag;
	}
}
```

For more details, see the [Tags Documentation](https://github.com/StackExchange/BosunReporter/blob/master/docs/Tags.md).

### Metric Types

There are two high-level metric types: counters and gauges.

__[Counters](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#counters)__ are for _counting_ things. The most common use case is to increment a counter each time an event occurs. Bosun/OpenTSDB normalizes this data and is able to show you a rate (events per second) in the graphing interface. BosunReporter has two built-in counter types.

| Name                                     | Description                              |
| ---------------------------------------- | ---------------------------------------- |
| [Counter](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#counter) | A general-purpose manually incremented long-integer counter. |
| [SnapshotCounter](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#snapshotcounter) | Calls a user-provided `Func<long?>` to get the current counter value each time metrics are going to be posted to the Bosun API. |
| [ExternalCounter](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#externalcounter) | A persistent counter (no resets) for very low-volume events. |

__[Gauges](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#gauges)__ describe a measurement at a point in time. A good example would be measuring how much RAM is being consumed by a process. BosunReporter provides several different built-in types of gauges in order to support different programmatic use cases, but Bosun itself does not differentiate between these types.

| Name                                     | Description                              |
| ---------------------------------------- | ---------------------------------------- |
| [SnapshotGauge](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#snapshotgauge) | Similar to a SnapshotCounter, it calls a user provided `Func<double?>` to get the current gauge value each time metrics are going to be posted to the Bosun API. |
| [EventGauge](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#eventgauge) | Every data point is sent to Bosun. Good for low-volume events. |
| [AggregateGauge](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#aggregategauge) | Aggregates data points (min, max, avg, median, etc) before sending them to Bosun. Good for recording high-volume events. |
| [SamplingGauge](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#samplinggauge) | Record as often as you want, but only the last value recorded before the reporting interval is sent to Bosun (it _samples_ the current value). |

If none of the built-in metric types meet your specific needs, it's easy to [create your own](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricTypes.md#create-your-own).

### Metric Groups

Metric groups allow you to easily setup metrics which share the same name, but with different tag values. [See Documentation](https://github.com/StackExchange/BosunReporter/blob/master/docs/MetricGroup.md).

## Implementation Notes

Periodically a `MetricsCollector` instance serializes all the metrics that it is responsible for collecting. 
When it does so it serially calls Serialize on each metric which eventually results in a call to WriteValue. 
WriteValue uses an `IMetricBatch` to assist in writing metrics into an endpoint-defined format using an 
implementation of `IBufferWriter<byte>` for buffering purposes.

For each type of payload that can be sent to an endpoint an `IBufferWriter<byte>` is created that manages 
an underlying buffer consisting of zero or more contiguous byte arrays. 

At a specific interval the `MetricsCollector` flushes all metrics that have been serialized into the `IBufferWriter<byte>`
to the underlying transport implemented by an endpoint (generally an HTTP JSON API). Once flushed the associated buffer
is released back to be used by the next batch of metrics being serialized.
