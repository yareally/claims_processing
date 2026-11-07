# What to know without looking it up

Learn the design questions by heart. Use the C# section to practise what you would otherwise have to look up.

## Design questions to answer before you write code

Answer these aloud in the first few minutes. They apply even if the brief does not mention them.

| Question | Answer |
|---|---|
| Which steps need a compensation? | Those that change something in another service. A step that only reads needs none. |
| In what order do compensations run? | Newest first, because later steps may depend on earlier ones. |
| Do you compensate the step that failed? | No, unless its outcome is unknown, such as after a timeout. |
| What if a compensation fails? | Retry it, continue with the others, and record it for a person to repair. |
| What if the caller cancels? | Still compensate. Do not pass the caller's token to the compensations or the final save. |
| Which step cannot be undone? | Run it last. An email is the usual example. |
| Should a failed email undo the claim? | Usually not. Record it and send it again later. |
| What if the process crashes partway? | Save progress after each step, so a recovery job can see what to undo. |
| What if the same request arrives twice? | Check an idempotency key first, as one atomic action. A repeat returns the first result. |
| What must stay out of the logs? | Personal data. Log IDs and step names. |

## C# you will need

**Make an updated copy of a record**

```csharp
public sealed record Order
{
    public required Guid Id { get; init; }
    public string? Note { get; init; }
}

var paidOrder = order with { Note = "Paid" };
```

**Get items back newest first**

```csharp
var stack = new Stack<string>();
stack.Push("first");
stack.Push("second");
while (stack.TryPop(out var item))
{
    Console.WriteLine(item); // "second", then "first"
}
```

**Store an action to run later.** The lambda keeps its access to `orderId`.

```csharp
Func<CancellationToken, Task> action = token => CancelAsync(orderId, token);
await action(CancellationToken.None);
```

**Catch, log, and continue.** The exception goes first, so the log keeps the stack trace. Use `{Name}` placeholders, not `$"..."`, so the logs stay searchable.

```csharp
try
{
    await DoWorkAsync(cancellationToken);
}
catch (Exception exception)
{
    logger.LogError(exception, "Order {OrderId} failed at step {Step}.", orderId, step);
}
```

**Wait, and use a token that never cancels.** Never block with `.Result` or `.Wait()`.

```csharp
await Task.Delay(TimeSpan.FromMilliseconds(200));
await DoWorkAsync(CancellationToken.None);
```

**Choose a value with a switch expression**

```csharp
var label = status switch
{
    OrderStatus.Open => "open",
    OrderStatus.Closed => "closed",
    _ => throw new InvalidOperationException($"Unknown status: {status}"),
};
```

**Return HTTP results from a minimal API**

```csharp
Results.Ok(body);                                 // 200
Results.Created($"/orders/{orderId}", body);      // 201 with a Location header
Results.NotFound();                               // 404
Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
```

**Write xUnit tests**

```csharp
[Fact]
public async Task A_bad_order_throws()
{
    var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitBadOrderAsync());
    Assert.Equal("Bad order", error.Message);
}

[Theory]
[InlineData(1, 2)]
[InlineData(2, 4)]
public void Doubles_the_input(int input, int expected)
{
    Assert.Equal(expected, input * 2);
}
```

## Commands

```
dotnet build
dotnet test
dotnet test --filter Level1
dotnet run --project src/Claims.Api --urls http://localhost:5100
```

`dotnet test` prints a failing test with its expected and actual values, followed by a stack trace. Find the stack trace line that names your own file. It shows where the code failed. This project stops at the first failure, but the assessment may print every failure at once.
