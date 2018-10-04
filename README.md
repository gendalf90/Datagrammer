# Datagrammer

Lightweight asynchronous extensible udp client

### Getting started

Install from [NuGet](https://www.nuget.org/packages/Datagrammer/):

```powershell
Install-Package Datagrammer
```

Use namespace

```csharp
using Datagrammer;
```

### Initialization

Use `Bootstrap` class for client creating

```csharp
var client = new Bootstrap().Build(); // by default it listen on the (IPAddress.Any, 5000) endpoint
```

or `Microsoft.Extensions.DependencyInjection` extension

```csharp
await new HostBuilder().ConfigureServices((context, services) =>
                       {
                           services.AddDatagrammer(); //it register both interfaces IDatagramClient and IDatagramSender
                       })
                       .RunConsoleAsync();
```

if you want to use it as hosted service just do so

```csharp
await new HostBuilder().ConfigureServices((context, services) =>
                       {
                           services.AddHostedDatagrammer(); //it register only IDatagramSender interface
                       })
                       .RunConsoleAsync();
```

### Configuration

By `Bootstrap` class using

```csharp
var client = new Bootstrap().Configure(options =>
                            {
                                options.ListeningPoint.Address = IPAddress.Loopback;
                            })
                            .Build();
```

by `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Configuration` using

```csharp
var services = new ServiceCollection().Configure<DatagramOptions>(options =>
                                      {
                                          options.ListeningPoint = new IPEndPoint(IPAddress.Loopback, 12345);
                                      })
                                      .AddDatagrammer();
```

### Sending

Do it like this

```csharp
await client.SendAsync(new Datagram
{
    Bytes = new byte[] { 1, 2, 3 },
    EndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 50000)
});
```

### Receiving

Use `IMessageHandler` interface for message handling

```csharp
class MyHandler : IMessageHandler
{
    public Task HandleAsync(IContext context, Datagram message)
    {
        throw new NotImplementedException();
    }
}
```

and register it in `Bootstrap`

```csharp
var client = new Bootstrap().AddMessageHandler(new MyHandler()).Build();
```

or in `ServiceCollection`

```csharp
var services = new ServiceCollection().AddSingleton<IMessageHandler, MyHandler>().AddDatagrammer();
```

### Error handling

Use `IErrorHandler` interface for it

```csharp
class MyHandler : IErrorHandler
{
    public Task HandleAsync(IContext context, Exception e)
    {
        throw new NotImplementedException();
    }
}
```

and register it in `Bootstrap`

```csharp
var client = new Bootstrap().AddErrorHandler(new MyHandler()).Build();
```

or in `ServiceCollection` like previous example

This handler is called when exception is thrown from message handler or middleware or the protocol wrapper has error. If error handler has unhandled exception the client will be disposed.

### Middleware

Use `IMiddleware` interface like this

```csharp
class MyMiddleware : IMiddleware
{
    public Task<byte[]> ReceiveAsync(byte[] bytes)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> SendAsync(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}
```

initialization is like previous examples

### Protocol

You can use your custom protocol. One should implement both of these interfaces

```csharp
class MyProtocol : IProtocol
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<Datagram> ReceiveAsync()
    {
        throw new NotImplementedException();
    }

    public Task SendAsync(Datagram message)
    {
        throw new NotImplementedException();
    }
}

class MyProtocolCreator : IProtocolCreator
{
    public IProtocol Create(IPEndPoint listeningPoint)
    {
        throw new NotImplementedException();
    }
}
```
and register them like this

```csharp
var client = new Bootstrap().UseCustomProtocol(new MyProtocolCreator()).Build();
```

or

```csharp
var services = new ServiceCollection().AddSingleton<IProtocolCreator, MyProtocolCreator>().AddDatagrammer();
```

### Pipeline

![alt text](https://9wpxtq.db.files.1drv.com/y4mfT5AmcOnbLro8HiHJ_hjs_BaEEFr9V8zxOAWUDdZPUQhJDQS8Z3OPZI4_A3nKhoyp8WVfKk7v6Wz3ii6eeHBz3YJqMyYV3QEULHHgM_8HirHfKMYvGdRVCHO4hMYp_PBX3yjSnNP7oWq9DD0BO2H6U0-izMTveKDAc7yNpZ463okwigoYMzOj2RdzmlhHMX1?width=741&height=351&cropmode=none)
