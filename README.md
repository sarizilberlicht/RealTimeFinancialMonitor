# Real-Time Financial Monitor

A full-stack real-time financial transaction monitoring application built with ASP.NET Core, React, SignalR, Redis, Docker, and Kubernetes.

The project implements the required MVP while also demonstrating a distributed multi-instance architecture, shared storage, real-time synchronization, containerization, Kubernetes deployment, and enhanced UI behavior.

---
## Quick Start

### Docker Compose

The fastest way to run the complete application:

```bash
docker compose up --build
```

Then open:

```text
http://localhost:5173
```

Redis is started automatically by Docker Compose.

To stop the application:

```bash
docker compose down
```

### Kubernetes

After deploying the Kubernetes manifests as described below, access the application locally using:

```bash
kubectl port-forward service/frontend-service 30173:80
```

Then open:

```text
http://localhost:30173
```

### Tests

Backend:

```bash
dotnet test Backend.Tests
```

The Redis integration test requires Redis on `localhost:6379`. If needed:

```bash
docker run --name realtime-financial-test-redis -p 6379:6379 -d redis:7-alpine
```

Frontend:

```bash
cd Frontend
npm test -- --run
```

Detailed setup, architecture, Docker, Redis, and Kubernetes instructions are provided below.

---
## Features

- Create financial transactions through a web form
- Display transactions in a live monitoring dashboard
- Receive transaction updates in real time using SignalR
- Filter transactions by status
- Load existing transactions after page refresh
- Prevent duplicate transactions
- Update the status of an existing transaction in real time
- Visual status indicators for Pending, Completed, and Failed transactions
- Entry animation for newly received transactions
- Smooth visual transitions when transaction status changes
- Thread-safe in-memory storage for the MVP
- Redis shared storage for distributed execution
- Redis SignalR backplane for communication between backend replicas
- Docker Compose environment for local execution
- Kubernetes deployment with multiple backend replicas
- Automatic Pod recovery through Kubernetes Deployments

---

# Technology Stack

## Backend

- .NET 9
- ASP.NET Core Web API
- SignalR
- StackExchange.Redis
- Redis SignalR Backplane
- xUnit
- Moq

## Frontend

- React
- TypeScript
- Vite
- React Router
- Microsoft SignalR JavaScript client
- Vitest
- React Testing Library
- Nginx

## Infrastructure

- Docker
- Docker Compose
- Kubernetes
- Redis

---

# Architecture

## Application Flow

```text
                         Browser
                            |
                            v
                     Frontend Service
                            |
                            v
                      Frontend Pod
                         Nginx
                       /       \
                      /         \
             React static       API / SignalR
                 files               |
                                     v
                              Backend Service
                               /           \
                              v             v
                        Backend Pod 1   Backend Pod 2
                              \             /
                               \           /
                                    Redis
                              /             \
                    Shared Storage     SignalR Backplane
```

Only the frontend needs to be exposed to the client.

The backend and Redis remain internal services.

Nginx serves the React application and acts as a reverse proxy:

```text
/api/*             -> backend-service:8080
/transactionHub    -> backend-service:8080
```

The React application therefore does not need to know which backend instance handles a request.

---

# Storage Strategy

The application supports two execution modes.

## MVP / Local Mode

By default:

```text
Distributed.Enabled = false
```

The application uses:

```text
InMemoryTransactionStore
```

This provides the in-memory storage required by the MVP without requiring any external infrastructure.

```text
Backend
   |
   v
In-Memory Store
```

## Distributed Mode

For Docker and Kubernetes:

```text
Distributed.Enabled = true
```

The application uses:

```text
RedisTransactionStore
+
Redis SignalR Backplane
```

```text
Backend A ----\
               \
                -> Redis
               /
Backend B ----/
```

This allows multiple backend replicas to share the same transaction state.

---

# Why Redis?

Redis performs two different roles in distributed mode.

## Shared Transaction Storage

Using application memory with multiple backend instances would result in each instance maintaining independent state.

For example:

```text
Backend A memory -> Transaction 1
Backend B memory -> Transaction 2
```

A browser refresh could be routed to either backend and therefore return different data.

Redis provides a shared transaction store:

```text
Backend A ----\
               -> Redis -> Shared Transactions
Backend B ----/
```

Both backend instances therefore see the same transactions.

## SignalR Backplane

SignalR connections belong to individual backend instances.

For example:

```text
Browser
   |
   v
Backend B
```

while a transaction may be processed by:

```text
Backend A
```

Without synchronization, Backend A would not automatically be able to send the event through Backend B's SignalR connection.

The Redis backplane distributes SignalR messages between backend instances:

```text
Backend A
    |
    v
Redis Backplane
    |
    v
Backend B
    |
    v
Browser
```

This allows real-time updates to work across backend replicas.

---

# SignalR and WebSockets

The frontend connects to SignalR using WebSockets directly:

```typescript
.withUrl(`${apiBaseUrl}/transactionHub`, {
    transport: signalR.HttpTransportType.WebSockets,
    skipNegotiation: true,
})
```

This is intentional for the multi-replica environment.

During testing with multiple backend replicas, the standard SignalR negotiation flow could result in:

```text
/negotiate -> Backend Pod A
/connect   -> Backend Pod B
```

The second Pod does not own the connection created during negotiation by the first Pod, which may result in a `404`.

Using WebSockets directly removes the separate negotiation request and establishes a persistent connection with one backend instance.

Redis then distributes SignalR events between replicas.

Another possible production solution would be session affinity (sticky sessions).

---

# Transaction Model

A transaction contains:

```text
TransactionId
Amount
Currency
Status
Timestamp
```

Supported statuses:

```text
Pending
Completed
Failed
```

---

# Backend API

## Create Transaction

```http
POST /api/transactions
```

Example request:

```json
{
  "transactionId": "5f5d9cf1-dcff-4693-a391-09b9e23e9d6c",
  "amount": 1500.50,
  "currency": "USD",
  "status": "Pending",
  "timestamp": "2026-08-20T10:00:00Z"
}
```

The transaction is stored and broadcast to connected clients through SignalR.

---

## Get Transactions

```http
GET /api/transactions
```

Returns the currently stored transactions.

This endpoint allows the monitor to restore its state after a browser refresh.

---

## Update Transaction Status

```http
PATCH /api/transactions/{id}/status
```

Example request:

```json
{
  "status": "Completed"
}
```

If the transaction exists:

```text
Store is updated
      ↓
Updated transaction is broadcast through SignalR
      ↓
Frontend receives the same TransactionId
      ↓
Existing table row is updated
```

If the transaction does not exist, the API returns:

```http
404 Not Found
```

This endpoint was added to demonstrate real-time status changes and the optional enhanced UI behavior.

---

# Frontend

The frontend contains two main routes:

```text
/add
/monitor
```

## Add Transaction

The Add Transaction page provides a form for creating financial transactions.

The transaction is sent to the backend through the REST API.

## Live Monitor

The Monitor page:

- Loads existing transactions from the backend
- Opens a SignalR WebSocket connection
- Receives transactions in real time
- Updates existing transactions when their status changes
- Prevents duplicate rows
- Sorts transactions by timestamp
- Filters transactions by status
- Displays status indicators
- Handles rapid transaction updates

---

# Enhanced UI

New transactions are animated when they appear in the monitor.

Status badges use CSS transitions so that status changes are visually smooth.

For example:

```text
Pending
   ↓
Completed
```

The frontend recognizes transactions by `TransactionId`.

When a SignalR event is received:

```text
TransactionId does not exist
        ↓
Add new row
```

or:

```text
TransactionId already exists
        ↓
Update existing row
```

This prevents duplicate transactions while supporting live status updates.

---

# Storage Abstraction

The backend depends on:

```csharp
ITransactionStore
```

rather than directly depending on a particular storage technology.

Implementations:

```text
ITransactionStore
       |
       +-- InMemoryTransactionStore
       |
       +-- RedisTransactionStore
```

The service layer therefore does not need to know where transactions are stored.

The concrete implementation is selected during application startup based on configuration.

```text
Distributed.Enabled = false
        ↓
InMemoryTransactionStore
```

```text
Distributed.Enabled = true
        ↓
RedisTransactionStore
```

This keeps the application loosely coupled and testable while supporting both the simple MVP and distributed execution.

---

# Dependency Injection

ASP.NET Core Dependency Injection is used to provide application dependencies.

Examples include:

```text
ITransactionStore
IConnectionMultiplexer
IHubContext<TransactionHub>
TransactionService
```

`TransactionService` depends on the `ITransactionStore` abstraction rather than a concrete implementation.

The application can therefore use:

```text
InMemoryTransactionStore
```

for the MVP and:

```text
RedisTransactionStore
```

for distributed execution without changing the business service.

Tests can also provide a fake store.

---

# Configuration

The default configuration uses local MVP mode.

Example `appsettings.json`:

```json
{
  "Distributed": {
    "Enabled": false
  },

  "Redis": {
    "ConnectionString": "localhost:6379",
    "TransactionsKey": "transactions"
  }
}
```

When distributed mode is disabled, Redis is not required by the application.

Docker and Kubernetes override the configuration using environment variables:

```text
Distributed__Enabled=true
Redis__ConnectionString=...
Redis__TransactionsKey=transactions
```

Examples:

```text
Local MVP
Distributed.Enabled=false
→ InMemoryTransactionStore

Docker Compose
Distributed.Enabled=true
→ RedisTransactionStore
→ Redis Backplane

Kubernetes
Distributed.Enabled=true
→ RedisTransactionStore
→ Redis Backplane
```

---

# Frontend Environment Configuration

The frontend reads:

```text
VITE_API_BASE_URL
```

For direct local Vite development, it can point directly to the backend:

```env
VITE_API_BASE_URL=http://localhost:5032
```

When running through Docker or Kubernetes, it is empty:

```text
VITE_API_BASE_URL=
```

The frontend then sends relative requests:

```text
/api/transactions
/transactionHub
```

and Nginx proxies them internally to the backend.

This avoids hard-coding environment-specific backend addresses in the frontend application.

---

# Running Locally

## Backend

From the project root:

```bash
dotnet run --project Backend
```

By default:

```text
Distributed.Enabled=false
```

so the backend uses in-memory storage and Redis is not required.

---

## Frontend

```bash
cd Frontend
npm install
npm run dev
```

For direct local development, configure:

```env
VITE_API_BASE_URL=http://localhost:5032
```

Then open:

```text
http://localhost:5173
```

---

# Running with Docker Compose

Docker Compose runs the complete distributed application locally:

```text
Frontend / Nginx
Backend
Redis
```

From the project root:

```bash
docker compose up --build
```

Then open:

```text
http://localhost:5173
```

To stop the environment:

```bash
docker compose down
```

---

## Redis Readiness

The Redis container includes a health check:

```yaml
healthcheck:
  test: ["CMD", "redis-cli", "ping"]
  interval: 2s
  timeout: 2s
  retries: 10
```

The backend waits for Redis to become healthy before starting.

This is important because:

```text
Container started
```

does not necessarily mean:

```text
Service is ready to accept connections
```

The Redis client is also configured not to abort permanently if the first connection attempt fails, allowing it to retry during transient startup conditions.

Redis is not exposed to the host in the Compose environment because only the backend needs to access it.

The backend connects internally using:

```text
redis:6379
```

---

# Testing

## Backend Tests

Run:

```bash
dotnet test Backend.Tests
```

The backend test suite covers behavior including:

- Transaction processing
- In-memory storage
- Concurrent storage operations
- Transaction status updates
- SignalR broadcasting
- Controller status updates
- `404 Not Found` for missing transactions

---

## Redis Integration Test

The Redis store test uses a real Redis instance and therefore acts as an integration test.

It verifies that transactions written through one Redis store instance are visible through another instance.

For example, a temporary Redis instance can be started with:

```bash
docker run --name realtime-financial-test-redis -p 6379:6379 -d redis:7-alpine
```

Then run:

```bash
dotnet test Backend.Tests
```

The Redis test validates shared storage behavior required by the distributed architecture.

---

## Frontend Tests

From the `Frontend` directory:

```bash
npm test -- --run
```

The frontend tests cover behavior including:

- Rendering the transaction table
- Receiving transactions through SignalR
- Loading existing transactions
- Status filtering
- Duplicate prevention
- Status indicators
- Handling 100 rapidly received transactions
- WebSocket-only SignalR configuration
- Updating an existing transaction when its status changes
- Add Transaction form behavior
- Success and error states

To verify the production build:

```bash
npm run build
```

---

# Testing Approach

The project follows a test-first approach where appropriate.

The development cycle used:

```text
RED
 ↓
Write a failing test

GREEN
 ↓
Implement the minimum behavior required

REFACTOR
 ↓
Improve the implementation while keeping tests green
```

Examples where this approach was used include:

- Transaction storage
- Concurrent writes
- Transaction processing
- Real-time transaction handling
- Duplicate prevention
- Status updates
- Controller behavior
- Frontend status updates

Infrastructure behavior was validated using integration and smoke tests rather than unit tests.

---

# Kubernetes

Kubernetes manifests are located in:

```text
k8s/
├── backend.yaml
├── frontend.yaml
└── redis.yaml
```

The distributed Kubernetes environment contains:

```text
Frontend
    |
    v
Backend Service
    |
    +---- Backend Pod 1
    |
    +---- Backend Pod 2
              |
              v
             Redis
```

The backend Deployment uses two replicas.

Kubernetes therefore maintains:

```text
backend replicas = 2
```

If one backend Pod is deleted or crashes, the Deployment automatically creates another Pod to restore the desired replica count.

This behavior was verified during development.

---

# Kubernetes Services

## Backend

The backend uses a `ClusterIP` Service.

It is accessible internally through:

```text
backend-service:8080
```

## Redis

Redis is also internal:

```text
redis-service:6379
```

## Frontend

The frontend is the client-facing component.

For the local Kubernetes demonstration it is accessed using port forwarding.

---

# Running on Docker Desktop Kubernetes

First verify that Kubernetes is running:

```bash
kubectl cluster-info
kubectl get nodes
```

The node should report:

```text
Ready
```

---

## Build Images

Backend:

```bash
docker build -t realtime-financial-backend:v2 ./Backend
```

Frontend:

```bash
docker build \
  --build-arg VITE_API_BASE_URL= \
  -t realtime-financial-frontend:v2 \
  ./Frontend
```

---

## Local Kubernetes Image Availability

In the local Docker Desktop Kubernetes environment used during development, the Kubernetes node used a container image store separate from the normal Docker CLI image store.

Therefore locally built images had to be imported into the Kubernetes node before deployment.

Example for the backend:

```bash
docker save realtime-financial-backend:v2 -o backend-v2.tar

docker cp backend-v2.tar \
  desktop-control-plane:/backend-v2.tar

docker exec desktop-control-plane \
  ctr -n k8s.io images import /backend-v2.tar
```

The same approach can be used for the frontend image.

This is specific to the local development environment.

In a normal production workflow:

```text
Docker Build
     ↓
Container Registry
     ↓
Kubernetes pulls Image
```

so manual image importing would not be necessary.

---

# Deploying to Kubernetes

Apply the manifests:

```bash
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/backend.yaml
kubectl apply -f k8s/frontend.yaml
```

Verify the deployment:

```bash
kubectl get deployments
kubectl get pods
kubectl get services
```

Expected application Pods:

```text
backend-...     1/1 Running
backend-...     1/1 Running
frontend-...    1/1 Running
redis-...       1/1 Running
```

---

# Accessing the Kubernetes Application

For the local Docker Desktop Kubernetes environment:

```bash
kubectl port-forward service/frontend-service 30173:80
```

Then open:

```text
http://localhost:30173
```

`kubectl port-forward` is used only as a local development/demo mechanism.

A production deployment would normally expose the application using an Ingress or LoadBalancer with HTTPS/TLS.

---

# Distributed Architecture Verification

The distributed architecture was manually verified using two backend replicas.

The following behaviors were tested:

```text
Two backend replicas running
        ↓
Both access the same Redis transaction storage
        ↓
Browser refresh returns consistent data
```

Real-time behavior was also verified:

```text
Browser connected to one backend
        ↓
Transaction handled by backend
        ↓
Redis SignalR backplane
        ↓
SignalR event reaches browser
```

Kubernetes self-healing was tested by deleting a backend Pod:

```text
kubectl delete pod <backend-pod>
```

The Deployment automatically created a replacement Pod to restore the configured replica count.

---

# Real-Time Status Update Demo

To demonstrate the enhanced UI, create a transaction with:

```text
Pending
```

while the Monitor page is open.

Then send:

```http
PATCH /api/transactions/{id}/status
```

with:

```json
{
  "status": "Completed"
}
```

The expected behavior is:

```text
Pending
   ↓
Completed
```

without refreshing the browser.

The existing row is updated instead of creating a duplicate, and the status badge changes visually.

This verifies the complete flow:

```text
PATCH request
     ↓
Controller
     ↓
TransactionService
     ↓
Transaction Store
     +
SignalR Broadcast
     ↓
React State Update
     ↓
CSS Status Transition
```

---

# Challenges and Design Decisions

## In-Memory vs Distributed Storage

The MVP requires simple local transaction storage.

`InMemoryTransactionStore` satisfies this requirement and provides a lightweight default mode.

However, application memory cannot provide consistent state across multiple backend replicas.

For distributed execution, `RedisTransactionStore` is selected through configuration.

This preserves the simple MVP architecture while allowing the same application to scale to multiple backend instances.

---

## SignalR with Multiple Backend Replicas

With multiple backend replicas, the standard SignalR negotiation process could be routed across different Pods:

```text
/negotiate -> Pod A
/connect   -> Pod B
```

This caused intermittent `404` responses during testing.

The final implementation uses:

```text
WebSockets
+
skipNegotiation
+
Redis SignalR Backplane
```

The WebSocket connection remains attached to one backend instance while Redis distributes events generated by other replicas.

---

## Duplicate Events

A transaction may be loaded from the REST API while also arriving through SignalR.

Simply appending every event could therefore create duplicate rows.

The frontend merges transactions using:

```text
TransactionId
```

If the ID does not exist, the transaction is added.

If it already exists, the existing transaction is updated.

This also enables real-time status changes.

---

## Redis Startup Timing

A container being started does not guarantee that the service inside it is ready.

During development, the backend could attempt to connect to Redis before Redis was ready, causing startup failure.

The Docker Compose configuration therefore uses a Redis health check and waits for:

```text
redis-cli ping
→ PONG
```

before starting the backend.

The Redis client is also configured to tolerate an initial connection failure and retry.

---

## Local Kubernetes Images

The local Kubernetes node did not automatically see images built through the normal Docker CLI.

Images were therefore imported manually into the node's container runtime.

A production environment would use a container registry instead.

---

## Local Kubernetes Networking

Direct NodePort access through `localhost` was not consistently available in the local Kubernetes environment.

`kubectl port-forward` was therefore used for the local demonstration.

A production deployment would normally use an Ingress or LoadBalancer.

---

# Production Considerations

This project focuses on demonstrating the requested architecture and real-time behavior in a local development environment.

For a production deployment, additional considerations would include:

- Container registry
- Managed Redis or durable database storage
- Redis persistence/high availability
- Kubernetes Ingress
- HTTPS/TLS
- Authentication and authorization
- Secrets management
- Liveness and readiness probes
- Resource requests and limits
- Horizontal Pod Autoscaling
- Centralized logging
- Metrics and monitoring
- Distributed tracing
- CI/CD pipeline

---

# Project Structure

```text
RealTimeFinancialMonitor/
│
├── Backend/
│   ├── Controllers/
│   ├── Hubs/
│   ├── Models/
│   ├── Services/
│   ├── Storage/
│   ├── Dockerfile
│   ├── appsettings.json
│   └── Program.cs
│
├── Backend.Tests/
│   ├── InMemoryTransactionStoreTests.cs
│   ├── RedisTransactionStoreTests.cs
│   ├── TransactionServiceTests.cs
│   └── TransactionsControllerTests.cs
│
├── Frontend/
│   ├── src/
│   │   ├── pages/
│   │   └── types/
│   ├── Dockerfile
│   ├── nginx.conf
│   └── vite.config.ts
│
├── k8s/
│   ├── backend.yaml
│   ├── frontend.yaml
│   └── redis.yaml
│
├── docker-compose.yml
├── .gitignore
└── README.md
```

---

# Summary

The project starts with a simple MVP architecture:

```text
React
  ↓
ASP.NET Core
  ↓
In-Memory Storage
```

and can run in distributed mode as:

```text
                 React / Nginx
                       |
                       v
                 Backend Service
                  /           \
                 v             v
           Backend Pod 1   Backend Pod 2
                  \           /
                   \         /
                      Redis
                 /             \
           Shared Store    SignalR Backplane
```

This allows the same application to demonstrate both the required MVP and the optional distributed architecture while keeping the application components loosely coupled and testable.
