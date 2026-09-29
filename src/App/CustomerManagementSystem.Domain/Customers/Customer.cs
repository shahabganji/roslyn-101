using CustomerManagementSystem.Domain.Customers.ConfirmRegistration;
using CustomerManagementSystem.Domain.Customers.Register;
using CustomerManagementSystem.Domain.Customers.UpdateContactsInfo;

namespace CustomerManagementSystem.Domain.Customers;

public sealed partial class Customer : IAmAggregateRoot
{
    public Guid CustomerId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public DateTime DateOfBirth { get; private set; }

    public bool IsRegistrationConfirmed { get; private set; }

    public void Apply(IEvent @event)
    {
        ((dynamic)this).Apply((dynamic)@event);
    }

    private void Apply(CustomerRegistered @event)
    {
        CustomerId = @event.CustomerId;
        FullName = @event.FullName;
        Email = @event.Email;
        DateOfBirth = @event.DateOfBirth;
    }

    private void Apply(EmailUpdated @event)
    {
        Email = @event.Email;
    }
}