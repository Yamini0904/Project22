using OrderIntake;

namespace OrderIntake.Tests;

public class OrderIntakeServiceTests
{
    private readonly OrderIntakeService _service = new();

    [Fact]
    public void AcceptedOrder_ReturnsOrderWithNoErrors()
    {
        string json = """
        {
            "orderId": "ORD-1005",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "blood",
            "priority": "urgent",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "CompleteBloodCount"],
            "senderNote": "ignore me"
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal("Accepted", result.Status);
        Assert.NotNull(result.Order);
        Assert.Empty(result.Errors);
        Assert.Equal("Blood", result.Order!.SpecimenType);
        Assert.Equal("Urgent", result.Order.Priority);
        Assert.Equal(2, result.Order.RequestedTests.Count);
    }

    [Fact]
    public void InvalidOrder_ReturnsAllValidationErrors()
    {
        string json = """
        {
            "orderId": "",
            "patientId": "   ",
            "specimenId": "123456789012345678901",
            "specimenType": "Plasma",
            "priority": "Emergency",
            "collectionDate": "2027-01-01",
            "requestedTests": ["Glucose", "glucose", ""]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);

        Assert.Contains(result.Errors, e =>
            e.Field == "orderId" && e.Code == "REQUIRED");

        Assert.Contains(result.Errors, e =>
            e.Field == "patientId" && e.Code == "REQUIRED");

        Assert.Contains(result.Errors, e =>
            e.Field == "specimenId" && e.Code == "MAX_LENGTH");

        Assert.Contains(result.Errors, e =>
            e.Field == "specimenType" && e.Code == "INVALID_VALUE");

        Assert.Contains(result.Errors, e =>
            e.Field == "priority" && e.Code == "INVALID_VALUE");

        Assert.Contains(result.Errors, e =>
            e.Field == "collectionDate" && e.Code == "FUTURE_DATE");

        Assert.Contains(result.Errors, e =>
            e.Field == "requestedTests" && e.Code == "INVALID_VALUE");

        Assert.Contains(result.Errors, e =>
            e.Field == "requestedTests" && e.Code == "DUPLICATE");
    }

    [Fact]
    public void IdLongerThan20Characters_ReturnsMaxLength()
    {
        string json = """
        {
            "orderId": "123456789012345678901",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose"]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Contains(result.Errors, e =>
            e.Field == "orderId" && e.Code == "MAX_LENGTH");
    }

    [Fact]
    public void InvalidCollectionDates_AreRejected()
    {
        string[] invalidDates =
        {
            "2026-9-2",
            "20-09-2026",
            "2026/09/20",
            "2026-02-30"
        };

        foreach (string date in invalidDates)
        {
            string json = CreateValidJson(date);

            OrderResult result = _service.Process(json);

            Assert.Equal("Rejected", result.Status);
            Assert.Contains(result.Errors, e =>
                e.Field == "collectionDate" &&
                e.Code == "INVALID_FORMAT");
        }
    }

    [Fact]
    public void RequestedTests_RejectsEmptyAndDuplicateItems()
    {
        string json = """
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "glucose", ""]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(result.Errors, e =>
            e.Field == "requestedTests" &&
            e.Code == "INVALID_VALUE");

        Assert.Contains(result.Errors, e =>
            e.Field == "requestedTests" &&
            e.Code == "DUPLICATE");
    }

    [Fact]
    public void BrokenJson_ReturnsSingleMalformedInputError()
    {
        string json = """{"orderId":""";

        OrderResult result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    private static string CreateValidJson(string collectionDate)
    {
        return $$"""
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "{{collectionDate}}",
            "requestedTests": ["Glucose"]
        }
        """;
    }
}