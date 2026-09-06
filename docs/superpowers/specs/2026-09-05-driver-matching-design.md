# Driver Matching (Discovery, Hire Requests, Ratings) — Design

## Context

Vanos is a mobile app (built separately as an Expo/React Native project) for
the Brazilian "van escolar" market, connecting van drivers with students and
their parents/guardians. The long-term product has two pillars:

1. **Matching/marketplace** — parents discover and hire a van driver.
2. **Live tracking** — drivers share live location, parents see ETA.

This spec covers **matching only**. Live tracking is out of scope and will
get its own design later.

The backend (`Vanos.API`, this repo — ASP.NET Core + EF Core + SQL Server,
shared with collaborator `cauanzzz`) was originally built assuming a driver
and a family already have an offline relationship: students are created
directly under a `DriverId`. There is currently no way for a parent to find
or request a driver. This spec adds that.

It also fixes a pre-existing bug: `AuthController.Login` does not verify the
password and hardcodes the returned role to `"Driver"`.

## Goals

- A parent can register, add their student(s), search for drivers by school
  and/or location, and send a hire request.
- A driver can register, declare which schools they serve, and accept/reject
  incoming hire requests.
- Accepting a request assigns the driver to the student (`Student.DriverId`).
- A parent can rate a driver after being hired by them; ratings are visible
  in search results.

## Non-goals

- Live GPS tracking, routes, or trip state.
- Payment processing (existing `MonthlyFee` tracking is untouched).
- Multi-driver simultaneous requests per student (one Pending request at a
  time — see Hire flow).
- Driver-rates-parent (one-directional rating only).

## Auth & roles

- `User.Role` becomes a real value: `"Driver"` or `"Parent"` (was hardcoded
  to `"Driver"` in the JWT regardless of the actual user).
- `AuthController`:
  - `POST /api/auth/register` — creates a `User` with a hashed password and
    role. If `role == "Driver"`, also creates the linked `Driver` profile
    (name, CPF, phone, plate, capacity, Pix key) in the same request and
    sets `User.DriverId`. If `role == "Parent"`, no extra profile is needed
    up front (students are added afterward).
  - `POST /api/auth/login` — verifies the password against `PasswordHash`
    (currently missing) and issues a JWT whose claims include the user's
    real `Role` and their user id (`sub`), not just their email.
- Existing `User.DriverId` (nullable) is unchanged and used only for
  Drivers. Parents do not get an equivalent id on `User`; their students
  link back to them via the new `Student.ParentId`.

## Schema changes

- `Student.DriverId`: **int → int?** (nullable). A student can now exist
  unassigned while the parent searches for a driver. All existing
  validation that currently requires `DriverId` on creation
  (`StudentsController.PostStudent`) is removed for this field.
- `Student.ParentId` (new, `int`, required): the `User.Id` of the parent who
  created the student. Replaces the implicit assumption that whoever calls
  the creation endpoint is authorized; the parent is taken from the JWT.
- `Driver.Latitude`, `Driver.Longitude` (new, `double?`): the driver's base
  service area, set by the driver in their profile. Used for
  distance-based search.
- `DriverSchool` (new join table): `DriverId`, `SchoolId`. Composite key.
  Represents the schools a driver explicitly declares they serve —
  independent of whether they have any hired students yet.
- `HireRequest` (new table):
  - `Id`, `StudentId`, `DriverId`
  - `Status`: `Pending` | `Accepted` | `Rejected`
  - `CreatedAt`, `RespondedAt` (nullable)
  - Constraint: at most one `Pending` request per `StudentId` at a time,
    enforced in application logic (a partial unique index would require
    SQL Server filtered index support — acceptable to add later as
    hardening, not required for this spec).
- `Rating` (new table):
  - `Id`, `DriverId`, `ParentId`, `Score` (1-5, int), `Comment` (string,
    optional), `CreatedAt`
  - Unique on (`DriverId`, `ParentId`) — rating again updates the existing
    row (upsert) rather than creating a duplicate.
  - A row may only be created/updated if an `Accepted` `HireRequest` exists
    linking a student of that `ParentId` to that `DriverId`.

All of the above ship as EF Core migrations on top of the existing three
(`InitialCreate`, `RelateStudentToSchool`, `AddUserAuthenticationTable`).

## Endpoints

| Method | Route | Role | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | — | Creates User (+Driver if role=Driver) |
| POST | `/api/auth/login` | — | Verifies password, real role in JWT |
| GET | `/api/drivers/search?schoolId=&lat=&lng=&radiusKm=` | Parent | Returns drivers serving the school and/or within radius, with average rating and available capacity (`StudentCapacity` minus current `Accepted`-linked students) |
| PUT | `/api/drivers/{id}/schools` | Driver | Replaces the driver's `DriverSchool` rows with the given list |
| POST | `/api/students` | Parent | `ParentId` from JWT; `DriverId` omitted/null at creation |
| POST | `/api/hirerequests` | Parent | Creates a Pending request for their own student → a driver. Rejected (400) if the student already has a Pending request or an assigned driver |
| GET | `/api/hirerequests?driverId=` | Driver | Requests addressed to the caller's own driver id (from JWT) |
| GET | `/api/hirerequests?parentId=` | Parent | Requests for the caller's own students |
| PATCH | `/api/hirerequests/{id}/accept` | Driver | Must be addressed to caller's own `DriverId`. Sets Accepted, sets `Student.DriverId`, auto-rejects any other Pending requests for that student |
| PATCH | `/api/hirerequests/{id}/reject` | Driver | Must be addressed to caller's own `DriverId`. Sets Rejected |
| POST | `/api/ratings` | Parent | Upsert; only if caller has an Accepted `HireRequest` with that driver |
| GET | `/api/drivers/{id}/ratings` | — | List + average, used by search results and driver profile |

All endpoints except register/login require a valid JWT.
`[Authorize(Roles="Driver")]` / `[Authorize(Roles="Parent")]` gate the
routes above accordingly. Ownership (a driver only acting on their own
`DriverId`, a parent only on their own students) is enforced in the handler
using the id from the JWT claims, never from a client-supplied path/query
param.

## Business rules recap

- A student can have at most one `Pending` hire request at a time.
- Accepting a request: sets `Student.DriverId`, marks the request
  `Accepted`, and auto-rejects any other `Pending` requests for that same
  student (defensive; only one should exist given the rule above).
- A rating requires a pre-existing `Accepted` `HireRequest` between that
  parent (via one of their students) and that driver.
- Search results show a driver's average rating and remaining capacity
  (`StudentCapacity` − count of students with `DriverId` = that driver and
  an `Accepted` request).

## Testing

- EF Core migrations for every schema change listed above.
- xUnit unit tests for the rules most likely to be gotten wrong:
  - one-Pending-request-per-student enforcement
  - accept auto-rejecting other pending requests
  - rating rejected without a prior Accepted hire request
  - driver available-capacity calculation
- Extend the existing `Vanos.API.http` file with the full flow: register
  parent → register driver → driver sets schools → parent creates student →
  parent searches → parent sends hire request → driver accepts → parent
  rates.
- No mobile/UI testing in this spec — the Expo app is a separate spec built
  against these endpoints once they exist.
