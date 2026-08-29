# 🛡️ SecureShare

[![.NET Core](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Supported-2496ED?logo=docker)](https://www.docker.com/)
[![Terraform](https://img.shields.io/badge/Terraform-AWS-7B42BC?logo=terraform)](https://www.terraform.io/)
[![Jenkins](https://img.shields.io/badge/Jenkins-CI%2FCD-D24939?logo=jenkins)](https://www.jenkins.io/)
[![Grafana](https://img.shields.io/badge/Grafana-Monitoring-F46800?logo=grafana)](https://grafana.com/)

SecureShare is a high-performance, encrypted file-sharing platform engineered with a secure .NET Clean Architecture and a modern, automated DevOps lifecycle. This project demonstrates a complete end-to-end cloud-native application, from code commit to a fully monitored multi-container deployment on AWS.

---

## ✨ Key Features

*   **Secure & Encrypted Core:** Robust backend cryptography ensures files are secure at rest. Built using a multi-project Clean Architecture split (`SecureShare.API` and `SecureShare.Core`).
*   **Infrastructure as Code (IaC):** Automated cloud environment provisioning on AWS utilizing **Terraform**.
*   **Continuous Integration & Deployment (CI/CD):** Declarative **Jenkins** pipelines automate Docker image builds, container registry pushes, and zero-downtime deployments via SSH.
*   **Observability & Dashboards as Code:** Native integration with **Prometheus** for metrics collection and dynamic **Grafana** provisioning for real-time telemetry and process monitoring.
*   **Containerized Architecture:** Fully dockerized ecosystem allowing seamless transitions from local development to production servers.

---

## 🛠️ Technology Stack

| Category | Technology |
| :--- | :--- |
| **Backend Framework** | C# / .NET (Core Architecture) |
| **Database Engine** | Microsoft SQL Server Express |
| **Containerization** | Docker Engine & Docker Compose |
| **Infrastructure Provisioning** | Terraform (AWS Provider) |
| **CI/CD Automation** | Jenkins Automation Server |
| **Telemetry / Time-Series DB** | Prometheus |
| **Visualization** | Grafana |

---

## 📂 Complete Project Structure

```text
SecureShare/
├── SecureShare.API/                  # Presentation Layer (.NET Web API)
│   ├── Controllers/                  # API endpoints (Upload, Share, Download)
│   ├── appsettings.json              # App configuration & DB connection strings
│   └── Program.cs                    # App entry point & Dependency Injection
│
├── SecureShare.Core/                 # Domain & Business Logic Layer
│   ├── Models/                       # Domain entities and contracts
│   └── Services/                     # Cryptography and file handling rules
│
├── grafana/                          # Dashboards as Code
│   ├── dashboards/                   # Pre-patched .NET telemetry dashboards
│   └── provisioning/                 # Automated Grafana provider & datasource configs
│
├── prometheus/                       # Monitoring Infrastructure
│   └── prometheus.yml                # Scrape configuration for the API metrics
│
├── teraform/                         # Infrastructure as Code (AWS)
│   └── main.tf                       # Terraform configs for AWS EC2/VPC deployment
│
├── Dockerfile                        # Multi-stage production container manifest
├── docker-compose.yml                # Multi-container orchestration (API, DB, Monitoring)
├── Jenkinsfile                       # CI/CD Declarative Pipeline
├── index.html                        # Front-end landing / test portal
└── SecureShare.slnx                  # .NET Solution file
```

---

## 🚀 Getting Started (Local Development)

To run the entire multi-tier system locally, you only need Git and Docker installed.

### 1. Clone the Repository
```bash
git clone https://github.com/Hari-haran-22/SecureShare.git
cd SecureShare
```

### 2. Spin Up Infrastructure
Launch the API, SQL Database, Prometheus, and Grafana simultaneously using Docker Compose:
```bash
docker-compose up -d --build
```

### 3. Access the Services
Once initialized, the platform services are exposed locally at:
*   **Swagger API Portal:** [http://localhost:8080/swagger](http://localhost:8080/swagger)
*   **Grafana Telemetry Dashboard:** [http://localhost:3000](http://localhost:3000) (Check the automated `.NET Metrics` dashboard)
*   **Web Portal UI:** Open `index.html` in your browser.

---

## ☁️ Infrastructure as Code (AWS Delivery)

To provision a pristine production environment in AWS, navigate to the terraform directory:

```bash
cd teraform
terraform init
terraform plan
terraform apply -auto-approve
```
*Note: Ensure your AWS CLI is configured with the appropriate IAM credentials before applying.*

---

## 🔄 CI/CD Pipeline Flow (Jenkins)

The delivery pipeline defined in the `Jenkinsfile` executes the following stages automatically on commit:

1.  **Checkout SCM:** Pulls the latest codebase from GitHub.
2.  **Build Image:** Compiles C# binaries via a multi-stage Dockerfile and builds the production image.
3.  **Push to Registry:** Authenticates via Jenkins credentials and pushes the artifact to Docker Hub.
4.  **Deploy to AWS:** Connects to the provisioned AWS EC2 instance via SSH, pulls the latest images, and restarts the environment using `docker-compose` for a seamless update.

---

## 📊 Observability & Monitoring

The system utilizes PromQL queries to map deep runtime metrics:
*   **Instance Auto-Discovery:** Dynamic tracking of container targets across teardowns without hardcoded IPs.
*   **Performance Metrics:** Real-time monitoring of Garbage Collection (GC), thread pools, and CPU/Memory allocations to ensure the server behaves predictably under heavy file I/O load.

---

## 🧪 QA & Testing Strategy

This repository serves as an excellent foundation for advanced Quality Assurance practices:
*   **API Automation:** Validate file upload integrity, encryption success, and access control via Postman or REST Assured.
*   **Load Testing:** Simulate concurrent high-volume file transfers using JMeter/k6 while monitoring hardware limits via Grafana.
*   **Security Audits:** Test for Insecure Direct Object References (IDOR) and validate Terraform configs using IaC security scanners.