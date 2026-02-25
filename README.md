# 🔒 SecureShare .NET

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC292B?logo=microsoftsqlserver&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)

**SecureShare** is a self-hosted, web-based utility designed to share sensitive documents securely over insecure channels. It allows users to upload a file, encrypt it on the server using AES-256, and generate a unique, one-time or time-sensitive download link. Once the file is downloaded or the link expires, the system permanently locks the data.

## ✨ Core Features

* **Server-Side Encryption:** Files are encrypted stream-to-stream using `System.Security.Cryptography` (AES-256) before ever being written to the disk.
* **Self-Destructing Links:** Links automatically expire based on a user-defined time limit (e.g., 24 hours) or a strict download count (e.g., 1 download only).
* **Anti-Enumeration Security:** The API uses cryptographically secure GUIDs for download URLs and returns generic `404 Not Found` responses for expired files to prevent IDOR (Insecure Direct Object Reference) and enumeration attacks.
* **Clean Architecture:** Built with a decoupled Core (business logic) and API (infrastructure) layer, utilizing the Repository Pattern and Dependency Injection.
* **Integrated Web Portal:** Includes a built-in, responsive HTML/JS frontend to easily upload and generate links without needing third-party tools like Postman.

## 🛠️ Technology Stack

* **Backend:** ASP.NET Core Web API (.NET 9)
* **Database:** SQL Server Express
* **ORM:** Entity Framework Core (Code-First approach)
* **Frontend:** Vanilla HTML, CSS, and JavaScript (Fetch API)

## 📂 Project Structure

```text
SecureShare/
├── SecureShare.Core/        # Domain layer (No external dependencies)
│   ├── Entities/            # Database models (FileRecord, AccessLog)
│   └── Interfaces/          # Contracts (IFileEncryptionService, etc.)
├── SecureShare.API/         # Application & Infrastructure layer
│   ├── Controllers/         # API Endpoints (FilesController)
│   ├── Services/            # AES Encryption & Storage Implementations
│   ├── Data/                # EF Core ApplicationDbContext
│   └── wwwroot/             # Frontend web assets (index.html)
└── SecureShare.sln          # Solution file