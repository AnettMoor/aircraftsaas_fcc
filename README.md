# Local run

### start DB
docker-compose up -d postgres-dev

### run backend (terminal1)
cd backend_csharp
dotnet run --project WebApp

### run frontend (terminal2)
cd frontend_vue
npm install
npm run dev


Vue frontend: http://localhost:5173
C# backend (mvc pages will be removed later): http://localhost:5219

### Seeded users (username, password, role)
        ("1@3", "3", ["Normal"]),
        ("1@2", "2", ["CompanyOwner"]),
         ("1@4", "4", ["SystemAdmin"])