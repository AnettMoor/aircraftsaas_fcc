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


### C# backend ((mvc pages) will be removed later): http://localhost:5219
### Vue frontend: http://localhost:5173


### Seeded users (username, password, role)
        ("1@3", "3", ["Normal"]),
        ("1@2", "2", ["CompanyOwner"]),
         ("1@4", "4", ["SystemAdmin"])

Vue frontend: only Normal(pilot) and CompanyOwner(rents out planes) views
