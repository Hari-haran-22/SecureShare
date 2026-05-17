# 🔒 SecureShare

SecureShare is a highly secure, encrypted file-sharing system built with **.NET 9**. It features a fully automated DevSecOps CI/CD pipeline for seamless containerized deployment to AWS, complete with real-time enterprise observability.

---

## 📂 Project Structure

```text
SecureShare/
├── SecureShare.API/            # Core .NET 9 Web API Application
│   ├── Controllers/            # API Endpoints
│   ├── Program.cs              # App entry point & Prometheus middleware
│   └── SecureShare.API.csproj  # API Project file
├── SecureShare.Core/           # Class Library (Business Logic & Models)
│   └── SecureShare.Core.csproj # Core Project file
├── terraform/                  # Infrastructure as Code (IaC)
│   └── main.tf                 # AWS EC2 & Security Group provisioning
├── .gitignore                  # Git rules for .NET, Terraform, & binaries
├── docker-compose.yml          # Multi-container orchestration (API + Monitoring)
├── Dockerfile                  # Multi-stage Docker build for the .NET API
├── Jenkinsfile                 # Declarative CI/CD Pipeline configuration
├── prometheus.yml              # Prometheus scraping configuration
└── README.md                   # Project documentation

This is a fantastic idea. A professional README isn't complete without a clear repository map. It helps other developers (and future you) instantly understand where everything lives before they even open a single file.

Here is your finalized, comprehensive README.md that now includes the exact folder structure of your solution alongside the DevSecOps architecture we built.

Copy this block, replace your current README.md, and push it to GitHub!

Markdown
# 🔒 SecureShare

SecureShare is a highly secure, encrypted file-sharing system built with **.NET 9**. It features a fully automated DevSecOps CI/CD pipeline for seamless containerized deployment to AWS, complete with real-time enterprise observability.

---

## 📂 Project Structure

```text
SecureShare/
├── SecureShare.API/            # Core .NET 9 Web API Application
│   ├── Controllers/            # API Endpoints
│   ├── Program.cs              # App entry point & Prometheus middleware
│   └── SecureShare.API.csproj  # API Project file
├── SecureShare.Core/           # Class Library (Business Logic & Models)
│   └── SecureShare.Core.csproj # Core Project file
├── terraform/                  # Infrastructure as Code (IaC)
│   └── main.tf                 # AWS EC2 & Security Group provisioning
├── .gitignore                  # Git rules for .NET, Terraform, & binaries
├── docker-compose.yml          # Multi-container orchestration (API + Monitoring)
├── Dockerfile                  # Multi-stage Docker build for the .NET API
├── Jenkinsfile                 # Declarative CI/CD Pipeline configuration
├── prometheus.yml              # Prometheus scraping configuration
└── README.md                   # Project documentation
🚀 Tech Stack & Architecture
Backend: C# / .NET 9 Web API

Database: SQL Server Express

Containerization: Docker & Docker Compose

Infrastructure as Code (IaC): Terraform

Cloud Provider: Amazon Web Services (AWS EC2)

CI/CD Pipeline: Jenkins (Automated via GitHub Webhooks)

Observability: Prometheus & Grafana

🏗️ Infrastructure & Deployment Pipeline
This project utilizes a modern Continuous Integration and Continuous Deployment (CI/CD) architecture with an integrated monitoring stack.

The CI/CD Flow
Push: Code is pushed to the master branch on GitHub.

Trigger: A GitHub Webhook instantly notifies the Jenkins server.

Build (CI): Jenkins checks out the repository, restores .NET dependencies, and compiles the application.

Containerize: Jenkins builds a new Docker Image containing the compiled .NET API.

Registry: The image is securely pushed to Docker Hub (hari2haran2/secureshare-api:latest).

Deploy (CD): Jenkins establishes a secure SSH connection to the live AWS EC2 instance, clones the latest infrastructure files, and runs docker compose up -d to spin up the API and the monitoring stack.

Infrastructure (Terraform)
The AWS infrastructure is completely automated using Terraform.

main.tf: Provisions a t3.micro Ubuntu EC2 instance, configures a Security Group (opening ports 22, 3000, 8080, and 9090), and automatically installs Docker and Docker Compose upon boot.

State Management: .gitignore strictly protects .terraform/ binaries and .tfstate files from being committed to version control.

📊 Observability Stack
The live application is actively monitored using a sidecar pattern via Docker Compose:

Prometheus-Net: The .NET application exposes real-time runtime metrics via the /metrics endpoint.

Prometheus: Scrapes and stores the time-series data from the API container.

Grafana: Visualizes the metrics (CPU load, memory allocation, HTTP request duration) in custom dashboards.

Live Ports:

:8080 - SecureShare .NET API

:9090 - Prometheus Server

:3000 - Grafana Dashboards

💻 Local Development Setup
To run this application locally on your Windows machine:

Prerequisites:

.NET 9 SDK

Docker Desktop

1. Clone the repository

Bash
git clone [https://github.com/Hari-haran-22/SecureShare.git](https://github.com/Hari-haran-22/SecureShare.git)
cd SecureShare
2. Run the Full Stack (API + Monitoring)

Bash
docker compose up -d
Alternatively, to run just the API for debugging:

Bash
cd SecureShare.API
dotnet run
☁️ Cloud Provisioning Guide
If you are a repository administrator and need to spin up the cloud infrastructure from scratch:

1. Configure AWS CLI
Ensure your local terminal is authenticated with your AWS IAM User credentials.

Bash
aws configure
2. Provision the Server
Navigate to the terraform directory and deploy the infrastructure.

Bash
cd terraform
terraform init
terraform apply
3. Teardown
To prevent unwanted AWS Free Tier charges when not in use:

Bash
terraform destroy
🛡️ Security Best Practices Implemented
Least Privilege: Infrastructure provisioning utilizes a dedicated AWS IAM User rather than the Root account.

SSH Key Management: Strict Windows file permissions strip inheritance from .pem files, and Jenkins manages deployment keys securely via its internal credential vault.

Hidden Variables: No IP addresses, database passwords, or secret keys are hardcoded in the source code or Jenkinsfile.

Git Hygiene: Local .exe binaries and Terraform state files are strictly ignored to prevent repository bloat and secret leakage.