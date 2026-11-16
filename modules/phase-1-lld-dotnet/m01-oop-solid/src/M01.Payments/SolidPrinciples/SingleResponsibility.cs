namespace M01.Payments.SolidPrinciples;

/// <summary>
/// S — Single Responsibility: a class should have one reason to change (answer to one stakeholder).
/// </summary>
public static class SingleResponsibility
{
    public static class Violation
    {
        // ❌ Five reasons to change in one class: payment rules, provider API, DB schema,
        //    email template, audit format. Changing the receipt email risks breaking charging.
        public class PaymentProcessor
        {
            public void Process(decimal amount, string email)
            {
                if (amount <= 0) throw new ArgumentException("Invalid amount.");   // validation  (finance)
                Console.WriteLine($"Calling Stripe to charge {amount}");          // integration (platform)
                Console.WriteLine("INSERT INTO Payments ...");                    // persistence (DB)
                Console.WriteLine($"Sending receipt to {email}");                 // email       (marketing)
                Console.WriteLine("AUDIT: payment processed");                    // audit       (compliance)
            }
        }
    }

    public static class Fix
    {
        public sealed record PaymentRequest(decimal Amount, string Email);

        // ✅ Each collaborator has exactly one reason to change.
        public class PaymentValidator
        {
            public void Validate(PaymentRequest request)
            {
                if (request.Amount <= 0) throw new ArgumentException("Invalid amount.");
            }
        }

        public interface IPaymentGateway { string Charge(decimal amount); }
        public interface IPaymentRepository { void Save(PaymentRequest request, string chargeId); }
        public interface IReceiptSender { void Send(string email, string chargeId); }
        public interface IAuditLog { void Record(string message); }

        // ✅ Orchestrates the flow only. A change to the email template touches IReceiptSender, not this class.
        public class PaymentService(
            PaymentValidator validator,
            IPaymentGateway gateway,
            IPaymentRepository repository,
            IReceiptSender receipts,
            IAuditLog audit)
        {
            public string Process(PaymentRequest request)
            {
                validator.Validate(request);
                var chargeId = gateway.Charge(request.Amount);
                repository.Save(request, chargeId);
                receipts.Send(request.Email, chargeId);
                audit.Record($"Payment {chargeId} processed");
                return chargeId;
            }
        }
    }
}
