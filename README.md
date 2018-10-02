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
