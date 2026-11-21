using M22.LlmApps;

var model = new FakeModel();   // swap for a real IChatClient (Azure OpenAI, OpenAI, ...) - the code below doesn't change

Console.WriteLine("BEFORE (no tools):    " + await NoTools.AskAsync(model, "Where's my order 123?"));
Console.WriteLine("AFTER  alice, 123:    " + await OrderTools.AskAsync(model, "alice", "Where's my order 123?"));
Console.WriteLine("AFTER  alice, 456:    " + await OrderTools.AskAsync(model, "alice", "Where's my order 456?"));
