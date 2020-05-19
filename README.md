 # Datagrammer

Datagrammer is lightweight asynchronous Udp client based on Channels and Dataflow library. You can read this [article](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) if you want to know more about Dataflow.
And this [article](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) about Channels.
Datagrammer helps you to build your own data flows which includes udp packets sending and receiving.
You can use it separately of Dataflow with Reactive extensions so Datagrammer has IObservable interface compatibility.
You can know more about Rx [here](http://reactivex.io/).

### Getting started

Install from [NuGet](https://www.nuget.org/packages/Datagrammer/):

```powershell
Install-Package Datagrammer
```

Use namespace

```csharp
using Datagrammer;
```

### Use cases

You can find simple use cases [here](https://github.com/gendalf90/Datagrammer/tree/master/Datagrammer/Tests/UseCases)

### License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
