# Project22 — Laboratory Order Intake and Validation Service

## Overview

This project implements a C# service that accepts a laboratory order as a JSON string, validates the input, and returns an `OrderResult`.

The service produces three possible outcomes:

- `Accepted` — the order satisfies all validation rules.
- `Rejected` — the order contains one or more validation errors.
- `Rejected (malformed input)` — the input is not valid JSON.

## Project Structure

```text
Project22/
+-- src/
¦   +-- OrderIntake/
¦       +-- Order.cs
¦       +-- OrderIntakeService.cs
¦       +-- OrderResult.cs
¦       +-- ValidationError.cs
¦       +-- OrderIntake.csproj
+-- tests/
¦   +-- OrderIntake.Tests/
¦       +-- OrderIntakeServiceTests.cs
¦       +-- OrderIntake.Tests.csproj
+-- Project22.slnx
