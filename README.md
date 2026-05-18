# SecureShare

SecureShare is a high-performance, encrypted file-sharing platform designed with a secure .NET architecture and a modern, automated DevOps lifecycle. The project transitions seamlessly from code commitment to multi-container cloud environments using GitOps configurations, Automated CI/CD pipelines, Infrastructure as Code (IaC), and comprehensive full-stack monitoring.

## 🚀 Key Features

- **Secure & Encrypted Core:** Robust backend cryptography utilizing a multi-project clean architecture split (`SecureShare.API` and `SecureShare.Core`).
- **Infrastructure as Code (IaC):** Automated AWS environment provisioning using custom **Terraform** scripts.
- **Continuous Integration & Deployment (CI/CD):** A comprehensive declarative **Jenkins** pipeline automating Docker builds, multi-platform image registry pushing, safe remote host file management, and zero-downtime SSH target deployment.
- **Dashboards as Code (Observability):** Native integration with **Prometheus** for metrics collection and dynamic **Grafana** GitOps provisioning—completely patched for real-time container instance tracking and dynamic datasource rendering.

---

## 📂 Project Structure

Below is the complete architectural layout of the SecureShare repository:

```text
SecureShare/
│
├── grafana/                          # Observability Configurations (Dashboards as Code)
│   ├── dashboards/
│   │   └── dotnet-metrics.json       # Pre-patched .NET Prometheus telemetry dashboard
│   └── provisioning/
│       ├── dashboards/
│       │   └── dashboards.yml        # Automated Grafana dashboard provider specification
│       └── datasources/
│           └── datasources.yml       # Automated local Prometheus data target registration
│
├── prometheus/                       # Time-Series Database Infrastructure
│   └── prometheus.yml                # Scrape configuration targeted at secureshare-api metrics
│
├── SecureShare.API/                  # Presentation / Endpoint Layer (.NET Core)
│   ├── Controllers/                  # API routing endpoints (File Uploads, Shares, Download management)
│   ├── Properties/
│   ├── appsettings.json              # Configuration file (DB strings, system boundaries)
│   ├── Program.cs                    # Application entry point & service dependency injections
│   └── SecureShare.API.csproj
│
├── SecureShare.Core/                 # Domain / Business Logic Layer
│   ├── Models/                       # Core system entities and structural contracts
│   ├── Services/                     # Cryptographic processors and file handling business rules
│   └── SecureShare.Core.csproj
│
├── .dockerignore                     # Optimizes Docker builds by ignoring local binaries and assets
├── .gitignore                        # Prevents build binaries and IDE caches from tracking to GitHub
├── docker-compose.yml                # Multi-container orchestration (API, Database, Prometheus, Grafana)
├── Dockerfile                        # Multi-stage, production-optimized container manifest
├── index.html                        # Front-end / Landing test environment portal
├── Jenkinsfile                       # Production-grade declarative pipeline code for automated deployment
├── main.tf                           # Terraform configuration mapping infrastructure to AWS (EC2/VPC)
└── README.md                         # Detailed project system specification manual
🛠️ Tech Stack & Infrastructure Components
Backend Architecture: C# / .NET 10.0 runtime Core Environment

Database Engine: Microsoft SQL Server Express Core Instance

Containerization Engine: Docker Engine & Docker Compose Orchestration

Infrastructure Provisioning: Terraform (AWS Provider)

CI/CD Automation Runner: Jenkins Automation Server

Telemetry & Monitoring: Prometheus (Scraper) & Grafana (Visualization Suite)

🔧 Installation & Deployment Guide
1. Local Orchestration Setup
To clone, construct, and execute the entire multi-tier system directly on a local development host:

Bash
# Clone the repository
git clone [https://github.com/Hari-haran-22/SecureShare.git](https://github.com/Hari-haran-22/SecureShare.git)
cd SecureShare

# Spin up all infrastructure containers simultaneously
docker compose up -d --build
Once initialized, the platform endpoints expose directly through:

SecureShare API Portal: http://localhost:8080/swagger

Grafana Live Telemetry Dashboard: http://localhost:3000

2. Infrastructure as Code (AWS Cloud Delivery)
To spin up a pristine production VM target dynamically inside AWS:

Bash
# Initialize providers and construct target architecture
terraform init
terraform plan
terraform apply -auto-approve
3. Continuous Integration Flow (Jenkins Pipeline)
The delivery pipeline defined inside the Jenkinsfile runs automated routines through three foundational stages:

Checkout SCM: Code pull from GitHub verifying integrity and fetching latest provisioning schemas.

Build Image: Compiles raw C# binaries through optimized multi-stage Docker environments and tags images to a registries index.

Push to Registry: Authenticates with secure registry contexts (withCredentials) to deploy artifacts to Docker Hub storage handles.

Deploy to AWS: Automatically logs into target instance boundaries via secure SSH keys, forces clean tree states via git reset --hard HEAD, updates configs, hooks live telemetry databases, and brings the stack back online seamlessly via Docker Compose.

📊 Monitoring Dashboard Notes
The dashboard maps metrics using custom Prometheus PromQL queries. Key elements include:

Instance Auto-Discovery: The variable system utilizes label_values(up, instance) ensuring dynamic cloud target visualization across environment teardowns without hardcoding.

Stability Metrics: Tracks real-time GC Collection metrics, thread lifecycles, and process allocations to ensure the server behaves predictably under load.

***

### Steps to Save this to your Repository:
1. Open your local `SecureShare` directory in VS Code.
2. Open your `README.md` file.
3. Replace its entire contents with the markdown block above.
4. Stage, commit, and push it up using your Git terminal:
   ```bash
   git add README.md
   git commit -m "Docs: Updated README with complete project structure and DevOps layout"
   git push