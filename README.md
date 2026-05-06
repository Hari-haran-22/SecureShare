# 🔒 SecureShare .NET

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC292B?logo=microsoftsqlserver&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white)
![Jenkins](https://img.shields.io/badge/Jenkins-D24939?logo=jenkins&logoColor=white)
![AWS EC2](https://img.shields.io/badge/AWS_EC2-FF9900?logo=amazonaws&logoColor=white)

**SecureShare** is a self-hosted, web-based utility designed to share sensitive documents securely over insecure channels. It allows users to upload a file, encrypt it on the server using AES-256, and generate a unique, one-time or time-sensitive download link. Once the file is downloaded or the link expires, the system permanently locks the data.

## ✨ Core Features

* **Server-Side Encryption:** Files are encrypted stream-to-stream using `System.Security.Cryptography` (AES-256) before ever being written to the disk.
* **Self-Destructing Links:** Links automatically expire based on a user-defined time limit (e.g., 24 hours) or a strict download count (e.g., 1 download only).
* **Anti-Enumeration Security:** The API uses cryptographically secure GUIDs for download URLs and returns generic `404 Not Found` responses for expired files to prevent IDOR (Insecure Direct Object Reference) and enumeration attacks.
* **Clean Architecture:** Built with a decoupled Core (business logic) and API (infrastructure) layer, utilizing the Repository Pattern and Dependency Injection.
* **Integrated Web Portal:** Includes a built-in, responsive HTML/JS frontend to easily upload and generate links without needing third-party tools like Postman.
* **Containerized Deployment:** Fully dockerized via a multi-stage build for consistent local development and lightweight AWS production environments.
* **Zero-Touch CI/CD Pipeline:** Fully automated Jenkins pipeline handles compilation, Docker image registry pushes, and secure SSH deployments to the cloud.

## 🛠️ Technology Stack

* **Backend:** ASP.NET Core Web API (.NET 9)
* **Database:** SQL Server Express
* **ORM:** Entity Framework Core (Code-First approach)
* **Frontend:** Vanilla HTML, CSS, and JavaScript (Fetch API)
* **DevOps & Cloud:** Docker, Docker Hub, Jenkins, Git, AWS EC2 (Ubuntu Linux)

## 🏗️ DevSecOps & CI/CD Architecture

This project implements a strict Continuous Integration and Continuous Deployment (CI/CD) pipeline to ensure reliable, zero-downtime updates:
1. **Source Control:** Code updates are pushed to the `master` branch on GitHub.
2. **Continuous Integration:** A Jenkins service fetches the latest commit and compiles the .NET 9 application inside a temporary container to guarantee a clean build.
3. **Registry Push:** Jenkins securely authenticates via vaulted access tokens and pushes the compiled multi-stage image to Docker Hub (`hari2haran2/secureshare-api`).
4. **Continuous Deployment:** Jenkins establishes a secure SSH connection to the live AWS EC2 instance, dynamically managing temporary Windows key file permissions (`icacls`), and executes a container swap to launch the live application.

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
├── Dockerfile               # Multi-stage Docker build configuration
├── Jenkinsfile              # Declarative CI/CD pipeline script
└── SecureShare.sln          # Solution file