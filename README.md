 # Datagrammer

Datagrammer is lightweight asynchronous Udp client based on Dataflow library. You can read this [article](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library) if you want to know more about Dataflow.
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

### Initialization

To create Datagrammer block use this code:

```csharp
var datagramBlock = new DatagramBlock();
```

or with options you need to specify:

```csharp
var datagramBlock = new DatagramBlock(new DatagramOptions
{
    ListeningPoint = new IPEndPoint(IPAddress.Loopback, 12345)
});
```
			
after it you already can use this block but it won't be sending or receiving any data. To start packets processing call this method:

```csharp
datagramBlock.Start();
```

This method starts initialization by an asynchronous way. It is safe to call this many times or from different threads and it does not throw any exceptions. To await initialization use this task property:

```csharp
await datagramBlock.Initialization;
```

If initialization is failed exception may be got by this property. Other methods or properties work by standard way like another dataflow blocks. But it is worth to say about `datagramBlock.Completion` property. This property is related to `datagramBlock.Initialization` and can be completed or failed until initialization is completed or failed.

### Sending

To send packet in simple case use this:

```csharp
await datagramBlock.SendAsync(new Datagram 
{ 
    Bytes = new byte[] { 1, 2, 3 }, 
    EndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 12345) 
});
```

by Dataflow way:

```csharp
ISourceBlock<Datagram> sourceBlock = new YourCustomSourceBlock(); //it may be buffer or transform or your custom generator block
sourceBlock.LinkTo(datagramBlock);
```

### Receiving

To receive messages by reactive way I suggest to use Reactive extensions. It is more conveniently if you consider to use IObservable interface.

```csharp
datagramBlock.AsObservable().Subscribe(message =>
{
    var str = Encoding.UTF8.GetString(message.Bytes, 0, message.Bytes.Length);
    Console.WriteLine(str);
});
```

by Dataflow way:

```csharp
ITargetBlock<Datagram> targetBlock = new ActionBlock<Datagram>(message =>
{
    var str = Encoding.UTF8.GetString(message.Bytes, 0, message.Bytes.Length);
    Console.WriteLine(str);
});
datagramBlock.LinkTo(targetBlock);
```
### Middleware

`MiddlewareBlock` can help you to build flows with data processing, filtering or transformation. 

```csharp
public class PipeBlock : MiddlewareBlock
{
    public PipeBlock() : base(new MiddlewareOptions())
    {
    }
    
    protected override async Task ProcessAsync(Datagram datagram)
    {
    	await NextAsync(datagram); //send data to next block. you can modify it before. or you can just send current data and process it below. don't call the method if you want to break current request pipeline.
	
	await ProcessInternalAsync(datagram);
    }
    
    private async Task ProcessInternalAsync(Datagram datagram)
    {
    	//...
    }
}
```

Let's build our own data pipe:

```csharp
var pipeBlock = new PipeBlock();
var anotherPipeBlock = new LogDatagramBlock(); //middleware too, like above
var oneMoreBlock = new SendDatagramToSomewhereBlock(); //may be just Datagram target

datagramBlock.LinkTo(pipeBlock);
pipeBlock.LinkTo(anotherPipeBlock);
anotherPipeBlock.LinkTo(oneMoreBlock);
```

### License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
