using System.Collections.Concurrent;

namespace M03.ParkingLot;

// Parking Lot (LLD): data model + services, all in memory.
// Production note: tickets would live in SQL, and the allocator's `lock` would become an atomic
// conditional UPDATE on the slot row (a lock only works inside one process).
public static class ParkingLotDesign
{
    // ----- Enums -----
    public enum VehicleType  { Motorcycle, Car, Truck }
    public enum SlotType     { Small, Medium, Large }
    public enum GateType     { Entry, Exit }
    public enum TicketStatus { Active, Paid, Exited }
    public enum PaymentType  { Cash, Card }
    public enum PaymentStatus { Failed, Succeeded }

    // ----- Entities (data) -----

    // The whole facility. Layout is built from config, not hard-coded.
    public class ParkingLot
    {
        public required string Id { get; init; }
        public List<Floor> Floors { get; } = [];
        public List<Gate> Gates { get; } = [];
    }

    public class Floor
    {
        public required string Id { get; init; }
        public int Number { get; init; }               // used for "nearest" ordering
        public List<Slot> Slots { get; } = [];
    }

    // The slot itself is the source of truth for "is it free?" (not the tickets).
    public class Slot
    {
        public required string Id { get; init; }
        public int FloorNumber { get; init; }          // nearest = lowest (FloorNumber, Number)
        public int Number { get; init; }               // printed on the ticket
        public SlotType Type { get; init; }
        public bool IsEmpty { get; set; } = true;
    }

    // Created at entry. EntryTime is stored; duration is derived at exit (ExitTime - EntryTime).
    public class Ticket
    {
        public required string Id { get; init; }
        public required string SlotId { get; init; }   // which slot to free at exit
        public required string LicensePlate { get; init; }
        public VehicleType VehicleType { get; init; }  // drives pricing + the fit rule
        public DateTimeOffset EntryTime { get; init; }
        public DateTimeOffset? ExitTime { get; set; }
        public TicketStatus Status { get; set; } = TicketStatus.Active;   // Active → Paid → Exited
        public List<Payment> Payments { get; } = [];   // several attempts: Failed, Failed, Succeeded
    }

    public class Payment
    {
        public required string Id { get; init; }
        public required string TicketId { get; init; }
        public decimal Amount { get; init; }
        public PaymentType Type { get; init; }
        public PaymentStatus Status { get; init; }
    }

    public class Gate
    {
        public required string Id { get; init; }
        public GateType Type { get; init; }
    }

    // ===================== 1. Slot allocation (Strategy) =====================
    public interface ISlotAllocator
    {
        Slot? Allocate(VehicleType vehicle);   // null = no fitting slot ("Lot full")
        void Release(string slotId);
    }

    // Nearest-first: one min-heap of FREE slots per SlotType, ordered by (FloorNumber, Number).
    public class NearestFirstAllocator : ISlotAllocator
    {
        private readonly object _lock = new();   // PriorityQueue isn't thread-safe; gates run concurrently
        private readonly Dictionary<SlotType, PriorityQueue<Slot, (int, int)>> _free = new();
        private readonly Dictionary<string, Slot> _allSlots = new();

        // Fit rule: a vehicle can use its own size or bigger, smallest first.
        private static readonly Dictionary<VehicleType, SlotType[]> FitOrder = new()
        {
            [VehicleType.Motorcycle] = [SlotType.Small, SlotType.Medium, SlotType.Large],
            [VehicleType.Car]        = [SlotType.Medium, SlotType.Large],
            [VehicleType.Truck]      = [SlotType.Large],
        };

        public NearestFirstAllocator(ParkingLot lot)
        {
            foreach (var type in Enum.GetValues<SlotType>())
                _free[type] = new PriorityQueue<Slot, (int, int)>();

            foreach (var slot in lot.Floors.SelectMany(f => f.Slots))
            {
                _allSlots[slot.Id] = slot;
                if (slot.IsEmpty) _free[slot.Type].Enqueue(slot, (slot.FloorNumber, slot.Number));
            }
        }
        public Slot? Allocate(VehicleType vehicle)
        {
            lock (_lock)   // check + take = one step, so two gates can't get the same slot
            {
                foreach (var type in FitOrder[vehicle])
                {
                    if (_free[type].TryDequeue(out var slot, out _))
                    {
                        slot.IsEmpty = false;
                        return slot;
                    }
                }
                return null;
            }
        }

        public void Release(string slotId)
        {
            lock (_lock)
            {
                var slot = _allSlots[slotId];
                if (slot.IsEmpty) return;   // already free: don't add it to the heap twice
                slot.IsEmpty = true;
                _free[slot.Type].Enqueue(slot, (slot.FloorNumber, slot.Number));
            }
        }
    }

    // ===================== 2. Pricing (Strategy) =====================
    public interface IPricingStrategy
    {
        decimal CalculateFee(VehicleType vehicle, DateTimeOffset entry, DateTimeOffset exit);
    }

    // Hourly, rounded up, minimum 1 hour. Rates are DATA (one class, not one per vehicle type).
    public class HourlyPricing(Dictionary<VehicleType, decimal> ratePerHour) : IPricingStrategy
    {
        public decimal CalculateFee(VehicleType vehicle, DateTimeOffset entry, DateTimeOffset exit)
        {
            var hours = Math.Max(1, (int)Math.Ceiling((exit - entry).TotalHours));
            return hours * ratePerHour[vehicle];
        }
    }

    // ===================== 3. Tickets =====================
    public interface ITicketService
    {
        Ticket GenerateTicket(Slot slot, string licensePlate, VehicleType vehicle);
        Ticket? Find(string ticketId);
    }

    public class TicketService(TimeProvider clock) : ITicketService
    {
        private readonly ConcurrentDictionary<string, Ticket> _tickets = new();   // O(1) lookup, thread-safe

        public Ticket GenerateTicket(Slot slot, string licensePlate, VehicleType vehicle)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                SlotId = slot.Id,
                LicensePlate = licensePlate,
                VehicleType = vehicle,
                EntryTime = clock.GetUtcNow(),   // from the clock, not the caller
            };
            _tickets[ticket.Id] = ticket;
            return ticket;
        }

        public Ticket? Find(string ticketId) => _tickets.GetValueOrDefault(ticketId);
    }

    // ===================== 4. Payment (Factory picks the processor) =====================
    public interface IPaymentProcessor
    {
        Payment Pay(Ticket ticket, decimal amount);
    }

    public class CashPaymentProcessor : IPaymentProcessor
    {
        public Payment Pay(Ticket ticket, decimal amount) => NewPayment(ticket, amount, PaymentType.Cash, PaymentStatus.Succeeded);
    }

    // Real system: wraps an external card gateway (Stripe etc.) behind an adapter.
    public class CardPaymentProcessor : IPaymentProcessor
    {
        public Payment Pay(Ticket ticket, decimal amount) => NewPayment(ticket, amount, PaymentType.Card, PaymentStatus.Succeeded);
    }

    public class PaymentProcessorFactory
    {
        public IPaymentProcessor Create(PaymentType type) => type switch
        {
            PaymentType.Cash => new CashPaymentProcessor(),
            PaymentType.Card => new CardPaymentProcessor(),
            _ => throw new NotSupportedException(type.ToString()),
        };
    }

    private static Payment NewPayment(Ticket t, decimal amount, PaymentType type, PaymentStatus status) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..8],
        TicketId = t.Id,
        Amount = amount,
        Type = type,
        Status = status,
    };

    // ===================== 5. Coordinator =====================
    public record ExitResult(bool Success, string Message, decimal Fee = 0);

    // Gates call ONLY this. It calls the services in order.
    public class ParkingService(
        ISlotAllocator allocator,
        ITicketService tickets,
        IPricingStrategy pricing,
        PaymentProcessorFactory payments,
        TimeProvider clock)
    {
        // Entry: Allocate → GenerateTicket. Null = lot full.
        public Ticket? Enter(string licensePlate, VehicleType vehicle)
        {
            var slot = allocator.Allocate(vehicle);
            if (slot is null) return null;

            try
            {
                return tickets.GenerateTicket(slot, licensePlate, vehicle);
            }
            catch
            {
                allocator.Release(slot.Id);   // compensate: don't leave a slot taken with no ticket
                throw;
            }
        }

        // Exit: Find → CalculateFee → Pay → (only if paid) Release → Exited.
        public ExitResult Exit(string ticketId, PaymentType paymentType)
        {
            var ticket = tickets.Find(ticketId);
            if (ticket is null) return new(false, "Ticket not found");
            if (ticket.Status != TicketStatus.Active) return new(false, $"Ticket already {ticket.Status}");

            var exitTime = clock.GetUtcNow();
            var fee = pricing.CalculateFee(ticket.VehicleType, ticket.EntryTime, exitTime);

            var payment = payments.Create(paymentType).Pay(ticket, fee);
            ticket.Payments.Add(payment);
            if (payment.Status == PaymentStatus.Failed)
                return new(false, "Payment failed, please retry", fee);   // slot stays taken, ticket stays Active

            ticket.Status = TicketStatus.Paid;
            ticket.ExitTime = exitTime;
            allocator.Release(ticket.SlotId);   // free the slot only AFTER payment
            ticket.Status = TicketStatus.Exited;
            return new(true, "Paid. Gate opening.", fee);
        }
    }

    // ===================== Wiring (what Program.cs would do) =====================
    public static ParkingService CreateDemo()
    {
        // Layout from "config": 3 floors, each with 2 small, 3 medium, 1 large.
        var lot = new ParkingLot { Id = "lot-1" };
        for (var f = 1; f <= 3; f++)
        {
            var floor = new Floor { Id = $"F{f}", Number = f };
            var n = 1;
            foreach (var (type, count) in new[] { (SlotType.Small, 2), (SlotType.Medium, 3), (SlotType.Large, 1) })
                for (var i = 0; i < count; i++, n++)
                    floor.Slots.Add(new Slot { Id = $"F{f}-{n}", FloorNumber = f, Number = n, Type = type });
            lot.Floors.Add(floor);
        }

        var rates = new Dictionary<VehicleType, decimal>
        {
            [VehicleType.Motorcycle] = 1m, [VehicleType.Car] = 2m, [VehicleType.Truck] = 4m,
        };

        return new ParkingService(
            new NearestFirstAllocator(lot),
            new TicketService(TimeProvider.System),
            new HourlyPricing(rates),
            new PaymentProcessorFactory(),
            TimeProvider.System);
    }
}
