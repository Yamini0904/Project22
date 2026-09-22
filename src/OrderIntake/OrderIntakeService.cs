using System.Globalization;
using System.Text.Json;

namespace OrderIntake;

public class OrderIntakeService
{
    private static readonly string[] AllowedSpecimenTypes =
        ["Blood", "Urine", "Tissue", "Saliva"];

    private static readonly string[] AllowedPriorities =
        ["Routine", "Urgent"];

    public OrderResult Process(string json)
    {
        var result = new OrderResult();

        if (string.IsNullOrWhiteSpace(json))
        {
            return Malformed(result);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Malformed(result);
            }

            JsonElement root = document.RootElement;

            string? orderId = GetString(root, "orderId");
            string? patientId = GetString(root, "patientId");
            string? specimenId = GetString(root, "specimenId");
            string? specimenType = GetString(root, "specimenType");
            string? priority = GetString(root, "priority");
            string? collectionDateText = GetString(root, "collectionDate");

            var requestedTests = new List<string>();

            if (root.TryGetProperty("requestedTests", out JsonElement tests) &&
                tests.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement test in tests.EnumerateArray())
                {
                    if (test.ValueKind == JsonValueKind.String)
                    {
                        requestedTests.Add(test.GetString() ?? string.Empty);
                    }
                    else
                    {
                        requestedTests.Add(string.Empty);
                    }
                }
            }

            ValidateRequiredAndLength("orderId", orderId, result);
            ValidateRequiredAndLength("patientId", patientId, result);
            ValidateRequiredAndLength("specimenId", specimenId, result);

            if (string.IsNullOrWhiteSpace(specimenType))
            {
                AddError(result, "specimenType", "REQUIRED",
                    "Specimen type is required.");
            }
            else if (!AllowedSpecimenTypes.Any(x =>
                string.Equals(x, specimenType, StringComparison.OrdinalIgnoreCase)))
            {
                AddError(result, "specimenType", "INVALID_VALUE",
                    "Specimen type must be Blood, Urine, Tissue or Saliva.");
            }

            if (string.IsNullOrWhiteSpace(priority))
            {
                AddError(result, "priority", "REQUIRED",
                    "Priority is required.");
            }
            else if (!AllowedPriorities.Any(x =>
                string.Equals(x, priority, StringComparison.OrdinalIgnoreCase)))
            {
                AddError(result, "priority", "INVALID_VALUE",
                    "Priority must be Routine or Urgent.");
            }

            DateTime collectionDate = default;

            if (string.IsNullOrWhiteSpace(collectionDateText))
            {
                AddError(result, "collectionDate", "REQUIRED",
                    "Collection date is required.");
            }
            else if (!DateTime.TryParseExact(
                collectionDateText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out collectionDate))
            {
                AddError(result, "collectionDate", "INVALID_FORMAT",
                    "Collection date must use yyyy-MM-dd and be a real calendar date.");
            }
            else if (collectionDate.Date > DateTime.Today)
            {
                AddError(result, "collectionDate", "FUTURE_DATE",
                    "Collection date cannot be after today.");
            }

            if (!root.TryGetProperty("requestedTests", out _) ||
                tests.ValueKind != JsonValueKind.Array ||
                requestedTests.Count == 0)
            {
                AddError(result, "requestedTests", "REQUIRED",
                    "At least one requested test is required.");
            }
            else
            {
                if (requestedTests.Any(string.IsNullOrWhiteSpace))
                {
                    AddError(result, "requestedTests", "INVALID_VALUE",
                        "Requested test names cannot be empty.");
                }

                bool duplicate = requestedTests
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Any(g => g.Count() > 1);

                if (duplicate)
                {
                    AddError(result, "requestedTests", "DUPLICATE",
                        "Requested test names must be unique.");
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Status = "Rejected";
                return result;
            }

            var order = new Order
            {
                OrderId = orderId!,
                PatientId = patientId!,
                SpecimenId = specimenId!,
                SpecimenType = AllowedSpecimenTypes.First(x =>
                    string.Equals(x, specimenType, StringComparison.OrdinalIgnoreCase)),
                Priority = AllowedPriorities.First(x =>
                    string.Equals(x, priority, StringComparison.OrdinalIgnoreCase)),
                CollectionDate = collectionDate,
                RequestedTests = requestedTests
            };

            result.Status = "Accepted";
            result.Order = order;
            return result;
        }
        catch (JsonException)
        {
            return Malformed(result);
        }
    }

    private static string? GetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void ValidateRequiredAndLength(
        string field,
        string? value,
        OrderResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(result, field, "REQUIRED",
                $"{field} is required.");
        }
        else if (value.Length > 20)
        {
            AddError(result, field, "MAX_LENGTH",
                $"{field} cannot exceed 20 characters.");
        }
    }

    private static void AddError(
        OrderResult result,
        string field,
        string code,
        string message)
    {
        result.Errors.Add(new ValidationError
        {
            Field = field,
            Code = code,
            Message = message
        });
    }

    private static OrderResult Malformed(OrderResult result)
    {
        result.Status = "Rejected";
        result.Order = null;
        result.Errors.Add(new ValidationError
        {
            Field = "$",
            Code = "MALFORMED_INPUT",
            Message = "Input is not a valid laboratory order."
        });
        return result;
    }
}
