# PFE-Back

Backend service for the OCRify project, providing document processing and OCR capabilities.

## Branch Structure

The project maintains two main branches to support different development environments:

### `main` branch
- **Purpose**: Windows development environment
- **Database**: SQL Server with Windows Authentication
- **Connection String**: 
  ```
  Server=.;Database=pfeDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
  ```
- **Setup**: 
  1. Install SQL Server on Windows
  2. Create database 'pfeDB'
  3. Run migrations
  4. Configure appsettings.json for Windows environment

### `mac-support` branch
- **Purpose**: macOS development environment
- **Database**: SQL Server running in Docker
- **Connection String**:
  ```
  Server=localhost,1433;Database=pfeDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true
  ```
- **Setup**:
  1. Install Docker Desktop for Mac
  2. Run SQL Server container
  3. Run migrations
  4. Configure appsettings.json for Docker environment

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Git
- SQL Server (Windows) or Docker (macOS)
- Visual Studio 2022 (Windows) or VS Code (macOS/macOS)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/PFE-Project.git
   ```

2. Switch to the appropriate branch:
   - For Windows: `git checkout main`
   - For macOS: `git checkout mac-support`

3. Install dependencies:
   ```bash
   dotnet restore
   ```

4. Configure the database:
   - Windows: Use the connection string from `main` branch
   - macOS: Use the connection string from `mac-support` branch

5. Run migrations:
   ```bash
   dotnet ef database update
   ```

6. Start the application:
   ```bash
   dotnet run
   ```

## Project Structure

```
PFE-Back/
├── Controllers/     # API endpoints
├── Models/         # Database models
├── Services/       # Business logic
├── DTOs/          # Data transfer objects
├── Interfaces/    # Service interfaces
└── uploads/       # User uploaded files (gitignored)
```

## Environment Setup

### Windows Environment
1. Install SQL Server
2. Create database
3. Update connection string in `appsettings.json`
4. Run the application

### macOS Environment
1. Install Docker Desktop
2. Run SQL Server container:
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 --name sql_server -d mcr.microsoft.com/mssql/server:2022-latest
   ```
3. Update connection string in `appsettings.json`
4. Run the application

## Important Notes

- The `uploads` directory is gitignored for security reasons
- Each environment has its own database configuration
- Make sure to use the correct branch for your operating system
- Keep sensitive information in environment variables or secure configuration

## Contributing

1. Create a feature branch from the appropriate main branch
2. Make your changes
3. Submit a pull request to the corresponding branch
4. Ensure all tests pass
5. Get code review approval

## Security

- Never commit sensitive information
- Keep API keys and secrets in environment variables
- The uploads directory is excluded from version control
- Use appropriate authentication and authorization

## License

[Your License Here] 