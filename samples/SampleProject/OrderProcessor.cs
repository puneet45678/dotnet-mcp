namespace SampleProject;

public class OrderProcessor
{
    private readonly List<string> _processedOrders = [];

    public string Process(string orderId, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));

        if (amount <= 0)
            return $"REJECTED:{orderId}";

        string status;
        if (amount > 10_000)
            status = "PENDING_APPROVAL";
        else if (amount > 1_000)
            status = "PROCESSING";
        else
            status = "COMPLETED";

        _processedOrders.Add(orderId);
        return $"{status}:{orderId}";
    }

    public int ProcessedCount => _processedOrders.Count;
}
