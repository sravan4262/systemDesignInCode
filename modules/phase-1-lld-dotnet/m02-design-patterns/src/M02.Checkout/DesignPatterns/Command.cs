using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M02.Checkout.DesignPatterns;

// Command: turn a request ("refund order 42") into an OBJECT, so it can be logged, queued, retried, or undone.
public static class Command
{
    // ❌ BEFORE
    // Problem: operations are plain method calls.
    //   - The audit log is copy-pasted into every method; a new operation copies it again.
    //   - A method call is gone once made: can't queue "refund 42" for later, retry it after a crash,
    //     or store exactly what was requested.
    public class OrderOperationsBefore(ILogger<OrderOperationsBefore> logger)
    {
        public string Cancel(string orderId, string reason)
        {
            logger.LogInformation("AUDIT Cancel {OrderId} {Reason}", orderId, reason);   // duplicated
            return $"Order {orderId} cancelled";
        }

        public string Refund(string orderId, decimal amount)
        {
            logger.LogInformation("AUDIT Refund {OrderId} {Amount}", orderId, amount);   // duplicated
            return $"Refunded {amount} on {orderId}";
        }
    }

    // ✅ AFTER

    // The commands: the request is now DATA (a record). It can be logged, serialized, queued, stored.
    public record CancelOrder(string OrderId, string Reason);
    public record RefundOrder(string OrderId, decimal Amount);

    // One handler per command does the actual work.
    public interface ICommandHandler<TCommand>
    {
        string Handle(TCommand command);
    }

    // How this solves it: each operation is its own small class; no audit code in here.
    public class CancelOrderHandler : ICommandHandler<CancelOrder>
    {
        public string Handle(CancelOrder c) => $"Order {c.OrderId} cancelled";
    }

    public class RefundOrderHandler : ICommandHandler<RefundOrder>
    {
        public string Handle(RefundOrder c) => $"Refunded {c.Amount} on {c.OrderId}";
    }

    // The dispatcher: every command passes through ONE place.
    // How this solves it: audit (and later validation, retries, timing) is written once for ALL commands.
    // (Uses IServiceProvider to find the handler; fine here because finding handlers IS its job, like a factory.)
    public class CommandDispatcher(IServiceProvider services, ILogger<CommandDispatcher> logger)
    {
        public string Send<TCommand>(TCommand command) where TCommand : notnull
        {
            logger.LogInformation("AUDIT {Command}", command);   // record ToString() prints all its data
            try
            {
                return services.GetRequiredService<ICommandHandler<TCommand>>().Handle(command);
            }
            catch (Exception ex)
            {
                // The failed command is a complete object: it can be stored and retried as-is.
                logger.LogError(ex, "FAILED {Command}", command);
                throw;
            }
        }
    }

    // ===== What goes in Program.cs =====
    public static class Program
    {
        public static void Run(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // New operation = a record + a handler + one line here. Dispatcher never changes.
            builder.Services.AddScoped<ICommandHandler<CancelOrder>, CancelOrderHandler>();
            builder.Services.AddScoped<ICommandHandler<RefundOrder>, RefundOrderHandler>();
            builder.Services.AddScoped<CommandDispatcher>();

            var app = builder.Build();

            // The endpoint only builds the command and sends it; it doesn't know who handles it.
            app.MapPost("/orders/{id}/cancel", (string id, string reason, CommandDispatcher bus) =>
                Results.Ok(bus.Send(new CancelOrder(id, reason))));

            app.MapPost("/orders/{id}/refund", (string id, decimal amount, CommandDispatcher bus) =>
                Results.Ok(bus.Send(new RefundOrder(id, amount))));

            app.Run();
        }
    }
}

// Classic GoF Command (for comparison), used for UNDO:
//   interface ICommand { void Execute(); void Undo(); }
//   class AddItemCommand(Cart cart, string sku) : ICommand
//   { public void Execute() => cart.Add(sku);  public void Undo() => cart.Remove(sku); }
//   Invoker keeps a Stack<ICommand>: Execute → push; Undo → pop().Undo().
// MediatR = this file's dispatcher, packaged: IRequest + IRequestHandler + pipeline behaviors.
