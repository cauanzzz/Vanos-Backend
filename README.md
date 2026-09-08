# Vanos | Backend

**Connecting families, students, and drivers through a simpler school transportation experience.**

Vanos is a school and university transportation platform built around a **B2C-led approach**. The journey starts with people looking for transportation, while drivers manage service areas, booking requests, passengers, and invoices.

This repository contains the backend, developed with **C# and ASP.NET Core**, combining a REST API, SQL Server persistence, and real-time communication through SignalR.

## Why Vanos?

Finding transportation that serves a specific neighborhood and educational institution often depends on personal recommendations and scattered conversations.

Drivers also need to keep track of seating capacity, passenger requests, attendance, and payments.

Vanos brings these activities into a connected workflow: discover a driver, request transportation, manage the passenger relationship, and receive vehicle location updates.

## Features

### Authentication and Authorization

* JWT-based account registration and login.
* Driver, parent, and student roles.
* Password hashing with BCrypt.
* Authentication required by default for endpoints without explicit authorization metadata.
* Role-based permissions and record ownership checks.

### Transportation Marketplace

* Search by educational institution, city, and neighborhood.
* Paginated results containing active drivers with available seats.
* Seat availability calculated from current passenger assignments.
* Average driver ratings.
* Driver-managed service areas and supported institutions.

### Booking Requests

* Transportation requests associated with student profiles.
* Driver acceptance and rejection workflows.
* Capacity and assignment checks during acceptance.
* Transactional passenger allocation.

### Real-Time Tracking

* Location updates published by authenticated drivers.
* SignalR delivery through private consumer groups.
* Recipients selected from current passenger assignments.
* Coordinate validation and per-connection update pacing.
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
* Driver ratings associated with accepted booking requests.

## Technology Stack

| Technology            | Purpose                              |
| --------------------- | ------------------------------------ |
| C# / .NET 10          | Backend development                  |
| ASP.NET Core          | REST API and authorization           |
| Entity Framework Core | Persistence, queries, and migrations |
| SQL Server            | Relational database                  |
| SignalR               | Real-time communication              |
| JWT Bearer            | Authentication                       |
| BCrypt                | Password hashing                     |
| Swagger / Swashbuckle | API documentation                    |

## Repository Structure

The application is located in **`Desktop/Vanos/Vanos.API`**.

The following directories are relative to that application folder:

| Directory     | Responsibility                            |
| ------------- | ----------------------------------------- |
| `Controllers` | API endpoints and application workflows   |
| `DTOs`        | Request and response contracts            |
| `Models`      | Domain entities                           |
| `Data`        | Database context and EF configuration     |
| `Services`    | Token generation and password hashing     |
| `Hubs`        | Real-time communication                   |
| `Extensions`  | Authenticated user identification helpers |
| `Migrations`  | Database schema history                   |

## Engineering Decisions

### Authorization Beyond Roles

Roles determine which operations an account can perform. Database relationships determine which records that account can access.

### Focused Marketplace Queries

Marketplace queries return discovery-specific fields and calculate availability and ratings in the database, avoiding additional queries for each driver.

### Consistent Seat Allocation

Booking acceptance checks capacity within a serializable transaction to protect passenger allocation during competing requests.

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
| `GET`   | `/api/students`                          | List owned student profiles     |
| `POST`  | `/api/hirerequests`                      | Submit a booking request        |
| `PATCH` | `/api/hirerequests/{id}/accept`          | Accept a booking request        |
| `PATCH` | `/api/hirerequests/{id}/reject`          | Reject a booking request        |
| `GET`   | `/api/monthlyfees/mine`                  | List owned invoices             |
| `POST`  | `/api/monthlyfees`                       | Create an invoice               |
| `POST`  | `/api/monthlyfees/{id}/simulate-payment` | Simulate payment                |

The tracking Hub is available at `/hubs/tracking`.

Marketplace discovery and personal invoice queries are available to `Parent` and `Student` accounts. Drivers use their corresponding management endpoints.

## Running Locally

### Prerequisites

* .NET 10 SDK.
* An accessible SQL Server instance.
* `dotnet-ef` 10.0.9, matching the project's EF Core version.

### 1. Clone the Repository

```bash
git clone https://github.com/cauanzzz/Vanos-Backend.git
cd Vanos-Backend/Desktop/Vanos/Vanos.API
dotnet restore
```

Run the remaining commands from this application directory.

### 2. Configure the Application

Set your SQL Server connection string:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
```

Set a randomly generated JWT signing secret:

```bash
dotnet user-secrets set "Jwt:Key" "YOUR_RANDOM_SECRET_OF_AT_LEAST_32_BYTES"
```

Replace the placeholders with your own values. Keep credentials outside version-controlled files.

In Development, the application creates a temporary signing key if none is configured. Restarting the application invalidates tokens issued with that temporary key.

### 3. Build and Apply Migrations

If `dotnet-ef` is not installed:

```bash
dotnet tool install --global dotnet-ef --version 10.0.9
```

Build the application and apply the committed migrations:

```bash
dotnet build
dotnet ef database update
```

The `CompleteVanosMvp` migration is already included. There is no need to generate it again during setup.

### 4. Start the API

```bash
dotnet run --launch-profile https
```

With the default HTTPS launch profile, Swagger is available at:

https://localhost:7154/swagger

Local addresses are configured in `Properties/launchSettings.json`.

Protected HTTP requests require a JWT in the authorization header:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

## Payment Simulation

Payment simulation requires both:

* The `Development` environment.
* `Payments:EnableSimulation` set to `true`.

Simulation is blocked in other environments, even when the configuration flag is enabled.

This module does not process real financial transactions.

## Validation Status

Local build, SQL Server migration application, token issuance, and selected endpoint flows have been manually exercised by the maintainer.

An automated test project is not currently included in this repository. Automated coverage and end-to-end validation of tracking isolation and concurrent operations remain pending.

## Roadmap

* Restore and integrate automated tests.
* Integrate a payment provider with authenticated webhooks.
* Introduce transportation contracts and recurring invoice generation.
* Add trip lifecycle management with explicit tracking start and end.
* Implement driver document verification.
* Add spatially indexed geographic queries.
* Support SignalR distribution across multiple application instances.

---

**Vanos — transportation discovery, passenger management, and real-time visibility in one platform.**
