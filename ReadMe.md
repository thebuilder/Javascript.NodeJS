# Jering.Javascript.NodeJS
[![Build Status](https://dev.azure.com/JeringTech/Javascript.NodeJS/_apis/build/status/Jering.Javascript.NodeJS-CI?branchName=master)](https://dev.azure.com/JeringTech/Javascript.NodeJS/_build/latest?definitionId=1?branchName=master)
[![codecov](https://codecov.io/gh/JeringTech/Javascript.NodeJS/branch/master/graph/badge.svg)](https://codecov.io/gh/JeringTech/Javascript.NodeJS)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](https://github.com/Pkcs11Interop/Pkcs11Interop/blob/master/LICENSE.md)
[![NuGet](https://img.shields.io/nuget/vpre/Jering.Javascript.NodeJS.svg?label=nuget)](https://www.nuget.org/packages/Jering.Javascript.NodeJS/)

## Table of Contents
[Overview](#overview)  
[Target Frameworks](#target-frameworks)  
[Prerequisites](#prerequisites)  
[Installation](#installation)  
[Usage](#usage)  
[API](#api)  
[Extensibility](#extensibility)  
[Performance](#performance)  
[Building and Testing](#building-and-testing)  
[Projects Using this Library](#projects-using-this-library)  
[Related Concepts](#related-concepts)  
[Contributing](#contributing)  
[About](#about)  

## Overview
Jering.Javascript.NodeJS enables you to invoke javascript in [NodeJS](https://nodejs.org/en/), from C#. With this ability, you can use javascript libraries and scripts from your C# projects.  

> You can use this library as a replacement for the recently deprecated [Microsoft.AspNetCore.NodeServices](https://github.com/aspnet/JavaScriptServices/tree/master/src/Microsoft.AspNetCore.NodeServices).
[`InvokeFromFileAsync<T>`](#inodejsserviceinvokefromfileasync) replaces `INodeService`'s `InvokeAsync<T>` and `InvokeExportAsync<T>`.

This library is built to be flexible; you can use a dependency injection (DI) based API or a static API, also, you can invoke both in-memory and on-disk javascript. 

Static API example:

```csharp
string javascriptModule = @"
module.exports = (callback, x, y) => {  // Module must export a function that takes a callback as its first parameter
    var result = x + y; // Your javascript logic
    callback(null /* If an error occurred, provide an error object or message */, result); // Call the callback when you're done.
}";

// Invoke javascript
int result = await StaticNodeJSService.InvokeFromStringAsync<int>(javascriptModule, args: new object[] { 3, 5 });

// result == 8
Assert.Equal(8, result);
```

DI based API example:

```csharp
string javascriptModule = @"
module.exports = (callback, x, y) => {  // Module must export a function that takes a callback as its first parameter
    var result = x + y; // Your javascript logic
    callback(null /* If an error occurred, provide an error object or message */, result); // Call the callback when you're done.
}";

// Create INodeJSService instance
var services = new ServiceCollection();
services.AddNodeJS();
ServiceProvider serviceProvider = services.BuildServiceProvider();
INodeJSService nodeJSService = serviceProvider.GetRequiredService<INodeJSService>();

// Invoke javascript
int result = await nodeJSService.InvokeFromStringAsync<int>(javascriptModule, args: new object[] { 3, 5 });

// result == 8
Assert.Equal(8, result);
```

## Target Frameworks
- .NET Standard 2.0
- .NET Framework 4.6.1
 
## Prerequisites
[NodeJS](https://nodejs.org/en/) must be installed and node.exe's directory must be added to the `Path` environment variable. This library has been
tested with NodeJS 10.5.2 - 12.13.0.

## Installation
Using Package Manager:
```
PM> Install-Package Jering.Javascript.NodeJS
```
Using .Net CLI:
```
> dotnet add package Jering.Javascript.NodeJS
```

## Usage
### Creating INodeJSService
This library provides a DI based API to facilitate [extensibility](#extensibility) and testability.
You can use any DI framework that has adapters for [Microsoft.Extensions.DependencyInjection](https://github.com/aspnet/DependencyInjection).
Here, we'll use the vanilla Microsoft.Extensions.DependencyInjection framework:
```csharp
var services = new ServiceCollection();
services.AddNodeJS();
ServiceProvider serviceProvider = services.BuildServiceProvider();
INodeJSService nodeJSService = serviceProvider.GetRequiredService<INodeJSService>();
```
The default implementation of `INodeJSService` is `HttpNodeJSService`. It starts a Http server in a NodeJS process and sends invocation requests
over Http. For simplicty's sake, this ReadMe assumes that `INodeJSService`'s default implementation is used.

`INodeJSService` is a singleton service and `INodeJSService`'s members are thread safe.
Where possible, inject `INodeJSService` into your types or keep a reference to a shared `INodeJSService` instance. 
Try to avoid creating multiple `INodeJSService` instances since by default, each instance spawns a NodeJS process. 

When you're done, you can manually dispose of an `INodeJSService` instance by calling
```csharp
nodeJSService.Dispose();
```
or 
```csharp
serviceProvider.Dispose(); // Calls Dispose on objects it has instantiated that are disposable
```
`Dispose` kills the spawned NodeJS process.
Note that even if `Dispose` isn't called manually, `INodeJSService` will kill the 
NodeJS process when the application shuts down - if the application shuts down gracefully. If the application doesn't shutdown gracefully, the NodeJS process will kill 
itself when it detects that its parent has been killed. 
Essentially, manually disposing of `INodeJSService` instances is not mandatory.

#### Static API
This library also provides a static API as an alternative. The `StaticNodeJSService` type wraps an `INodeJSService` instance, exposing most of its [public members](#api) statically.
Whether you use the static API or the DI based API depends on your development needs. If you are already using DI, if you want to mock 
out javascript invocations in your tests or if you want to [overwrite](#extensibility) services, use the DI based API. Otherwise,
use the static API. An example usage:

```csharp
string result = await StaticNodeJSService
    .InvokeFromStringAsync<Result>("module.exports = (callback, message) => callback(null, message);", args: new[] { "success" });

Assert.Equal("success", result);
```
The following section on using `INodeJSService` applies to usage of `StaticNodeJSService`.

### Using INodeJSService
#### Basics
To invoke javascript, we'll first need to create a [NodeJS module](#nodejs-modules) that exports a function or an object containing functions. Exported functions can be of two forms:

##### Function With Callback Parameter
These functions must take a callback as their first argument, and they must call the callback.
The callback takes two optional arguments:
- The first argument must be an error or an error message. It must be an instance of type [`Error`](https://nodejs.org/api/errors.html#errors_class_error) or a `string`.
- The second argument is the result. It must be an instance of a JSON-serializable type, a `string`, or a [`stream.Readable`](https://nodejs.org/api/stream.html#stream_class_stream_readable). 
 
This sort of callback is known as an [error-first callback](https://nodejs.org/api/errors.html#errors_error_first_callbacks).
Such callbacks are commonly used for [error handling](https://nodejs.org/api/errors.html#errors_error_propagation_and_interception) in NodeJS asynchronous code (check out the [NodeJS event loop](https://nodejs.org/en/docs/guides/event-loop-timers-and-nexttick/)
if you'd like to learn more about how asynchrony works in NodeJS).

This is a module that exports a valid function:
```javascript
module.exports = (callback, arg1, arg2, arg3) => {
    ... // Do something with args

    callback(null, result);
}
```

And this is a module that exports an object containing valid functions:
```javascript
module.exports = {
    doSomething: (callback, arg1) => {
        ... // Do something with arg

        callback(null, result);
    },
    doSomethingElse: (callback) => {
        ... // Do something else

        callback(null, result);
    }
}
```

##### Async Function
Async functions are really just syntactic sugar for functions with callback parameters.
[Callbacks, Promises and Async/Await](https://medium.com/front-end-weekly/callbacks-promises-and-async-await-ad4756e01d90) provides a nice summary on how callbacks, promises and async/await work.

This is a module that exports a valid function:
```javascript
module.exports = async (arg1, arg2) => {
    ... // Do something with args

    return result;
}
```

And this is a module that exports an object containing valid functions:
```javascript
module.exports = {
    doSomething: async (arg1, arg2, arg3, arg4) => {
        ... // Do something with args

        // async functions can explicitly return promises
        return new Promise((resolve, reject) => {
            resolve(result);
        });
    },
    doSomethingElse: async (arg1) => {
        ... // Do something with arg
            
        return result;
    }
}
```

If an error is thrown it is caught and handled by the caller (error message is sent back to the calling .Net process):
```javascript
module.exports = async () => {
    throw new Error('error message');
}
```

#### Invoking Javascript From a File
If we have a file named `exampleModule.js` (located in [`NodeJSProcessOptions.ProjectPath`](#nodejsprocessoptions)), with contents:
```javascript
module.exports = (callback, message) => callback(null, { resultMessage: message });
```
And we have the class `Result`:
```csharp
public class Result
{
    public string ResultMessage { get; set; }
}
```
We can invoke the javascript using [`InvokeFromFileAsync<T>`](#inodejsserviceinvokefromfileasync) ):
```csharp
Result result = await nodeJSService.InvokeFromFileAsync<Result>("exampleModule.js", args: new[] { "success" });

Assert.Equal("success", result.ResultMessage);
```
If we change `exampleModule.js` to export an object containing functions:
```javascript
module.exports = {
    appendExclamationMark: (callback, message) => callback(null, { resultMessage: message + '!' }),
    appendFullStop: (callback, message) => callback(null, { resultMessage: message + '.' })
}
```
We can invoke javascript by providing an export's name to `InvokeFromFileAsync`:
```csharp
Result result = await nodeJSService.InvokeFromFileAsync<Result>("exampleModule.js", "appendExclamationMark", args: new[] { "success" });

Assert.Equal("success!", result.ResultMessage);
```
When using `InvokeFromFileAsync`, NodeJS always caches the module, using the absolute path of the `.js` file as the module's cache identifier. This is great for
performance, since the file will not be read more than once.

#### Invoking Javascript in String Form
We can invoke javascript in string form using [`InvokeFromStringAsync<T>`](#inodejsserviceinvokefromstringasync) :
```csharp
Result result = await nodeJSService.InvokeFromStringAsync<Result>("module.exports = (callback, message) => callback(null, { resultMessage: message });", 
    args: new[] { "success" });

Assert.Equal("success", result.ResultMessage);
```

If we're going to invoke the module repeatedly, it would make sense to have NodeJS cache the module so that it doesn't need to be kept in 
memory and sent with every invocation. To cache the module, we must specify a custom cache identifier, since unlike a file, a string has no 
"absolute file path" for NodeJS to use as a cache identifier. Once NodeJS has cached the module, we should invoke logic directly from the NodeJS cache: 
```csharp
string cacheIdentifier = "exampleModule";

// Try to invoke from the NodeJS cache
(bool success, Result result) = await nodeJSService.TryInvokeFromCacheAsync<Result>(cacheIdentifier, args: new[] { "success" });
// If the NodeJS process dies and restarts, the module will have to be re-cached, so we must always check whether success is false
if(!success)
{
    // Retrieve the module string, this is a trivialized example for demonstration purposes
    string moduleString = "module.exports = (callback, message) => callback(null, { resultMessage: message });"; 
    // Cache and invoke the module
    result = await nodeJSService.InvokeFromStringAsync<Result>(moduleString, cacheIdentifier, args: new[] { "success" });
}

Assert.Equal("success", result.ResultMessage);
```

Like when [invoking javascript form a file](#invoking-javascript-from-a-file), if the module exports an object containing functions, we can invoke a function by specifying
an export's name.  
#### Invoking Javascript in Stream Form
We can invoke javascript in Stream form using [`InvokeFromStreamAsync<T>`](#inodejsserviceinvokefromstreamasync) :
```csharp
using (var memoryStream = new MemoryStream())
using (var streamWriter = new StreamWriter(memoryStream))
{
    // Write the module to a MemoryStream for demonstration purposes.
    streamWriter.Write("module.exports = (callback, message) => callback(null, {resultMessage: message});");
    streamWriter.Flush();
    memoryStream.Position = 0;

    Result result = await nodeJSService.InvokeFromStreamAsync<Result>(memoryStream, args: new[] { "success" });
    
    Assert.Equal("success", result.ResultMessage);
}
```
`InvokeFromStreamAsync` behaves in a similar manner to `InvokeFromStringAsync`, refer to [Invoking Javascript in String Form](#invoking-javascript-in-string-form) for details on caching and more. 
The utility of this method is in providing a way to avoid allocating a string if the source of the module is a Stream. Avoiding `string` allocations can improve performance.

### Configuring INodeJSService
This library uses the [ASP.NET Core options pattern](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-2.1). While developed for ASP.NET Core,
this pattern can be used by other types of applications. The NodeJS process and the service that manages the process are both configurable, for example:

 ```csharp
var services = new ServiceCollection();
services.AddNodeJS();

// Options for the NodeJSProcess, here we enable debugging
services.Configure<NodeJSProcessOptions>(options => options.NodeAndV8Options = "--inspect-brk");

// Options for the service that manages the process, here we make its timeout infinite
services.Configure<OutOfProcessNodeJSServiceOptions>(options => options.TimeoutMS = -1);

ServiceProvider serviceProvider = services.BuildServiceProvider();
INodeJSService nodeJSService = serviceProvider.GetRequiredService<INodeJSService>();
```

#### Configuring Using the Static API
The static API exposes a method for configuring options:
```csharp
StaticNodeJSService.Configure<OutOfProcessNodeJSServiceOptions>(options => options.TimeoutMS = -1);
```
Configurations made using `StaticNodeJSService.Configure<T>` only apply to javascript invocations made using the static API. 
Ideally, such configurations should be done before the first javascript invocation.
Any existing NodeJS process is killed and a new one is created in the first javascript invocation after every `StaticNodeJSService.Configure<T>` call. 
Re-creating the NodeJS process is resource intensive. Also, if you are using the static API from multiple threads and 
the NodeJS process is performing invocations for other threads, you might get unexpected results.

The next two sections list all available options.

#### NodeJSProcessOptions
| Option | Type | Description | Default |  
| ------ | ---- | ----------- | ------- |
| ProjectPath | `string` | The base path for resolving paths of NodeJS modules on disk. | If the application is an ASP.NET Core application, this value defaults to `IHostingEnvironment.ContentRootPath`. Otherwise, it defaults to the current working directory. |
| NodeAndV8Options | `string` | NodeJS and V8 options in the form "[NodeJS options] [V8 options]". The full list of NodeJS options can be found here: https://nodejs.org/api/cli.html#cli_options. | null |
| Port | `int` | The port that the server running on NodeJS will listen on. If set to 0, the OS will choose the port. | 0 |
| EnvironmentVariables | `IDictionary<string, string>` | The environment variables for the NodeJS process. The full list of NodeJS environment variables can be found here: https://nodejs.org/api/cli.html#cli_environment_variables. | null |

#### OutOfProcessNodeJSServiceOptions
| Option | Type | Description | Default |  
| ------ | ---- | ----------- | ------- |
| TimeoutMS | `int` | The maximum duration to wait for the NodeJS process to initialize and to wait for responses to invocations. If set to a negative value, the maximum duration will be infinite. | 10000 |
| NumRetries | `int` | The number of times an invocation will be retried. If set to a negative value, invocations will be retried indefinitely. If the module source of an invocation is an unseekable stream, the invocation will not be retried. If you require retries for such streams, copy their contents to a `MemoryStream`.| 1 |

### Debugging Javascript
These are the steps for debugging javascript invoked using INodeJSService:
1. Create an INodeJSService using the example options in the previous section (`NodeJSProcessOptions.NodeAndV8Options` = `--inspect-brk` and `OutOfProcessNodeJSServiceOptions.TimeoutMS` = `-1`).
2. Add [`debugger`](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/debugger) statements to your javascript module.
3. Call a [javascript invoking method](#api). 
4. Navigate to `chrome://inspect/` in Chrome.
5. Click "Open dedicated DevTools for Node".
6. Click continue to advance to your `debugger` statements.

## API
### INodeJSService.InvokeFromFileAsync
#### Signature
```csharp
Task<T> InvokeFromFileAsync<T>(string modulePath, string exportName = null, object[] args = null, CancellationToken cancellationToken = default(CancellationToken));
```
#### Description
Invokes a function exported by a NodeJS module on disk.
#### Parameters
- `T`
  - Description: The type of object this method will return. It can be a JSON-serializable type, `string`, or `Stream`.
 
- `modulePath`
  - Type: `string`
  - Description: The path to the NodeJS module (i.e., JavaScript file) relative to `NodeJSProcessOptions.ProjectPath`.

- `exportName`
  - Type: `string`
  - Description: The function in the module's exports to be invoked. If unspecified, the module's exports object is assumed to be a function, and is invoked.

- `args`
  - Type: `object[]`
  - Description: The sequence of JSON-serializable and/or `string` arguments to be passed to the function to invoke.

- `cancellationToken`
  - Type: `CancellationToken`
  - Description: The cancellation token for the asynchronous operation.
#### Returns
The task object representing the asynchronous operation.
#### Exceptions
- `InvocationException`
  - Thrown if a NodeJS error occurs.
  - Thrown if the invocation request times out.
  - Thrown if NodeJS cannot be initialized.
- `ObjectDisposedException`
  - Thrown if this instance has been disposed or if an attempt is made to use one of its dependencies that has been disposed.
- `OperationCanceledException`
  - Thrown if `cancellationToken` is cancelled.
#### Example
If we have a file named `exampleModule.js` (located in `NodeJSProcessOptions.ProjectPath`), with contents:
```javascript
module.exports = (callback, message) => callback(null, { resultMessage: message });
```
And we have the class `Result`:
```csharp
public class Result
{
    public string ResultMessage { get; set; }
}
```
The following assertion will pass:
```csharp
Result result = await nodeJSService.InvokeFromFileAsync<Result>("exampleModule.js", args: new[] { "success" });

Assert.Equal("success", result.ResultMessage);
```

### INodeJSService.InvokeFromStringAsync
#### Signature
```csharp
Task<T> InvokeFromStringAsync<T>(string moduleString, string newCacheIdentifier = null, string exportName = null, object[] args = null, CancellationToken cancellationToken = default(CancellationToken));
```
#### Description
Invokes a function exported by a NodeJS module in string form.
#### Parameters
- `T`
  - Description: The type of object this method will return. It can be a JSON-serializable type, `string`, or `Stream`.
 
- `moduleString`
  - Type: `string`
  - Description: The module in `string` form.

- `newCacheIdentifier`
  - Type: `string`
  - Description: The modules's cache identifier in the NodeJS module cache. If unspecified, the module will not be cached.

- `exportName`
  - Type: `string`
  - Description: The function in the module's exports to be invoked. If unspecified, the module's exports object is assumed to be a function, and is invoked.

- `args`
  - Type: `object[]`
  - Description: The sequence of JSON-serializable and/or `string` arguments to be passed to the function to invoke.

- `cancellationToken`
  - Type: `CancellationToken`
  - Description: The cancellation token for the asynchronous operation.
#### Returns
The task object representing the asynchronous operation.
#### Exceptions
- `InvocationException`
  - Thrown if a NodeJS error occurs.
  - Thrown if the invocation request times out.
  - Thrown if NodeJS cannot be initialized.
- `ObjectDisposedException`
  - Thrown if this instance has been disposed or if an attempt is made to use one of its dependencies that has been disposed.
- `OperationCanceledException`
  - Thrown if `cancellationToken` is cancelled.
#### Example
Using the class `Result`:
```csharp
public class Result
{
    public string ResultMessage { get; set; }
}
```
The following assertion will pass:
```csharp
Result result = await nodeJSService.InvokeFromStringAsync<Result>("module.exports = (callback, message) => callback(null, { resultMessage: message });", 
    args: new[] { "success" });

Assert.Equal("success", result.ResultMessage);
```
### INodeJSService.InvokeFromStreamAsync
#### Signature
```csharp
Task<T> InvokeFromStreamAsync<T>(Stream moduleStream, string newCacheIdentifier = null, string exportName = null, object[] args = null, CancellationToken cancellationToken = default(CancellationToken));
```
#### Description
Invokes a function exported by a NodeJS module in Stream form.
#### Parameters
- `T`
  - Description: The type of object this method will return. It can be a JSON-serializable type, `string`, or `Stream`.
 
- `moduleStream`
  - Type: `Stream`
  - Description: The module in `Stream` form.

- `newCacheIdentifier`
  - Type: `string`
  - Description: The modules's cache identifier in the NodeJS module cache. If unspecified, the module will not be cached.

- `exportName`
  - Type: `string`
  - Description: The function in the module's exports to be invoked. If unspecified, the module's exports object is assumed to be a function, and is invoked.

- `args`
  - Type: `object[]`
  - Description: The sequence of JSON-serializable and/or `string` arguments to be passed to the function to invoke.

- `cancellationToken`
  - Type: `CancellationToken`
  - Description: The cancellation token for the asynchronous operation.
#### Returns
The task object representing the asynchronous operation.
#### Exceptions
- `InvocationException`
  - Thrown if a NodeJS error occurs.
  - Thrown if the invocation request times out.
  - Thrown if NodeJS cannot be initialized.
- `ObjectDisposedException`
  - Thrown if this instance has been disposed or if an attempt is made to use one of its dependencies that has been disposed.
- `OperationCanceledException`
  - Thrown if `cancellationToken` is cancelled.
#### Example
Using the class `Result`:
```csharp
public class Result
{
    public string ResultMessage { get; set; }
}
```
The following assertion will pass:
```csharp
using (var memoryStream = new MemoryStream())
using (var streamWriter = new StreamWriter(memoryStream))
{
    // Write the module to a MemoryStream for demonstration purposes.
    streamWriter.Write("module.exports = (callback, message) => callback(null, {resultMessage: message});");
    streamWriter.Flush();
    memoryStream.Position = 0;

    Result result = await nodeJSService.InvokeFromStreamAsync<Result>(memoryStream, args: new[] { "success" });
    
    Assert.Equal("success", result.ResultMessage);
}
```
### INodeJSService.TryInvokeFromCacheAsync
#### Signature
```csharp
Task<(bool, T)> TryInvokeFromCacheAsync<T>(string moduleCacheIdentifier, string exportName = null, object[] args = null, CancellationToken cancellationToken = default(CancellationToken));
```
#### Description
Attempts to invoke a function exported by a NodeJS module cached by NodeJS.
#### Parameters
- `T`
  - Description: The type of object this method will return. It can be a JSON-serializable type, `string`, or `Stream`.
 
- `moduleCacheIdentifier`
  - Type: `string`
  - Description: The cache identifier of the module.

- `exportName`
  - Type: `string`
  - Description: The function in the module's exports to be invoked. If unspecified, the module's exports object is assumed to be a function, and is invoked.

- `args`
  - Type: `object[]`
  - Description: The sequence of JSON-serializable and/or `string` arguments to be passed to the function to invoke.

- `cancellationToken`
  - Type: `CancellationToken`
  - Description: The cancellation token for the asynchronous operation.
#### Returns
The task object representing the asynchronous operation. On completion, the task returns a `(bool, T)` with the bool set to true on 
success and false otherwise.
#### Exceptions
- `InvocationException`
  - Thrown if a NodeJS error occurs.
  - Thrown if the invocation request times out.
  - Thrown if NodeJS cannot be initialized.
- `ObjectDisposedException`
  - Thrown if this instance has been disposed or if an attempt is made to use one of its dependencies that has been disposed.
- `OperationCanceledException`
  - Thrown if `cancellationToken` is cancelled.
#### Example
Using the class `Result`:
```csharp
public class Result
{
    public string ResultMessage { get; set; }
}
```
The following assertion will pass:
```csharp
// Cache the module
string cacheIdentifier = "exampleModule";
await nodeJSService.InvokeFromStringAsync<Result>("module.exports = (callback, message) => callback(null, { resultMessage: message });", 
    cacheIdentifier,
    args: new[] { "success" });

// Invoke from cache
(bool success, Result result) = await nodeJSService.TryInvokeFromCacheAsync<Result>(cacheIdentifier, args: new[] { "success" });

Assert.True(success);
Assert.Equal("success", result.ResultMessage);
```

## Extensibility
This library's behaviour can be customized by implementing public interfaces and overwriting their default DI services. For example, if we have objects that
can't be serialized using the default JSON serialization logic, we can implement `IJsonService`:
```csharp
// Create a custom implementation of IJsonService
public class MyJsonService : IJsonService
{
    public T Deserialize<T>(JsonReader jsonReader)
    {
        ... // Custom deserializetion logic
    }

    public void Serialize(JsonWriter jsonWriter, object value)
    {
        ... // Custom serialization logic
    }
}
```
And overwrite its default DI service:
```csharp
var services = new ServiceCollection();
services.AddNodeJS();

// Overwrite the default DI service
services.AddSingleton<IJsonService, MyJsonService>();

ServiceProvider serviceProvider = services.BuildServiceProvider();
INodeJSService nodeJSService = serviceProvider.GetRequiredService<INodeJSService>();
```
This is the list of implementable interfaces:

| Interface | Description |
| --------- | ----------- |
| `IJsonService` | An abstraction for JSON serialization/deserialization. |
| `IHttpClientService` | An abstraction for `HttpClient`. |
| `INodeJSProcessFactory` | An abstraction for NodeJS process creation. |
| `IHttpContentFactory` | An abstraction for `HttpContent` creation. |
| `INodeJSService` | An abstraction for invoking code in NodeJS. |
| `IEmbeddedResourcesService` | An abstraction for reading of embedded resources. |

## Performance
This library is heavily inspired by [Microsoft.AspNetCore.NodeServices](https://github.com/aspnet/JavaScriptServices/tree/master/src/Microsoft.AspNetCore.NodeServices). While the main
additions to this library are ways to invoke in-memory javascript, this library also provides better performance (note that INodeServices has only 1 benchmark because it 
only supports invoking javascript from a file):
<table>
<thead>
<tr><th>Method</th><th>Mean</th><th>Error</th><th>StdDev</th><th>Gen 0</th><th>Allocated</th></tr>
</thead>
<tbody>
<tr><td>INodeJSService_InvokeFromFile</td><td>0.1058 ms</td><td>0.069 ms</td><td>0.064 ms</td><td>1.2207</td><td>5.8 KB</td>
</tr><tr><td>INodeJSService_InvokeFromCache</td><td>0.1025 ms</td><td>0.091 ms</td><td>0.085 ms</td><td>1.2207</td><td>5.65 KB</td>
</tr><tr><td>INodeServices</td><td>0.1164 ms</td><td>0.108 ms</td><td>0.90 ms</td><td>2.4414</td><td>10.35 KB</td></tr>
</tbody>
</table>
</body>
</html>

System:
```
Jering.Javascript.NodeJS 4.3.0
Microsoft.AspNetCore.NodeServices 3.0.0
NodeJS v12.13.0
BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
Intel Core i7-7700 CPU 3.60GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
```

The [benchmarks](https://github.com/JeringTech/Javascript.NodeJS/blob/master/perf/NodeJS/Benchmarks.cs).

## Building and Testing
You can build and test this project in Visual Studio 2017/2019.

## Projects Using this Library
[Jering.Web.SyntaxHighlighters.HighlightJS](https://github.com/JeringTech/Web.SyntaxHighlighters.HighlightJS) - Use the Syntax Highlighter, HighlightJS, from C#.
[Jering.Web.SyntaxHighlighters.Prism](https://github.com/JeringTech/Web.SyntaxHighlighters.Prism) - Use the Syntax Highlighter, Prism, from C#.

## Related Concepts

### What is NodeJS?
[NodeJS](https://nodejs.org/en/) is a javascript runtime. Essentially, it provides some built-in libraries and executes javascript. Similarities can be drawn to the
[Core Common Language Runtime (CoreCLR)](https://github.com/dotnet/coreclr), which provides a set of base libraries and executes [.NET Intermediate Language](https://en.wikipedia.org/wiki/Common_Intermediate_Language) 
(typically generated by compiling C# or some other .NET language).  

Under the hood, NodeJS uses [V8](https://developers.google.com/v8/) to execute javascript. While this library could have been built to invoke javascript directly in V8,
invoking javascript in NodeJS affords both access to NodeJS's built-in modules and the ability to use most of the modules hosted by [npm](https://www.npmjs.com/).

### NodeJS Modules
NodeJS modules are a kind of javascript module. The concept of javascript modules can seem far more complicated than it really is,
not least because of the existence of competing specifications (CommonJS, AMD, ES6, ...), and the existence of multiple implementations of each specification (SystemJS, RequireJS, 
Dojo, NodeJS, ...). In reality, javascript modules such as NodeJS modules are really simple. In the following sections, we will go through the what, how and why of NodeJS modules.

#### What is a NodeJS Module?
The following is a valid NodeJS module. Lets imagine that it exists in a file, `flavours.js`:
```javascript
// Note that the module variable isn't declared (no "var module = {}")
module.exports = ['chocolate', 'strawberry', 'vanilla'];
```
The following is another valid NodeJS module, we will use it as an entry script (to be supplied to `node` on the command line). Lets imagine that it exists in a file, `printer.js`, 
in the same directory as `flavours.js`:
```javascript
var flavours = require('./flavours.js');

flavours.forEach((flavour) => console.log(flavour));
```
If we run `node printer.js` on the command line, the flavours get printed:
```powershell
PS C:\NodeJS_Modules_Example> node printer.js
chocolate
strawberry
vanilla
```

In general, a NodeJS module is simply a block of javascript with `module.exports` and/or `require` statements. These statements are explained in the next section.

#### How does a NodeJS Module Work?
NodeJS's logic for managing modules is contained in its `require` function. In the example above, `require('./flavours.js')` executes the following steps:
1. Resolves the absolute path of `flavours.js` to `C:/NodeJS_Modules_Example/flavours.js`.
2. Checks whether the NodeJS module cache (a simple javascript object) has a property with name `C:/NodeJS_Modules_Example/flavours.js`, and finds that it does not 
  (the module has not been cached).
3. Reads the contents of `C:/NodeJS_Modules_Example/flavours.js` into memory.
4. Wraps the contents of `C:/NodeJS_Modules_Example/flavour.js` in a function by appending and prepending strings. The resulting function looks like the following:
   ```javascript
   // Note how the require function and a module object are supplied by the wrapper.
   function (exports, require, module, __filename, __dirname){
       module.exports = ['chocolate', 'strawberry', 'vanilla'];
   }
   ```
5. Creates the `module` object and passes it to the generated function.
6. Adds the `module` object (now containing an array as its `exports` property) to the NodeJS module cache using the property name `C:/NodeJS_Modules_Example/flavours.js`.
7. Returns `module.exports`.

If the flavours module is required again, the cached `module` object is retrieved in step 2, and its exports object is returned. This means that module exports are not immutable, for example,
if we replace the contents of `printer.js` with the following:

```javascript
var flavours = require('./flavours.js');

flavours.forEach((flavour) => console.log(flavour));

// Clear the array
flavours.length = 0;

// Add three new flavours
flavours.push('apple');
flavours.push('green tea');
flavours.push('sea salt');

// Require the module again, turns out that require returns a reference to the same array
flavours = require('./flavours.js');

flavours.forEach((flavour) => console.log(flavour));
```

Running `node printer.js` on the command line prints the following flavours:
```powershell
PS C:\Users\Jeremy\Desktop\JSTest> node entry.js
chocolate
strawberry
vanilla
apple
green tea
sea salt
```

#### Why do NodeJS Modules exist?
To answer this question, lets consider the impetus for the creation of javascript modules in general. Web pages used to include scripts like so:
``` html
<html>
    ...
    <script type="text/javascript" src="path/to/coolLibrary.js"></script>
    <script type="text/javascript" src="path/to/myScript.js"></script>
    ...
</html>
```
Browsers would load the scripts like so:
```javascript
// Contents of coolLibrary.js
var coolLibraryPrivateObject = ...;

function CoolLibraryPublicFunction(){
    ... // Do something with coolLibraryPrivateObject, and return some value
}

// Contents of myScript.js
var myVar = CoolLibraryPublicFunction();

... // Do something with myVar
```
Note how everything in the example above is in the same scope. `coolLibraryPrivateObject` can be accessed in `myscript.js`. How
can we hide the private object? We can encapsulate cool library in a function:
```javascript
var module = {};

// This is an immediately invoked function expression, shorthand for assigning the function to a variable then calling it - https://developer.mozilla.org/en-US/docs/Glossary/IIFE
(function(module){
    // Contents of coolLibrary.js
    var coolLibraryPrivateObject = ...;

    function CoolLibraryPublicFunction(){
        ... // Do something with coolLibraryPrivateObject, and return some value
    }
    
    module.exports = CoolLibraryPublicFunction;
})(module)

// Contents of myScript.js
var myVar = module.exports(); // We assigned CoolLibraryPublicFunction to module.exports

... // Do something with myVar
```
We've successfully hidden `coolLibraryPrivateObject` from the global scope using a module-esque pattern. Apart from hiding private objects, this pattern also prevents global namespace pollution.  

NodeJS modules serve a similar purpose. By wrapping modules in functions, NodeJS creates a closure for each module so internal details
can be kept private.

## Contributing
Contributions are welcome!

### Contributers
- [JeremyTCD](https://github.com/JeremyTCD)

## About
Follow [@JeringTech](https://twitter.com/JeringTech) for updates and more.
