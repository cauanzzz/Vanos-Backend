# Vanos | Backend

**Connecting families, students, and drivers through a simpler school transportation experience.**

Vanos is a school and university transportation platform built around a **B2C-led approach**: the journey starts with people looking for transportation. For drivers, the platform brings service discovery, booking requests, passenger management, and invoices into a shared workflow.

This repository contains the backend, built with **C# and ASP.NET Core**, combining a REST API, SQL Server persistence, and real-time communication through SignalR.

## The Problem

Finding a van that serves a specific neighborhood and educational institution often relies on personal recommendations and scattered conversations.

Drivers also need to manage limited seating, passenger requests, daily attendance, and payments.

Vanos aims to connect these activities in one platform, from discovering transportation options to following vehicle location updates.

## Features

### Authentication and Access Control

* JWT-based registration and login.
* Driver, parent, and student roles.
* Password hashing with BCrypt.
* Role-based authorization and record ownership checks.

### Transportation Marketplace

* Search by educational institution, city, and neighborhood.
* Paginated results with active drivers and available seats.
* Seat availability calculated from current passenger assignments.
* Average driver ratings.
* Driver-managed service areas and supported institutions.

### Booking Requests

* Transportation requests associated with student profiles.
* Driver acceptance and rejection workflows.
* Capacity checks during acceptance.
* Passenger assignment after a request is accepted.

### Real-Time Tracking

* Location updates published by authenticated drivers.
* SignalR delivery through private consumer groups.
* Recipients selected from current passenger assignments.
* Coordinate validation.
* Connections closed when authentication expires.

### Invoices and Payment Simulation

* Invoice creation for assigned passengers.
* Ownership-scoped access to pending and paid invoices.
* Payment simulation restricted to the development environment.
* Conditional updates that preserve payment timestamps on repeated requests.
* Explicit identification of simulated payments.

### Passenger Management and Ratings

* Student profile registration.
* Daily outbound and return attendance preferences.
* Passenger lists scoped to the assigned driver.
* Driver ratings linked to accepted booking requests.

## Technology Stack

| Technology               | Purpose                              |
| ------------------------ | ------------------------------------ |
| C# / .NET 10             | Backend development                  |
| ASP.NET Core             | REST API and authorization           |
| Entity Framework Core    | Persistence, queries, and migrations |
| SQL Server               | Application database                 |
| SignalR                  | Real-time communication              |
| JWT Bearer               | Authentication                       |
| BCrypt                   | Password hashing                     |
| xUnit                    | Automated testing                    |
| SQLite                   | Relational database for tests        |
| ASP.NET Core MVC Testing | HTTP integration testing             |

## Project Structure

| Directory               | Responsibility                            |
| ----------------------- | ----------------------------------------- |
| `Vanos.API/Controllers` | API endpoints and application workflows   |
| `Vanos.API/DTOs`        | Request and response contracts            |
| `Vanos.API/Models`      | Domain entities                           |
| `Vanos.API/Data`        | Database context and EF configuration     |
| `Vanos.API/Services`    | Token generation and password hashing     |
| `Vanos.API/Hubs`        | Real-time communication                   |
| `Vanos.API/Extensions`  | Authenticated user identification helpers |
| `Vanos.API/Migrations`  | Database schema history                   |
| `Vanos.API.Tests`       | Unit and integration tests                |

## Engineering Decisions

### Authorization Beyond Roles

Roles determine which operations an account can perform. Database relationships determine which records that account can access.

### Focused Marketplace Queries

Marketplace queries return discovery-specific fields and calculate availability and ratings in the database, avoiding additional queries for each driver.

### Consistent Seat Allocation

Booking acceptance checks capacity within a serializable transaction to protect seat allocation during competing requests.

### Idempotent Payment Simulation

Payment confirmation uses `ExecuteUpdateAsync` with a pending-invoice condition. Repeated confirmations do not overwrite an existing payment timestamp.

### Server-Controlled Tracking Access

Clients cannot select arbitrary tracking groups. The server determines recipients from authenticated identities and current passenger assignments.

## Main Endpoints

| Method  | Route                                    | Purpose                         |
| ------- | ---------------------------------------- | ------------------------------- |
| `POST`  | `/api/auth/register`                     | Register an account             |
| `POST`  | `/api/auth/login`                        | Authenticate                    |
| `GET`   | `/api/drivers/marketplace`               | Discover transportation options |
| `PUT`   | `/api/drivers/{id}/service-areas`        | Update service areas            |
| `PUT`   | `/api/drivers/{id}/schools`              | Update supported institutions   |
| `POST`  | `/api/students`                          | Create a student profile        |
| `GET`   | `/api/students`                          | List owned profiles             |
| `POST`  | `/api/hirerequests`                      | Submit a booking request        |
| `PATCH` | `/api/hirerequests/{id}/accept`          | Accept a booking request        |
| `GET`   | `/api/monthlyfees/mine`                  | List owned invoices             |
| `POST`  | `/api/monthlyfees`                       | Create an invoice               |
| `POST`  | `/api/monthlyfees/{id}/simulate-payment` | Simulate payment                |

The tracking Hub is available at `/hubs/tracking`.

## Running Locally

### Prerequisites

* .NET 10 SDK.
* An accessible SQL Server instance.
* `dotnet-ef` compatible with the project's Entity Framework Core version.

### Setup

```bash
git clone https://github.com/cauanzzz/Vanos-Backend.git
cd Vanos-Backend
dotnet restore
```

Configure your database connection and a randomly generated JWT signing secret:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING" --project Vanos.API
dotnet user-secrets set "Jwt:Key" "YOUR_RANDOM_SECRET_OF_AT_LEAST_32_BYTES" --project Vanos.API
```

Replace the placeholders with your own values. Keep credentials outside version-controlled files.

Apply the committed database migrations and start the API:

```bash
dotnet ef database update --project Vanos.API --startup-project Vanos.API
dotnet run --project Vanos.API --launch-profile https
```

Local URLs are configured in `Vanos.API/Properties/launchSettings.json`.

### Tests

```bash
dotnet test Vanos.API.Tests/Vanos.API.Tests.csproj
```

The test suite includes scenarios for HTTP authorization, invoice ownership, marketplace filtering, seat availability, and repeated or concurrent payment confirmations.

## Project Status

**MVP under development.** The payment module uses simulation and does not process real financial transactions. Production readiness and SQL Server integration remain subject to validation.

Planned improvements:

* Payment provider integration with authenticated webhooks.
* Transportation contracts and recurring invoice generation.
* Trip lifecycle management with explicit tracking start and end.
* Driver document verification.
* Spatially indexed geographic queries.
* SignalR distribution across multiple application instances.

---

**Vanos — bringing transportation discovery, passenger management, and real-time visibility into one platform.**
